using System.Text;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace Flowery.NET.RuntimeTests.Controls;

/// <summary>
/// Every public control is constructed, attached to the window, laid out and rendered without exceptions.
/// </summary>
public class Given_AllControls
{
    [Fact]
    public Task When_Rendering_DaisyControls_NoExceptions() => RenderAllAsync("Daisy", ControlCatalog.GetDaisyControls());

    [Fact]
    public Task When_Rendering_ColorPickerControls_NoExceptions() => RenderAllAsync("ColorPicker", ControlCatalog.GetColorPickerControls());

    [Fact]
    public Task When_Rendering_CustomControls_NoExceptions() => RenderAllAsync("Custom", ControlCatalog.GetCustomControls());

    [Fact]
    public Task When_Rendering_KanbanControls_NoExceptions() => RenderAllAsync("Kanban", ControlCatalog.GetKanbanControls());

    private static async Task RenderAllAsync(string area, IReadOnlyList<Type> controlTypes)
    {
        Assert.NotEmpty(controlTypes);
        var failures = new StringBuilder();
        foreach (var type in controlTypes)
        {
            try
            {
                if (type.GetConstructor(Type.EmptyTypes) is null)
                    throw new InvalidOperationException("No public parameterless constructor.");

                var control = (Control)Activator.CreateInstance(type)!;
                if (control is Window window)
                {
                    // A window cannot be hosted in a panel; construction is the check.
                    window.Close();
                    continue;
                }

                // Dialogs that build their own chrome in the constructor are attached through that chrome.
                var root = control;
                while (root.GetVisualParent() is Control parent)
                    root = parent;

                await RuntimeTestHelpers.AttachAsync(root);
                using var _ = RuntimeTestHelpers.Render(root);
            }
            catch (Exception exception)
            {
                var cause = exception is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : exception;
                failures.AppendLine($"[{area}] {type.FullName}: {cause.GetType().Name} - {cause.Message}");
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
            }
        }

        Assert.True(failures.Length == 0, failures.ToString());
    }
}
