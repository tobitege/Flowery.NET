using System.Diagnostics;
using System.Xml.Linq;
using Xunit;

namespace Flowery.NET.RuntimeTests.Runner;

/// <summary>
/// Starts the runtime test host in its own process and checks its report. Run it through scripts/test_runtime.ps1.
/// </summary>
public class Given_RuntimeTestRun(ITestOutputHelper output)
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(8);

    [Fact]
    public async Task When_The_Runtime_Tests_Run_In_The_Gallery_Window_None_Fail()
    {
        var executable = Environment.GetEnvironmentVariable("FLOWERY_RUNTIME_TESTS_EXE_PATH");
        Assert.True(!string.IsNullOrWhiteSpace(executable) && File.Exists(executable),
            "Set FLOWERY_RUNTIME_TESTS_EXE_PATH to the built Flowery.NET.RuntimeTests executable. Use scripts/test_runtime.ps1.");
        var resultsRoot = Environment.GetEnvironmentVariable("FLOWERY_TEST_RESULTS_ROOT");
        Assert.True(!string.IsNullOrWhiteSpace(resultsRoot) && Path.IsPathFullyQualified(resultsRoot) && Directory.Exists(resultsRoot),
            "Set FLOWERY_TEST_RESULTS_ROOT to an existing absolute report directory. Use scripts/test_runtime.ps1.");

        var reportPath = Path.Combine(resultsRoot, "runtime-tests.xml");
        // A report left from an earlier run in the same directory must not pass for this run's report.
        File.Delete(reportPath);
        File.Delete(Path.ChangeExtension(reportPath, ".progress.log"));
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(executable),
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--runtime-tests");
        startInfo.ArgumentList.Add(reportPath);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(RunTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // Only terminate the process tree started above, then wait for its handles to close.
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            Assert.Fail($"The runtime tests did not finish within {RunTimeout.TotalMinutes} minutes. See {Path.ChangeExtension(reportPath, ".progress.log")}.");
        }
        finally
        {
            WriteIfPresent(await standardOutput);
            WriteIfPresent(await standardError);
            output.WriteLine($"Runtime report: {reportPath}");
        }

        Assert.True(File.Exists(reportPath), $"The runtime tests produced no report. Exit code: {process.ExitCode}.");
        var root = XDocument.Load(reportPath).Root ?? throw new InvalidOperationException("The runtime report has no root element.");
        var tests = root.Elements("test").ToList();
        var total = (int?)root.Attribute("total") ?? -1;

        foreach (var test in tests.Where(test => (string?)test.Attribute("outcome") != "Passed"))
            output.WriteLine($"{test.Attribute("outcome")?.Value} {test.Attribute("name")?.Value}: {test.Element("message")?.Value}");

        Assert.True(total > 0, "The runtime report contains no tests.");
        Assert.Equal(total, tests.Count);
        Assert.Equal(total, tests.Select(test => (string?)test.Attribute("name")).Distinct().Count());
        var counted = 0;
        foreach (var (attribute, outcome) in Outcomes)
        {
            var count = (int?)root.Attribute(attribute) ?? -1;
            Assert.Equal(count, CountOutcome(tests, outcome));
            counted += count;
        }

        Assert.Equal(total, counted);
        // Inconclusive tests could not set up their scenario here; scripts/test_runtime.ps1 reports them as warnings.
        var unsuccessful = total - CountOutcome(tests, "Passed") - CountOutcome(tests, "Inconclusive");
        Assert.True(unsuccessful == 0, $"{unsuccessful} runtime tests failed, timed out or did not run. See {reportPath}.");
        Assert.True(process.ExitCode == 0, $"The runtime test host returned exit code {process.ExitCode}.");
    }

    // The report's count attributes and the outcomes they count.
    private static readonly (string Attribute, string Outcome)[] Outcomes =
    [
        ("passed", "Passed"),
        ("failed", "Failed"),
        ("inconclusive", "Inconclusive"),
        ("timedOut", "TimedOut"),
        ("notRun", "NotRun")
    ];

    private static int CountOutcome(IEnumerable<XElement> tests, string outcome)
    {
        var count = 0;
        foreach (var test in tests)
        {
            if ((string?)test.Attribute("outcome") == outcome)
                count++;
        }

        return count;
    }

    private void WriteIfPresent(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            output.WriteLine(text.TrimEnd());
    }
}
