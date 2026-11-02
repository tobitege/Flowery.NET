using Xunit;

namespace Flowery.NET.Kiosk.Tests;

public class KioskOptionsTests
{
    private static readonly string ReportPath = Path.Combine(Path.GetTempPath(), "kiosk", "report.xml");

    [Fact]
    public void Without_Options_The_Kiosk_Plays_Automatically()
    {
        Assert.Equal(new KioskOptions(false, null), KioskOptions.Parse([]));
    }

    [Fact]
    public void Manual_Turns_Autoplay_Off()
    {
        Assert.Equal(new KioskOptions(true, null), KioskOptions.Parse(["--manual"]));
    }

    [Fact]
    public void A_Smoke_Test_Takes_An_Absolute_Report_Path_And_Runs_Manually()
    {
        var options = KioskOptions.Parse(["--smoke-test", ReportPath]);
        Assert.True(options.Manual);
        Assert.Equal(Path.GetFullPath(ReportPath), options.SmokeReport);
    }

    [Theory]
    [InlineData("--smoke-test")]
    [InlineData("--smoke-test", "report.xml")]
    [InlineData("--smoke-test", "--manual")]
    [InlineData("--fullscreen")]
    [InlineData("--manual", "extra")]
    public void Missing_Relative_And_Unknown_Arguments_Are_Rejected(params string[] arguments)
    {
        Assert.Throws<ArgumentException>(() => KioskOptions.Parse(arguments));
    }
}
