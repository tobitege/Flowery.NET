using System.Diagnostics;
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

    private static readonly double DefaultNeumorphicIntensity = DaisyNeumorphic.GlobalIntensity;

    private const string DynamicSkipPrefix = "$XunitDynamicSkip$";
    // xunit runs tests without a timeout indefinitely; here they get this much before the run stops.
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
    private static readonly List<Exception> UnhandledExceptions = [];

    /// <summary>Exceptions that reached the dispatcher and were not yet blamed on a test.</summary>
    internal static int PendingUnhandledExceptions => UnhandledExceptions.Count;

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

            // Work that the last test posted late would otherwise be lost when the run ends.
            await SettleDispatcherAsync();
            if (TakeUnhandledExceptions() is { } late)
                results.Add(new RuntimeTestResult("RuntimeTests.AfterLastTest", RuntimeTestOutcome.Failed, late.ToString()));

            RuntimeTestReport.Write(reportPath, results);
            return RuntimeTestReport.HasFailures(results) ? 1 : 0;
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
    internal static async Task<List<RuntimeTestResult>> RunCasesAsync(IReadOnlyList<RuntimeTestCase> testCases, Panel host, TextWriter progress)
    {
        var results = new List<RuntimeTestResult>();
        string? timedOut = null;
        foreach (var testCase in testCases)
        {
            if (timedOut is not null)
            {
                results.Add(new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.NotRun,
                    $"{timedOut} timed out and may still be running on the UI thread."));
                continue;
            }

            await progress.WriteLineAsync($"START {testCase.Name}");
            var result = await ExecuteAsync(testCase, host);
            results.Add(result);
            await progress.WriteLineAsync($"{ProgressLabel(result.Outcome)} {testCase.Name}");
            if (result.Message is not null)
                await progress.WriteLineAsync(result.Message);
            if (result.Outcome == RuntimeTestOutcome.TimedOut)
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

                if (GetSignatureError(method) is { } signatureError)
                {
                    yield return new RuntimeTestCase(testClass, method, fact, null, signatureError);
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
    /// Runs one test, then its teardown, and blames the test for exceptions that reached the dispatcher meanwhile.
    /// A timed-out test is still running, so it gets no teardown; the run stops after it.
    /// </summary>
    internal static async Task<RuntimeTestResult> ExecuteAsync(RuntimeTestCase testCase, Panel host)
    {
        if (testCase.DiscoveryError is { } discoveryError)
            return new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Failed, discoveryError);

        string? skipReason = null;
        Exception? failure = null;
        try
        {
            skipReason = testCase.ResolveSkipReason();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (skipReason is null && failure is null)
        {
            var (testFailure, timedOut) = await InvokeAsync(testCase);
            if (timedOut)
                return new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.TimedOut, testFailure!.Message);
            failure = testFailure;
        }

        failure = Combine(failure, await TearDownAsync(host));
        failure = Combine(failure, TakeUnhandledExceptions());
        return Classify(testCase, skipReason, failure);
    }

    /// <summary>
    /// Creates the test class, runs the test with its timeout and disposes the instance.
    /// Returns the failure and whether the test timed out.
    /// </summary>
    private static async Task<(Exception? Failure, bool TimedOut)> InvokeAsync(RuntimeTestCase testCase)
    {
        object? instance;
        try
        {
            instance = testCase.Method.IsStatic ? null : Activator.CreateInstance(testCase.TestClass);
        }
        catch (Exception exception)
        {
            return (RuntimeTestHelpers.Unwrap(exception), false);
        }

        Exception? failure = null;
        try
        {
            var task = testCase.Method.Invoke(instance, testCase.Arguments) switch
            {
                Task returned => returned,
                ValueTask returned => returned.AsTask(),
                // Discovery admits only void, Task and ValueTask.
                _ => Task.CompletedTask
            };
            try
            {
                await task.WaitAsync(testCase.Timeout);
            }
            catch (TimeoutException) when (!task.IsCompleted)
            {
                // The test keeps running and keeps its instance.
                return (new TimeoutException(string.Create(CultureInfo.InvariantCulture,
                    $"The test did not finish within {testCase.Timeout.TotalSeconds:0.###} seconds.")), true);
            }
        }
        catch (Exception exception)
        {
            failure = RuntimeTestHelpers.Unwrap(exception);
        }

        return (Combine(failure, await DisposeAsync(instance)), false);
    }

    private static async Task<Exception?> DisposeAsync(object? instance)
    {
        try
        {
            if (instance is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (instance is IDisposable disposable)
                disposable.Dispose();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// Lets the test's posted work run, clears the host panel and pins the global state again. Each step runs
    /// even when an earlier one failed, so a failing teardown cannot leave its state to the next test.
    /// </summary>
    private static async Task<Exception?> TearDownAsync(Panel host)
    {
        // Work the test posted (async handlers, Loaded callbacks) runs while its controls are still attached.
        await SettleDispatcherAsync();
        Exception? failure = null;
        try
        {
            host.Children.Clear();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            PinGlobalState();
        }
        catch (Exception exception)
        {
            failure = Combine(failure, exception);
        }

        await SettleDispatcherAsync();
        return failure;
    }

    // Runs all work already posted to the dispatcher at a higher priority. Timers and thread-pool continuations
    // that post later are blamed on the test that is running then.
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
        // Neumorphic effects are off, with the library's default intensity and colors. Raised first resets
        // the mode that GlobalEnabled would restore.
        DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Raised;
        DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.None;
        DaisyNeumorphic.GlobalIntensity = DefaultNeumorphicIntensity;
        DaisyNeumorphic.GlobalDarkShadowColor = null;
        DaisyNeumorphic.GlobalLightShadowColor = null;
        if (!DaisyThemeManager.ApplyTheme(BaseTheme))
            throw new InvalidOperationException($"The {BaseTheme} theme could not be applied.");
    }

    /// <summary>
    /// An inconclusive test could not set up its scenario in this environment; it warns instead of failing.
    /// Every skip is reported as "Skipped: reason"; the stack of a skip carries no information.
    /// </summary>
    private static RuntimeTestResult Classify(RuntimeTestCase testCase, string? skipReason, Exception? failure) => failure switch
    {
        null when skipReason is not null => Skipped(testCase, skipReason),
        null => new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Passed, null),
        SkipException skip => Skipped(testCase, StripSkipPrefix(skip.Message)),
        _ when testCase.IsSkipException(failure) => Skipped(testCase, failure.Message),
        _ => new RuntimeTestResult(testCase.Name, RuntimeTestOutcome.Failed, failure.ToString())
    };

    private static RuntimeTestResult Skipped(RuntimeTestCase testCase, string reason) =>
        new(testCase.Name, RuntimeTestOutcome.Inconclusive, $"Skipped: {reason}");

    private static Exception? Combine(Exception? first, Exception? second) =>
        first is null ? second : second is null ? first : new AggregateException(first, second);

    private static Exception? TakeUnhandledExceptions()
    {
        if (UnhandledExceptions.Count == 0)
            return null;

        var exceptions = UnhandledExceptions.ToArray();
        UnhandledExceptions.Clear();
        return new AggregateException("Unhandled exceptions reached the UI thread.", exceptions);
    }

    /// <summary>The runner awaits only Task and ValueTask; any other result would pass without being awaited.</summary>
    private static string? GetSignatureError(MethodInfo method)
    {
        var returnType = method.ReturnType;
        if (returnType == typeof(void))
        {
            return method.GetCustomAttribute<AsyncStateMachineAttribute>() is null
                ? null
                : "Async void test methods are not supported; return a Task.";
        }

        return typeof(Task).IsAssignableFrom(returnType) || returnType == typeof(ValueTask)
            ? null
            : $"Runtime tests must return void, Task or ValueTask, not {returnType.Name}.";
    }

    private static string StripSkipPrefix(string message) =>
        message.StartsWith(DynamicSkipPrefix, StringComparison.Ordinal) ? message[DynamicSkipPrefix.Length..] : message;

    private static string ProgressLabel(RuntimeTestOutcome outcome) => outcome switch
    {
        RuntimeTestOutcome.Passed => "PASS",
        RuntimeTestOutcome.Inconclusive => "WARN",
        RuntimeTestOutcome.TimedOut => "TIMEOUT",
        _ => "FAIL"
    };

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

        // As in xunit, a debugger session has no timeout.
        public TimeSpan Timeout => Debugger.IsAttached
            ? System.Threading.Timeout.InfiniteTimeSpan
            : (((IDataAttribute?)Row)?.Timeout ?? Fact.Timeout) is > 0 and var milliseconds
                ? TimeSpan.FromMilliseconds(milliseconds)
                : DefaultTimeout;

        /// <summary>
        /// The skip reason, if the test is skipped. As in xunit, SkipUnless or SkipWhen name a public static bool
        /// property that makes the skip conditional, and they need Skip; a row's own Skip replaces the method's.
        /// </summary>
        public string? ResolveSkipReason()
        {
            if (Row is { Skip: null } && (Row.SkipUnless ?? Row.SkipWhen) is not null)
                throw new InvalidOperationException("You must set Skip when you set SkipUnless or SkipWhen.");

            var (reason, type, unless, when) = Row?.Skip is not null
                ? (Row.Skip, Row.SkipType, Row.SkipUnless, Row.SkipWhen)
                : (Fact.Skip, Fact.SkipType, Fact.SkipUnless, Fact.SkipWhen);
            if (unless is not null && when is not null)
                throw new InvalidOperationException("SkipUnless and SkipWhen cannot both be set.");
            if (reason is null)
            {
                return (unless ?? when) is null
                    ? null
                    : throw new InvalidOperationException("You must set Skip when you set SkipUnless or SkipWhen.");
            }

            if ((unless ?? when) is not { } propertyName)
                return reason;

            var source = type ?? TestClass;
            var property = source.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property?.GetValue(null) is not bool value)
                throw new InvalidOperationException($"{source.FullName}.{propertyName} must be a public static bool property.");

            return unless is not null ? (value ? null : reason) : (value ? reason : null);
        }

        /// <summary>As in xunit, the exception's exact type must be listed in SkipExceptions.</summary>
        public bool IsSkipException(Exception exception) =>
            Fact.SkipExceptions is { } types && Array.IndexOf(types, exception.GetType()) >= 0;

        private static string FormatArgument(object? argument) => argument switch
        {
            null => "null",
            string text => $"\"{text}\"",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => argument.ToString() ?? string.Empty
        };
    }
}
