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
        var names = Discover().Where(IsExpandsRows).Select(testCase => testCase.Name).ToArray();

        Assert.Equal([$"{FixtureName}.Expands_Rows(1, \"a\")", $"{FixtureName}.Expands_Rows(2.5, null)"], names);
    }

    [Fact]
    public void When_Selecting_Explicit_Tests_Only_A_Filter_That_Names_Them_Selects_Them()
    {
        var fixtures = Discover().ToList();

        Assert.Empty(RuntimeTestRunner.Select(fixtures, null));
        Assert.Empty(RuntimeTestRunner.Select(fixtures, "RuntimeRunnerFixtures"));
        Assert.Single(RuntimeTestRunner.Select(fixtures, $"{FixtureName}.Passes"));
        Assert.Equal(2, RuntimeTestRunner.Select(fixtures, $"{FixtureName}.Expands_Rows").Count);
    }

    [Theory]
    [InlineData("Passes", "Passed", null)]
    [InlineData("Skips_At_Runtime", "Inconclusive", "no room for the scenario")]
    [InlineData("Skips_Statically", "Inconclusive", "Skipped: static reason")]
    [InlineData("Skips_Unless_Condition", "Inconclusive", "Skipped: condition not met")]
    [InlineData("Runs_When_Condition_Holds", "Passed", null)]
    [InlineData("Skips_When_Condition", "Inconclusive", "Skipped: condition met")]
    [InlineData("Skips_On_Listed_Exception", "Inconclusive", "not on this platform")]
    [InlineData("Has_No_Rows", "Failed", "need [InlineData] rows")]
    [InlineData("Is_Async_Void", "Failed", "Async void test methods are not supported")]
    [InlineData("Posts_An_Exception_And_Returns", "Failed", "posted after the test")]
    [InlineData("Leaves_A_Control_That_Throws_On_Detach", "Failed", "detached")]
    public async Task When_Executing_A_Sample_The_Outcome_And_Message_Match(string method, string outcome, string? message)
    {
        var (result, timedOut) = await RuntimeTestRunner.ExecuteAsync(FindSingle(method), RuntimeTestHelpers.GetHostPanel());

        Assert.False(timedOut);
        Assert.Equal(outcome, result.Outcome.ToString());
        if (message is null)
            Assert.Null(result.Message);
        else
            Assert.Contains(message, result.Message);
    }

    [Fact]
    public async Task When_A_Test_Times_Out_The_Run_Stops_And_Reports_The_Rest_As_Not_Run()
    {
        var results = await RuntimeTestRunner.RunCasesAsync(
            [FindSingle("Times_Out"), FindSingle("Passes")], RuntimeTestHelpers.GetHostPanel(), TextWriter.Null);
        // The timed-out sample keeps running; let it end before later tests start.
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        Assert.Equal("Failed", results[0].Outcome.ToString());
        Assert.Contains("did not finish within 0.1 seconds", results[0].Message);
        Assert.Equal("Failed", results[1].Outcome.ToString());
        Assert.Contains($"Not run: {FixtureName}.Times_Out timed out", results[1].Message);
    }

    private static IEnumerable<RuntimeTestRunner.RuntimeTestCase> Discover() =>
        RuntimeTestRunner.Discover([typeof(RuntimeRunnerFixtures)]);

    private static bool IsExpandsRows(RuntimeTestRunner.RuntimeTestCase testCase) =>
        testCase.Method.Name == nameof(RuntimeRunnerFixtures.Expands_Rows);

    private static RuntimeTestRunner.RuntimeTestCase FindSingle(string method)
    {
        RuntimeTestRunner.RuntimeTestCase? found = null;
        foreach (var testCase in Discover())
        {
            if (testCase.Method.Name != method)
                continue;
            Assert.Null(found);
            found = testCase;
        }

        Assert.NotNull(found);
        return found;
    }
}
