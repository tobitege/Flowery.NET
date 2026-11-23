using System.Reflection;
using Avalonia;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed class ReleaseNotesTests
{
    [Fact]
    public void TheCurrentReleaseNotesMatchTheAvaloniaVersionLoadedByPhotos()
    {
        var version = typeof(AvaloniaObject).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var changelog = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CHANGELOG.md");
        var current = File.ReadLines(changelog).SkipWhile(line => !line.StartsWith("## [", StringComparison.Ordinal))
            .Skip(1).TakeWhile(line => !line.StartsWith("## [", StringComparison.Ordinal));
        var dependency = Assert.Single(current, line => line.StartsWith("- Updated all Avalonia packages", StringComparison.Ordinal));
        Assert.StartsWith($"- Updated all Avalonia packages to {version} ", dependency);
    }
}
