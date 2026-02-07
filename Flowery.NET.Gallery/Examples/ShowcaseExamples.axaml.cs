using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Flowery.NET.Gallery.Examples
{
    public partial class ShowcaseExamples : UserControl
    {
        public ShowcaseExamples()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
