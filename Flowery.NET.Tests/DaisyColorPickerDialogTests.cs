using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls.ColorPicker;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyColorPickerDialogTests
    {
        [AvaloniaFact]
        public void Editor_Slider_Change_Should_Update_Dialog_Color()
        {
            var dialog = new DaisyColorPickerDialog
            {
                Color = Colors.Red,
                OriginalColor = Colors.Red
            };
            dialog.Show();

            try
            {
                var editor = Assert.Single(dialog.GetVisualDescendants().OfType<DaisyColorEditor>());
                var redSlider = Assert.Single(
                    editor.GetVisualDescendants().OfType<DaisyColorSlider>(),
                    slider => slider.Name == "PART_RedSlider");
                var expectedColor = Color.FromArgb(255, 71, 0, 0);
                Color? previewColor = null;
                var changeCount = 0;
                dialog.PreviewColorChanged += (_, e) =>
                {
                    previewColor = e.Color;
                    changeCount++;
                };

                redSlider.Value = 71;

                Assert.Equal(expectedColor, dialog.Color);
                Assert.Equal(expectedColor, previewColor);
                Assert.Equal(1, changeCount);
            }
            finally
            {
                dialog.Close();
            }
        }

        [AvaloniaFact]
        public void Editor_Hex_Change_Should_Update_Dialog_Color()
        {
            var dialog = new DaisyColorPickerDialog
            {
                Color = Colors.Red,
                OriginalColor = Colors.Red
            };
            dialog.Show();

            try
            {
                var editor = Assert.Single(dialog.GetVisualDescendants().OfType<DaisyColorEditor>());
                var expectedColor = Color.FromArgb(255, 71, 0, 0);
                Color? previewColor = null;
                var changeCount = 0;
                dialog.PreviewColorChanged += (_, e) =>
                {
                    previewColor = e.Color;
                    changeCount++;
                };

                editor.HexValue = "#FF470000";

                Assert.Equal(expectedColor, dialog.Color);
                Assert.Equal(expectedColor, previewColor);
                Assert.Equal(1, changeCount);
            }
            finally
            {
                dialog.Close();
            }
        }

        [AvaloniaFact]
        public void Undo_And_Redo_Should_Navigate_Color_History()
        {
            var dialog = new DaisyColorPickerDialog { Color = Colors.Red };
            dialog.Show();

            try
            {
                var undoButton = GetHistoryButton(dialog, "PART_UndoButton");
                var redoButton = GetHistoryButton(dialog, "PART_RedoButton");

                dialog.Color = Colors.Green;
                dialog.Color = Colors.Blue;

                Assert.True(undoButton.IsEnabled);
                Assert.False(redoButton.IsEnabled);

                Click(undoButton);
                Assert.Equal(Colors.Green, dialog.Color);
                Assert.True(redoButton.IsEnabled);

                Click(undoButton);
                Assert.Equal(Colors.Red, dialog.Color);
                Assert.False(undoButton.IsEnabled);

                Click(redoButton);
                Assert.Equal(Colors.Green, dialog.Color);

                dialog.Color = Colors.Yellow;
                Assert.False(redoButton.IsEnabled);
            }
            finally
            {
                dialog.Close();
            }
        }

        private static Button GetHistoryButton(DaisyColorPickerDialog dialog, string name)
        {
            return Assert.Single(
                dialog.GetVisualDescendants().OfType<Button>(),
                button => button.Name == name);
        }

        private static void Click(Button button)
        {
            button.PointerClick();
        }
    }
}
