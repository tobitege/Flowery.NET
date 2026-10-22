using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Services;
using Xunit;
using Xunit.Sdk;

namespace Flowery.NET.RuntimeTests;

/// <summary>
/// Discovers xunit tests in this assembly and runs them one by one on the UI thread of the test window.
/// </summary>
internal static class RuntimeTestRunner
{
    /// <summary>The theme every test starts with: the library's base theme when the run started.</summary>
    internal static readonly string BaseTheme = DaisyThemeManager.BaseThemeName;

    private const string DynamicSkipPrefix = "$XunitDynamicSkip$";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(2);
    private static readonly List<Exception> UnhandledExceptions = [];

    public static async Task<int> RunAsync(Window window, string reportPath, string? placement)
    {
        Dispatcher.UIThread.VerifyAccess();
        PinGlobalState();
        var host = new Grid();
        window.Content = host;
        RuntimeTestContext.Initialize(window, host, Path.Combine(Path.GetDirectoryName(reportPath)!, "artifacts"));
        await RuntimeTestHelpers.WaitForRenderAsync();

        var filter = Environment.GetEnvironmentVariable("FLOWERY_RUNTIME_TEST_FILTER");
        var testCases = Discover()
            .Where(test => string.IsNullOrWhiteSpace(filter) || test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (testCases.Length == 0)
            throw new InvalidOperationException($"No runtime tests match '{filter}'.");

        // An exception that escapes to the dispatcher (for example from an async event handler) fails the running test.
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        var results = new List<RuntimeTestResult>();
        await using var progress = new StreamWriter(Path.ChangeExtension(reportPath, ".progress.log")) { AutoFlush = true };
        if (placement is not null)
            await progress.WriteLineAsync($"PLACEMENT {placement}");
        try
        {
            foreach (var testCase in testCases)
            {
                await progress.WriteLineAsync($"START {testCase.Name}");
                var result = await ExecuteAsync(testCase);
                results.Add(result);
                await progress.WriteLineAsync($"{result.Outcome switch { RuntimeTestOutcome.Passed => "PASS", RuntimeTestOutcome.Inconclusive => "WARN", _ => "FAIL" }} {testCase.Name}");
                if (result.Message is not null)
                    await progress.WriteLineAsync(result.Message);

                host.Children.Clear();
                PinGlobalState();
            }
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= OnUnhandledException;
        }

        RuntimeTestReport.Write(reportPath, results);
        return results.Any(result => result.Outcome == RuntimeTestOutcome.Failed) ? 1 : 0;
    }

    /// <summary>
    /// Pins the global state that tests may depend on, independent of the host's settings and of earlier tests.
    /// </summary>
    private static void PinGlobalState()
    {
        // Saved control state must not leak into tests, and tests must not write user data.
        StateStorageProvider.Configure(new InMemoryStateStorage());
        FloweryLocalization.SetCulture(CultureInfo.GetCultureInfo("en"));
        FlowerySizeManager.Reset();
        // Rendering assertions use normal motion and contrast regardless of the Windows settings.
        DaisyAccessibility.ReducedMotionOverride = false;
        DaisyAccessibility.HighContrastOverride = false;
        if (!DaisyThemeManager.ApplyTheme(BaseTheme))
            throw new InvalidOperationException($"The {BaseTheme} theme could not be applied.");
    }

    private static IEnumerable<RuntimeTestCase> Discover()
    {
        var testClasses = typeof(RuntimeTestRunner).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var testClass in testClasses)
        {
            var methods = testClass.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
                .OrderBy(method => method.MetadataToken);
            foreach (var method in methods)
            {
                if (method.GetCustomAttribute<FactAttribute>() is not { } fact)
                    continue;

                if (method.GetParameters().Length == 0)
                {
                    yield return new RuntimeTestCase(testClass, method, [], fact.Skip);
                    continue;
                }

                var rows = method.GetCustomAttributes<InlineDataAttribute>().ToArray();
                if (rows.Length == 0)
                {
                    yield return new RuntimeTestCase(testClass, method, [], fact.Skip,
                        "Runtime tests with parameters need [InlineData] rows.");
                    continue;
                }

                foreach (var row in rows)
                    yield return new RuntimeTestCase(testClass, method, row.Data, fact.Skip ?? row.Skip);
            }
        }
    }

    private static async Task<RuntimeTestResult> ExecuteAsync(RuntimeTestCase testCase)
    {
        if (testCase.SkipReason is { } skipReason)
            return new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Inconclusive, $"Skipped: {skipReason}");
        if (testCase.DiscoveryError is { } discoveryError)
            return new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Failed, discoveryError);

        UnhandledExceptions.Clear();
        try
        {
            var instance = testCase.Method.IsStatic ? null : Activator.CreateInstance(testCase.TestClass);
            try
            {
                var task = testCase.Method.Invoke(instance, testCase.Arguments) switch
                {
                    Task returned => returned,
                    ValueTask returned => returned.AsTask(),
                    _ => Task.CompletedTask
                };
                await task.WaitAsync(TestTimeout);
            }
            finally
            {
                if (instance is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else if (instance is IDisposable disposable)
                    disposable.Dispose();
            }

            if (UnhandledExceptions.Count > 0)
                throw new AggregateException("The test left unhandled exceptions on the UI thread.", UnhandledExceptions.ToArray());

            return new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Passed, null);
        }
        catch (Exception exception)
        {
            var cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
            // An inconclusive test could not set up its scenario in this environment; it warns instead of failing.
            // The reason alone is reported; the stack of a skip carries no information.
            return cause is SkipException
                ? new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Inconclusive, StripSkipPrefix(cause.Message))
                : new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Failed, cause.ToString());
        }
    }

    private static string StripSkipPrefix(string message) =>
        message.StartsWith(DynamicSkipPrefix, StringComparison.Ordinal) ? message[DynamicSkipPrefix.Length..] : message;

    private static void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        UnhandledExceptions.Add(e.Exception);
        e.Handled = true;
    }

    private sealed record RuntimeTestCase(
        Type TestClass,
        MethodInfo Method,
        object?[] Arguments,
        string? SkipReason,
        string? DiscoveryError = null)
    {
        public string Name => Arguments.Length == 0
            ? $"{TestClass.FullName}.{Method.Name}"
            : $"{TestClass.FullName}.{Method.Name}({string.Join(", ", Arguments.Select(FormatArgument))})";

        private static string FormatArgument(object? argument) => argument switch
        {
            null => "null",
            string text => $"\"{text}\"",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => argument.ToString() ?? string.Empty
        };
    }
}
