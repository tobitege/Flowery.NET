using System.Xml.Linq;
using Xunit;

namespace Flowery.NET.Kiosk.Tests;

public class KioskSmokeReportTests
{
    [Fact]
    public void The_Report_Counts_Each_Outcome_And_Keeps_Repeated_Names_Apart()
    {
        var folder = Path.Combine(Path.GetTempPath(), "flowery-kiosk-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var report = new KioskSmokeReport(Path.Combine(folder, "nested", "kiosk-smoke.xml"));
            report.Add("Slide1", true, "shown");
            report.Add("Slide1", false, "failed once");
            report.Add("Scaling", SmokeOutcome.Inconclusive, "small screen");
            report.Save();

            var root = XDocument.Load(report.Path).Root!;
            Assert.Equal("3", (string?)root.Attribute("total"));
            Assert.Equal("1", (string?)root.Attribute("passed"));
            Assert.Equal("1", (string?)root.Attribute("failed"));
            Assert.Equal("1", (string?)root.Attribute("inconclusive"));
            Assert.Equal(["Slide1", "Slide1.2", "Scaling"], root.Elements("test").Select(test => (string?)test.Attribute("name")));
            Assert.Equal(["Passed", "Failed", "Inconclusive"], root.Elements("test").Select(test => (string?)test.Attribute("outcome")));
            Assert.Equal(3, File.ReadAllLines(report.Path + ".log").Length);
            Assert.False(File.Exists(report.Path + ".writing"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
