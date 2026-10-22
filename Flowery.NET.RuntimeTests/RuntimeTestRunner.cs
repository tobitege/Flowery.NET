using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Tests;
using Flowery.Services;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Flowery.NET.RuntimeTests;

/// <summary>
/// Discovers xunit tests in this assembly and runs them one by one on the UI thread of the test window.
/// </summary>
internal static class RuntimeTestRunner
{
    /// <summary>The theme every test starts with: the library's base theme when the run started.</summary>
    internal static readonly string BaseTheme = DaisyThemeManager.BaseThemeName;

    private const string DynamicSkipPrefix = "$XunitDynamicSkip$";
    // xunit runs tests without a timeout indefinitely; here they get this much before the run stops.
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
    private static readonly List<Exception> UnhandledExceptions = [];

    public static async Task<int> RunAsync(Window window, string reportPath, string? placement)
    {
        Dispatcher.UIThread.VerifyAccess();
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        PinGlobalState();
        var host = new Grid();
        window.Content = host;
        RuntimeTestContext.Initialize(window, host, Path.Combine(Path.GetDirectoryName(reportPath)!, "artifacts"));
        await RuntimeTestHelpers.WaitForRenderAsync();

        var filter = Environment.GetEnvironmentVariable("FLOWERY_RUNTIME_TEST_FILTER");
        var testCases = Select(Discover(typeof(RuntimeTestRunner).Assembly.GetTypes()), filter);
        if (testCases.Count == 0)
            throw new InvalidOperationException($"No runtime tests match '{filter}'.");

        // An exception that escapes to the dispatcher (for example from an async event handler) fails the running test.
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        try
        {
            await using var progress = new StreamWriter(Path.ChangeExtension(reportPath, ".progress.log")) { AutoFlush = true };
            if (placement is not null)
                await progress.WriteLineAsync($"PLACEMENT {placement}");
            var results = await RunCasesAsync(testCases, host, progress);
            RuntimeTestReport.Write(reportPath, results);
            return results.Any(result => result.Outcome == RuntimeTestOutcome.Failed) ? 1 : 0;
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= OnUnhandledException;
        }
    }

    /// <summary>
    /// Runs the test cases in order. A timed-out test cannot be cancelled on the UI thread and would interfere
    /// with later tests, so the run stops there and reports the remaining tests as not run.
    /// </summary>
    internal static async Task<List<RuntimeTestResult>> RunCasesAsync(IReadOnlyList<RuntimeTestCase> testCases, Panel? host, TextWriter progress)
    {
        var results = new List<RuntimeTestResult>();
        string? timedOut = null;
        foreach (var testCase in testCases)
        {
            if (timedOut is not null)
            {
                results.Add(new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Failed,
                    $"Not run: {timedOut} timed out and may still be running on the UI thread."));
                continue;
            }

            await progress.WriteLineAsync($"START {testCase.Name}");
            var (result, isTimedOut) = await ExecuteAsync(testCase, host);
            results.Add(result);
            await progress.WriteLineAsync($"{result.Outcome switch { RuntimeTestOutcome.Passed => "PASS", RuntimeTestOutcome.Inconclusive => "WARN", _ => "FAIL" }} {testCase.Name}");
            if (result.Message is not null)
                await progress.WriteLineAsync(result.Message);
            if (isTimedOut)
                timedOut = testCase.Name;
        }

        return results;
    }

    /// <summary>
    /// Selects the tests to run. Explicit tests run only when the filter names them, by full name or by method.
    /// </summary>
    internal static List<RuntimeTestCase> Select(IEnumerable<RuntimeTestCase> testCases, string? filter)
    {
        var selected = new List<RuntimeTestCase>();
        foreach (var testCase in testCases)
        {
            var matches = string.IsNullOrWhiteSpace(filter)
                ? !testCase.IsExplicit
                : testCase.IsExplicit
                    ? string.Equals(testCase.Name, filter, StringComparison.OrdinalIgnoreCase)
                      || string.Equals(testCase.MethodName, filter, StringComparison.OrdinalIgnoreCase)
                    : testCase.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            if (matches)
                selected.Add(testCase);
        }

        return selected;
    }

    internal static IEnumerable<RuntimeTestCase> Discover(IEnumerable<Type> types)
    {
        var testClasses = types
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

                if (method.ReturnType == typeof(void) && method.GetCustomAttribute<AsyncStateMachineAttribute>() is not null)
                {
                    // The runner could not await it; the body would run into later tests.
                    yield return new RuntimeTestCase(testClass, method, fact, null, "Async void test methods are not supported; return a Task.");
                    continue;
                }

                if (method.GetParameters().Length == 0)
                {
                    yield return new RuntimeTestCase(testClass, method, fact, null);
                    continue;
                }

                var rows = method.GetCustomAttributes<InlineDataAttribute>().ToArray();
                if (rows.Length == 0)
                {
                    yield return new RuntimeTestCase(testClass, method, fact, null, "Runtime tests with parameters need [InlineData] rows.");
                    continue;
                }

                foreach (var row in rows)
                    yield return new RuntimeTestCase(testClass, method, fact, row);
            }
        }
    }

    /// <summary>
    /// Runs one test, then its teardown: the dispatcher catches up, the host panel is cleared and the global state
    /// is pinned again. Exceptions of the teardown and exceptions that reached the dispatcher fail this test.
    /// </summary>
    internal static async Task<(RuntimeTestResult Result, bool TimedOut)> ExecuteAsync(RuntimeTestCase testCase, Panel? host)
    {
        if (testCase.DiscoveryError is { } discoveryError)
            return (new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Failed, discoveryError), false);

        Exception? failure = null;
        var timedOut = false;
        try
        {
            if (testCase.ResolveSkipReason() is { } skipReason)
                return (new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Inconclusive, $"Skipped: {skipReason}"), false);

            var instance = testCase.Method.IsStatic ? null : Activator.CreateInstance(testCase.TestClass);
            try
            {
                var task = testCase.Method.Invoke(instance, testCase.Arguments) switch
                {
                    Task returned => returned,
                    ValueTask returned => returned.AsTask(),
                    _ => Task.CompletedTask
                };
                try
                {
                    await task.WaitAsync(testCase.Timeout);
                }
                catch (TimeoutException) when (!task.IsCompleted)
                {
                    timedOut = true;
                    throw new TimeoutException(string.Create(CultureInfo.InvariantCulture,
                        $"The test did not finish within {testCase.Timeout.TotalSeconds:0.###} seconds."));
                }
            }
            finally
            {
                // A timed-out test is still running; it keeps its instance.
                if (!timedOut)
                {
                    if (instance is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync();
                    else if (instance is IDisposable disposable)
                        disposable.Dispose();
                }
            }
        }
        catch (Exception exception)
        {
            failure = RuntimeTestHelpers.Unwrap(exception);
        }

        if (!timedOut)
        {
            try
            {
                await TearDownAsync(host);
            }
            catch (Exception exception)
            {
                failure = failure is null ? exception : new AggregateException("The test and its teardown failed.", failure, exception);
            }
        }

        var unhandled = UnhandledExceptions.ToArray();
        UnhandledExceptions.Clear();
        if (unhandled.Length > 0)
        {
            var dispatcherFailure = new AggregateException("The test left unhandled exceptions on the UI thread.", unhandled);
            failure = failure is null ? dispatcherFailure : new AggregateException("The test failed.", failure, dispatcherFailure);
        }

        // An inconclusive test could not set up its scenario in this environment; it warns instead of failing.
        // The reason alone is reported; the stack of a skip carries no information.
        return failure switch
        {
            null => (new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Passed, null), false),
            SkipException skip => (new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Inconclusive, StripSkipPrefix(skip.Message)), false),
            _ when testCase.IsSkipException(failure) => (new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Inconclusive, failure.Message), false),
            _ => (new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Failed, failure.ToString()), timedOut)
        };
    }

    private static async Task TearDownAsync(Panel? host)
    {
        // Work the test posted (async handlers, Loaded callbacks) runs while its controls are still attached.
        await SettleDispatcherAsync();
        host?.Children.Clear();
        PinGlobalState();
        await SettleDispatcherAsync();
    }

    // Lower than every priority that controls post work with, so all of it runs first.
    private static async Task SettleDispatcherAsync() =>
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle);

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

    private static string StripSkipPrefix(string message) =>
        message.StartsWith(DynamicSkipPrefix, StringComparison.Ordinal) ? message[DynamicSkipPrefix.Length..] : message;

    private static void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        UnhandledExceptions.Add(e.Exception);
        e.Handled = true;
    }

    /// <summary>One test method, or one [InlineData] row of a theory, with xunit's skip, explicit and timeout settings.</summary>
    internal sealed record RuntimeTestCase(Type TestClass, MethodInfo Method, FactAttribute Fact, InlineDataAttribute? Row, string? DiscoveryError = null)
    {
        public object?[] Arguments => Row?.Data ?? [];

        public string MethodName => $"{TestClass.FullName}.{Method.Name}";

        public string Name => Row is null ? MethodName : $"{MethodName}({string.Join(", ", Arguments.Select(FormatArgument))})";

        // A row inherits these from the method unless it sets its own.
        public bool IsExplicit => ((IDataAttribute?)Row)?.Explicit ?? Fact.Explicit;

        public TimeSpan Timeout => (((IDataAttribute?)Row)?.Timeout ?? Fact.Timeout) is > 0 and var milliseconds
            ? TimeSpan.FromMilliseconds(milliseconds)
            : DefaultTimeout;

        /// <summary>
        /// The skip reason, if the test is skipped. As in xunit, SkipUnless or SkipWhen name a public static bool
        /// property that makes the skip conditional; a row's own Skip replaces the method's.
        /// </summary>
        public string? ResolveSkipReason()
        {
            var (reason, type, unless, when) = Row?.Skip is not null
                ? (Row.Skip, Row.SkipType, Row.SkipUnless, Row.SkipWhen)
                : (Fact.Skip, Fact.SkipType, Fact.SkipUnless, Fact.SkipWhen);
            if (reason is null)
                return null;
            if (unless is not null && when is not null)
                throw new InvalidOperationException("SkipUnless and SkipWhen cannot both be set.");
            if ((unless ?? when) is not { } propertyName)
                return reason;

            var source = type ?? TestClass;
            var property = source.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property?.GetValue(null) is not bool value)
                throw new InvalidOperationException($"{source.FullName}.{propertyName} must be a public static bool property.");

            return unless is not null ? (value ? null : reason) : (value ? reason : null);
        }

        public bool IsSkipException(Exception exception)
        {
            foreach (var type in Fact.SkipExceptions ?? [])
            {
                if (type.IsInstanceOfType(exception))
                    return true;
            }

            return false;
        }

        private static string FormatArgument(object? argument) => argument switch
        {
            null => "null",
            string text => $"\"{text}\"",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => argument.ToString() ?? string.Empty
        };
    }
}
