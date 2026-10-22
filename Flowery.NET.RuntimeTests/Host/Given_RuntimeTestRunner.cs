using Flowery.Controls;
using Xunit;

namespace Flowery.NET.RuntimeTests.Host;

/// <summary>
/// Runs the samples in <see cref="RuntimeRunnerFixtures"/> through the runner's discovery, selection and execution.
/// </summary>
public class Given_RuntimeTestRunner
{
    private const string FixtureName = "Flowery.NET.RuntimeTests.Host.RuntimeRunnerFixtures";

    [Fact]
    public void When_Discovering_A_Theory_Each_Row_Is_A_Named_Test()
    {
        var names = Find(nameof(RuntimeRunnerFixtures.Expands_Rows)).Select(testCase => testCase.Name).ToArray();

        Assert.Equal([$"{FixtureName}.Expands_Rows(1, \"a\")", $"{FixtureName}.Expands_Rows(2.5, null)"], names);
    }

    [Fact]
    public void When_Selecting_Explicit_Tests_Only_A_Filter_That_Names_Them_Selects_Them()
    {
        var fixtures = Discover().ToList();

        // Only the row that opts out of explicit runs without a filter.
        Assert.Equal([$"{FixtureName}.Uses_Row_Settings(4)"], RuntimeTestRunner.Select(fixtures, null).Select(testCase => testCase.Name));
        Assert.Single(RuntimeTestRunner.Select(fixtures, "RuntimeRunnerFixtures"));
        Assert.Single(RuntimeTestRunner.Select(fixtures, $"{FixtureName}.Passes"));
        Assert.Equal(2, RuntimeTestRunner.Select(fixtures, $"{FixtureName}.Expands_Rows").Count);
    }

    [Theory]
    [InlineData("Passes", "Passed", null)]
    [InlineData("Skips_At_Runtime", "Inconclusive", "Skipped: no room for the scenario")]
    [InlineData("Skips_Statically", "Inconclusive", "Skipped: static reason")]
    [InlineData("Skips_Unless_Condition", "Inconclusive", "Skipped: condition not met")]
    [InlineData("Runs_When_Condition_Holds", "Passed", null)]
    [InlineData("Skips_When_Condition", "Inconclusive", "Skipped: condition met")]
    [InlineData("Skips_When_Condition_Of_Another_Type", "Inconclusive", "Skipped: condition of another type")]
    [InlineData("Skips_On_Listed_Exception", "Inconclusive", "Skipped: not on this platform")]
    public async Task When_A_Sample_Passes_Or_Is_Skipped_The_Outcome_And_Message_Match(string method, string outcome, string? message)
    {
        var result = await ExecuteIsolatedAsync(FindSingle(method));

        Assert.Equal(outcome, result.Outcome.ToString());
        Assert.Equal(message, result.Message);
    }

    [Theory]
    [InlineData("Sets_SkipUnless_Without_Skip", "You must set Skip when you set SkipUnless or SkipWhen.")]
    [InlineData("Fails_On_Derived_Exception", "derived from a listed type")]
    [InlineData("Has_No_Rows", "need [InlineData] rows")]
    [InlineData("Is_Async_Void", "Async void test methods are not supported")]
    [InlineData("Returns_A_Result", "must return void, Task or ValueTask")]
    [InlineData("Posts_An_Exception_And_Returns", "posted after the test")]
    [InlineData("Changes_The_Theme_And_Leaves_A_Control_That_Throws_On_Detach", "detached")]
    public async Task When_A_Sample_Fails_The_Message_Names_The_Cause(string method, string message)
    {
        var result = await ExecuteIsolatedAsync(FindSingle(method));

        Assert.Equal("Failed", result.Outcome.ToString());
        Assert.Contains(message, result.Message);
    }

    [Fact]
    public async Task When_A_Row_Sets_Its_Own_Settings_They_Replace_The_Theory_Settings()
    {
        var rows = Find(nameof(RuntimeRunnerFixtures.Uses_Row_Settings)).ToArray();

        Assert.Equal([true, true, true, false], rows.Select(row => row.IsExplicit));
        Assert.Equal(TimeSpan.FromSeconds(5), rows[0].Timeout);
        Assert.Equal(TimeSpan.FromMilliseconds(100), rows[2].Timeout);
        Assert.Equal("Passed", (await ExecuteIsolatedAsync(rows[0])).Outcome.ToString());
        Assert.Equal("Skipped: row skip", (await ExecuteIsolatedAsync(rows[1])).Message);
        Assert.Equal("TimedOut", (await RuntimeTestRunner.ExecuteAsync(rows[2], RuntimeTestHelpers.GetHostPanel())).Outcome.ToString());
        // The timed-out row keeps running; let it end before later tests start.
        await Task.Delay(TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public async Task When_A_Test_Class_Is_Disposable_The_Runner_Disposes_It_And_Reports_Both_Failures()
    {
        var before = RuntimeRunnerDisposalFixtures.DisposeCount;

        var passed = await ExecuteIsolatedAsync(FindSingle(nameof(RuntimeRunnerDisposalFixtures.Passes_And_Is_Disposed)));
        var failed = await ExecuteIsolatedAsync(FindSingle(nameof(RuntimeRunnerDisposalFixtures.Fails_And_Fails_To_Dispose)));

        Assert.Equal("Passed", passed.Outcome.ToString());
        Assert.Equal("Failed", failed.Outcome.ToString());
        Assert.Contains("the test failed", failed.Message);
        Assert.Contains("the disposal failed", failed.Message);
        Assert.Equal(before + 2, RuntimeRunnerDisposalFixtures.DisposeCount);
    }

    [Fact]
    public async Task When_A_Test_Times_Out_The_Run_Stops_And_Reports_The_Rest_As_Not_Run()
    {
        var results = await RuntimeTestRunner.RunCasesAsync(
            [FindSingle(nameof(RuntimeRunnerFixtures.Times_Out)), FindSingle(nameof(RuntimeRunnerFixtures.Passes))],
            RuntimeTestHelpers.GetHostPanel(), TextWriter.Null);
        // The timed-out sample keeps running; let it end before later tests start.
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        Assert.Equal("TimedOut", results[0].Outcome.ToString());
        Assert.Equal("The test did not finish within 0.1 seconds.", results[0].Message);
        Assert.Equal("NotRun", results[1].Outcome.ToString());
        Assert.Equal($"{FixtureName}.Times_Out timed out and may still be running on the UI thread.", results[1].Message);
    }

    /// <summary>
    /// Runs a sample inside this test. Nothing of this test may be pending, so the sample is blamed only for its
    /// own exceptions, and the sample's own teardown must restore the global state.
    /// </summary>
    private static async Task<RuntimeTestResult> ExecuteIsolatedAsync(RuntimeTestRunner.RuntimeTestCase testCase)
    {
        Assert.Equal(0, RuntimeTestRunner.PendingUnhandledExceptions);

        var result = await RuntimeTestRunner.ExecuteAsync(testCase, RuntimeTestHelpers.GetHostPanel());

        Assert.Equal(0, RuntimeTestRunner.PendingUnhandledExceptions);
        Assert.Equal(RuntimeTestRunner.BaseTheme, DaisyThemeManager.CurrentThemeName);
        Assert.Empty(RuntimeTestHelpers.GetHostPanel().Children);
        return result;
    }

    private static IEnumerable<RuntimeTestRunner.RuntimeTestCase> Discover() =>
        RuntimeTestRunner.Discover([typeof(RuntimeRunnerFixtures), typeof(RuntimeRunnerDisposalFixtures)]);

    private static List<RuntimeTestRunner.RuntimeTestCase> Find(string method)
    {
        var found = new List<RuntimeTestRunner.RuntimeTestCase>();
        foreach (var testCase in Discover())
        {
            if (testCase.Method.Name == method)
                found.Add(testCase);
        }

        return found;
    }

    private static RuntimeTestRunner.RuntimeTestCase FindSingle(string method) => Assert.Single(Find(method));
}
