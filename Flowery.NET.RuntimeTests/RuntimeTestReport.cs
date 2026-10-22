using System.Xml.Linq;

namespace Flowery.NET.RuntimeTests;

/// <summary>
/// How a runtime test ended. A timed-out test stops the run; the tests after it are not run.
/// </summary>
internal enum RuntimeTestOutcome { Passed, Failed, Inconclusive, TimedOut, NotRun }

internal sealed record RuntimeTestResult(string Name, RuntimeTestOutcome Outcome, string? Message);

/// <summary>
/// Writes the XML report that the runner project and scripts/test_runtime.ps1 read.
/// </summary>
internal static class RuntimeTestReport
{
    public static void Write(string reportPath, IReadOnlyCollection<RuntimeTestResult> results)
    {
        var document = new XDocument(
            new XElement("tests",
                new XAttribute("total", results.Count),
                new XAttribute("passed", Count(results, RuntimeTestOutcome.Passed)),
                new XAttribute("failed", Count(results, RuntimeTestOutcome.Failed)),
                new XAttribute("inconclusive", Count(results, RuntimeTestOutcome.Inconclusive)),
                new XAttribute("timedOut", Count(results, RuntimeTestOutcome.TimedOut)),
                new XAttribute("notRun", Count(results, RuntimeTestOutcome.NotRun)),
                results.Select(result =>
                    new XElement("test",
                        new XAttribute("name", result.Name),
                        new XAttribute("outcome", result.Outcome.ToString()),
                        result.Message is null ? null : new XElement("message", result.Message)))));

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        document.Save(reportPath);
    }

    /// <summary>Whether the run failed: a test failed or timed out, or a test was not run.</summary>
    public static bool HasFailures(IEnumerable<RuntimeTestResult> results)
    {
        foreach (var result in results)
        {
            if (result.Outcome is RuntimeTestOutcome.Failed or RuntimeTestOutcome.TimedOut or RuntimeTestOutcome.NotRun)
                return true;
        }

        return false;
    }

    public static void WriteStartupFailure(string reportPath, Exception exception)
    {
        try
        {
            Write(reportPath, [new RuntimeTestResult("RuntimeTests.Startup", RuntimeTestOutcome.Failed, exception.ToString())]);
        }
        catch (Exception writeError)
        {
            // The exit code still reports the failure.
            Console.Error.WriteLine($"Could not write the runtime test report: {writeError.Message}");
        }

        Console.Error.WriteLine(exception);
    }

    private static int Count(IEnumerable<RuntimeTestResult> results, RuntimeTestOutcome outcome)
    {
        var count = 0;
        foreach (var result in results)
        {
            if (result.Outcome == outcome)
                count++;
        }

        return count;
    }
}
