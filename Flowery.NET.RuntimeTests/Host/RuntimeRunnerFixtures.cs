using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;

namespace Flowery.NET.RuntimeTests.Host;

/// <summary>
/// Sample tests for <see cref="Given_RuntimeTestRunner"/>. They are explicit, so a normal run skips them;
/// many of them fail on purpose.
/// </summary>
public class RuntimeRunnerFixtures
{
    public static bool ConditionMet => true;
    public static bool ConditionNotMet => false;

    [Fact(Explicit = true)]
    public void Passes()
    {
    }

    [Fact(Explicit = true)]
    public void Skips_At_Runtime() => Assert.Skip("no room for the scenario");

    [Fact(Explicit = true, Skip = "static reason")]
    public void Skips_Statically()
    {
    }

    [Fact(Explicit = true, Skip = "condition not met", SkipUnless = nameof(ConditionNotMet))]
    public void Skips_Unless_Condition()
    {
    }

    [Fact(Explicit = true, Skip = "condition met", SkipUnless = nameof(ConditionMet))]
    public void Runs_When_Condition_Holds()
    {
    }

    [Fact(Explicit = true, Skip = "condition met", SkipWhen = nameof(ConditionMet))]
    public void Skips_When_Condition()
    {
    }

    [Fact(Explicit = true, SkipExceptions = [typeof(PlatformNotSupportedException)])]
    public void Skips_On_Listed_Exception() => throw new PlatformNotSupportedException("not on this platform");

    [Theory(Explicit = true)]
    [InlineData(1, "a")]
    [InlineData(2.5, null)]
    public void Expands_Rows(double value, string? text) => Assert.True(value > 0, text);

    [Theory(Explicit = true)]
    public void Has_No_Rows(int value) => Assert.True(value > 0);

    [Fact(Explicit = true)]
    public async void Is_Async_Void() => await Task.Yield();

    [Fact(Explicit = true)]
    public Task Posts_An_Exception_And_Returns()
    {
        // The exception reaches the dispatcher only after the test task has completed.
        Dispatcher.UIThread.Post(static () => throw new InvalidOperationException("posted after the test"));
        return Task.CompletedTask;
    }

    [Fact(Explicit = true)]
    public async Task Leaves_A_Control_That_Throws_On_Detach() =>
        await RuntimeTestHelpers.AttachAsync(new ThrowsOnDetach());

    [Fact(Explicit = true, Timeout = 100)]
    public Task Times_Out() => Task.Delay(TimeSpan.FromSeconds(1));

    private sealed class ThrowsOnDetach : Border
    {
        private bool _thrown;

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            if (_thrown)
                return;

            _thrown = true;
            throw new InvalidOperationException("detached");
        }
    }
}
