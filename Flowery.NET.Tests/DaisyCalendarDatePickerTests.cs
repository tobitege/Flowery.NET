using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyCalendarDatePickerTests
{
    [AvaloniaFact]
    public void Date_And_SelectedDate_Stay_In_Sync()
    {
        var picker = new DaisyCalendarDatePicker();

        picker.Date = new DateTimeOffset(new DateTime(2026, 10, 16, 15, 30, 0));
        Assert.Equal(new DateTime(2026, 10, 16), picker.SelectedDate);

        picker.SelectedDate = new DateTime(2026, 12, 24);
        Assert.Equal(new DateTime(2026, 12, 24), picker.Date?.Date);

        picker.Date = null;
        Assert.Null(picker.SelectedDate);
    }

    [AvaloniaFact]
    public void DateChanged_Reports_Old_And_New_Date()
    {
        var picker = new DaisyCalendarDatePicker { SelectedDate = new DateTime(2026, 1, 1) };
        var changes = new List<DaisyCalendarDatePickerDateChangedEventArgs>();
        picker.DateChanged += (_, e) => changes.Add(e);

        picker.Date = new DateTimeOffset(new DateTime(2026, 2, 3));

        var change = Assert.Single(changes);
        Assert.Equal(new DateTime(2026, 1, 1), change.OldDate?.Date);
        Assert.Equal(new DateTime(2026, 2, 3), change.NewDate?.Date);
    }

    [AvaloniaFact]
    public void Variant_And_Size_Apply_Daisy_Tokens()
    {
        var picker = new DaisyCalendarDatePicker { Variant = DaisyInputVariant.Primary, Size = DaisySize.Large };
        FlowerySizeManager.SetIgnoreGlobalSize(picker, true);
        var window = new Window { Width = 400, Height = 200, Content = picker };
        try
        {
            window.Show();
            Layout(window);

            Assert.Same(FindResource<IBrush>(picker, "DaisyPrimaryBrush"), picker.BorderBrush);
            Assert.Equal(FindResource<double>(picker, "DaisySizeLargeHeight"), picker.MinHeight);
            Assert.Equal(FindResource<double>(picker, "DaisySizeLargeFontSize"), picker.FontSize);

            picker.Variant = DaisyInputVariant.Ghost;
            Layout(window);

            Assert.Equal(default, picker.BorderThickness);
            var background = picker.GetVisualDescendants().OfType<Border>().First(border => border.Name == "Background");
            Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(background.Background).Color);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void BorderRingBrush_Sets_Ring_PseudoClass()
    {
        var picker = new DaisyCalendarDatePicker();
        Assert.DoesNotContain(":ring", picker.Classes);

        picker.BorderRingBrush = Brushes.Orange;
        Assert.Contains(":ring", picker.Classes);

        picker.BorderRingBrush = null;
        Assert.DoesNotContain(":ring", picker.Classes);
    }

    private static T FindResource<T>(Control control, string key)
    {
        Assert.True(control.TryFindResource(key, control.ActualThemeVariant, out var value), key);
        return Assert.IsAssignableFrom<T>(value);
    }

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
