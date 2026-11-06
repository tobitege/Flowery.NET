using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Custom;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// DaisyGlassSurface on the input controls, and the glass mode that turns it on for all of them.
/// </summary>
public class DaisyGlassInputControlsTests
{
    public static TheoryData<string> InputControls =>
    [
        nameof(DaisyInput), nameof(DaisySelect)
    ];

    [AvaloniaTheory]
    [MemberData(nameof(InputControls))]
    public void An_Input_Control_Gets_A_Blurring_Glass_Under_Its_Field(string name)
    {
        var control = Create(name);
        DaisyGlassSurface.SetIsEnabled(control, true);
        var window = Show(control);
        try
        {
            var glass = Assert.Single(control.GetVisualDescendants().OfType<DaisyGlass>());
            AssertUnderField(glass);
            Assert.True(glass.EnableBackdropBlur);
            Assert.Equal(GlassBlurMode.SkiaSharp, glass.BlurMode);

            DaisyGlassSurface.SetIsEnabled(control, false);
            Assert.Empty(control.GetVisualDescendants().OfType<DaisyGlass>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Glass_Mode_Puts_The_Input_Controls_On_Glass_Unless_A_Control_Says_Otherwise()
    {
        var input = new DaisyInput();
        var select = new DaisySelect();
        var opted = new DaisyInput();
        DaisyGlassSurface.SetIsEnabled(opted, false);
        var button = new DaisyButton { Content = "OK" };
        var otp = new DaisyOtpInput { Length = 2 };
        var window = Show(new StackPanel { Children = { input, select, opted, button, otp } });
        try
        {
            DaisyGlassSurface.GlobalEnabled = true;
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(DaisyGlassSurface.GetGlass(input));
            Assert.NotNull(DaisyGlassSurface.GetGlass(select));
            Assert.Null(DaisyGlassSurface.GetGlass(opted));
            Assert.Empty(button.GetVisualDescendants().OfType<DaisyGlass>());
            Assert.Equal(2, otp.GetVisualDescendants().OfType<DaisyGlass>().Count());

            var glass = DaisyGlassSurface.GetGlass(input);
            DaisyGlassSurface.GlobalBlurMode = GlassBlurMode.BitmapCapture;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(glass, DaisyGlassSurface.GetGlass(input));
            Assert.Equal(GlassBlurMode.BitmapCapture, DaisyGlassSurface.GetGlass(input)!.BlurMode);
            // A blur mode set on a control wins over the one of the glass mode.
            DaisyGlassSurface.SetBlurMode(select, GlassBlurMode.Simulated);
            Assert.Equal(GlassBlurMode.Simulated, DaisyGlassSurface.GetGlass(select)!.BlurMode);

            DaisyGlassSurface.GlobalEnabled = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Null(DaisyGlassSurface.GetGlass(input));
            Assert.Empty(otp.GetVisualDescendants().OfType<DaisyGlass>());
        }
        finally
        {
            DaisyGlassSurface.GlobalEnabled = false;
            DaisyGlassSurface.GlobalBlurMode = GlassBlurMode.SkiaSharp;
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Glass_Mode_Comes_Back_When_Its_Styles_Left_The_App()
    {
        // As when the mode was switched on before the app existed: the next theme change adds the styles.
        var previous = DaisyThemeManager.CurrentThemeName;
        var app = Application.Current!;
        var input = new DaisyInput();
        var window = Show(input);
        try
        {
            DaisyGlassSurface.GlobalEnabled = true;
            var mode = app.Styles.OfType<Avalonia.Styling.Styles>().Last();
            app.Styles.Remove(mode);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(DaisyGlassSurface.GetGlass(input));

            Assert.True(DaisyThemeManager.ApplyTheme(previous == "Dracula" ? "Light" : "Dracula"));
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(DaisyGlassSurface.GetGlass(input));
        }
        finally
        {
            DaisyGlassSurface.GlobalEnabled = false;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
            window.Close();
        }
    }

    // The glass lies right under the field, in the same panel, and the field is clear.
    private static void AssertUnderField(DaisyGlass glass)
    {
        var panel = Assert.IsAssignableFrom<Panel>(glass.GetVisualParent());
        var field = panel.Children[panel.Children.IndexOf(glass) + 1];
        Assert.Contains("glass-surface", glass.Classes);
        Assert.Equal(field.Bounds, glass.Bounds);
        Assert.Same(Brushes.Transparent, (field as Border)?.Background);
    }

    private static Control Create(string name) => name switch
    {
        nameof(DaisyInput) => new DaisyInput(),
        nameof(DaisyTextArea) => new DaisyTextArea(),
        nameof(DaisyPasswordBox) => new DaisyPasswordBox(),
        nameof(DaisyMaskInput) => new DaisyMaskInput(),
        nameof(DaisyNumericUpDown) => new DaisyNumericUpDown(),
        nameof(DaisySelect) => new DaisySelect(),
        nameof(DaisyCalendarDatePicker) => new DaisyCalendarDatePicker(),
        nameof(DaisyFileInput) => new DaisyFileInput(),
        nameof(DaisyTagPicker) => new DaisyTagPicker(),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 400, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }
}
