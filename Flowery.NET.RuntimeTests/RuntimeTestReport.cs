using System.Xml.Linq;

namespace Flowery.NET.RuntimeTests;

internal enum RuntimeTestOutcome { Passed, Failed, Inconclusive }

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
                new XAttribute("passed", results.Count(result => result.Outcome == RuntimeTestOutcome.Passed)),
                new XAttribute("failed", results.Count(result => result.Outcome == RuntimeTestOutcome.Failed)),
                new XAttribute("inconclusive", results.Count(result => result.Outcome == RuntimeTestOutcome.Inconclusive)),
                results.Select(result =>
                    new XElement("test",
                        new XAttribute("name", result.Name),
                        new XAttribute("outcome", result.Outcome.ToString()),
                        result.Message is null ? null : new XElement("message", result.Message)))));

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        document.Save(reportPath);
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
}
