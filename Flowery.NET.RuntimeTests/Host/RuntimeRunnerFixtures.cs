using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.RuntimeTests.Host;

/// <summary>
/// Sample tests for <see cref="Given_RuntimeTestRunner"/>. They are explicit, so a normal run skips them;
/// many of them fail on purpose. The one row that opts out of explicit passes and runs in every run.
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

    [Fact(Explicit = true, Skip = "condition of another type", SkipWhen = nameof(SkipConditions.Always), SkipType = typeof(SkipConditions))]
    public void Skips_When_Condition_Of_Another_Type()
    {
    }

    [Fact(Explicit = true, SkipUnless = nameof(ConditionMet))]
    public void Sets_SkipUnless_Without_Skip()
    {
    }

    [Fact(Explicit = true, SkipExceptions = [typeof(PlatformNotSupportedException)])]
    public void Skips_On_Listed_Exception() => throw new PlatformNotSupportedException("not on this platform");

    [Fact(Explicit = true, SkipExceptions = [typeof(InvalidOperationException)])]
    public void Fails_On_Derived_Exception() => throw new ObjectDisposedException("derived from a listed type");

    [Theory(Explicit = true)]
    [InlineData(1, "a")]
    [InlineData(2.5, null)]
    public void Expands_Rows(double value, string? text) => Assert.True(value > 0, text);

    [Theory(Explicit = true, Timeout = 5000)]
    [InlineData(1)]
    [InlineData(2, Skip = "row skip")]
    [InlineData(3, Timeout = 100)]
    [InlineData(4, Explicit = false)]
    public async Task Uses_Row_Settings(int value)
    {
        if (value == 3)
            await Task.Delay(TimeSpan.FromSeconds(1));
    }

    [Theory(Explicit = true)]
    public void Has_No_Rows(int value) => Assert.True(value > 0);

    [Fact(Explicit = true)]
    public async void Is_Async_Void() => await Task.Yield();

    [Fact(Explicit = true)]
    public ValueTask<int> Returns_A_Result() => ValueTask.FromResult(1);

    [Fact(Explicit = true)]
    public Task Posts_An_Exception_And_Returns()
    {
        // The exception reaches the dispatcher only after the test task has completed.
        Dispatcher.UIThread.Post(static () => throw new InvalidOperationException("posted after the test"));
        return Task.CompletedTask;
    }

    [Fact(Explicit = true)]
    public async Task Changes_The_Theme_And_Leaves_A_Control_That_Throws_On_Detach()
    {
        var otherTheme = DaisyThemeManager.AvailableThemes
            .First(static theme => !string.Equals(theme.Name, RuntimeTestRunner.BaseTheme, StringComparison.OrdinalIgnoreCase));
        Assert.True(DaisyThemeManager.ApplyTheme(otherTheme.Name));
        await RuntimeTestHelpers.AttachAsync(new ThrowsOnDetach());
    }

    [Fact(Explicit = true, Timeout = 100)]
    public Task Times_Out() => Task.Delay(TimeSpan.FromSeconds(1));

    public static class SkipConditions
    {
        public static bool Always => true;
    }

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

/// <summary>Disposable samples for <see cref="Given_RuntimeTestRunner"/>; explicit like <see cref="RuntimeRunnerFixtures"/>.</summary>
public sealed class RuntimeRunnerDisposalFixtures : IDisposable
{
    private bool _failDispose;

    public static int DisposeCount { get; private set; }

    [Fact(Explicit = true)]
    public void Passes_And_Is_Disposed()
    {
    }

    [Fact(Explicit = true)]
    public void Fails_And_Fails_To_Dispose()
    {
        _failDispose = true;
        throw new InvalidOperationException("the test failed");
    }

    public void Dispose()
    {
        DisposeCount++;
        if (_failDispose)
            throw new InvalidOperationException("the disposal failed");
    }
}
