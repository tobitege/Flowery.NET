using System.Globalization;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class SidebarItemSearchTests
    {
        [Theory]
        [InlineData("Tag-Auswahl")]
        [InlineData("tag-auswahl")]
        [InlineData("Auswahl")]
        [InlineData("DaisyTagPicker")]
        [InlineData("TagPicker")]
        [InlineData("tag picker")]
        public void MatchesSearch_Finds_Translated_And_Control_Name(string search)
        {
            var item = new SidebarItem { Id = "tagpicker", Name = "Tag-Auswahl", ControlName = "DaisyTagPicker" };

            Assert.True(item.MatchesSearch(search));
        }

        [Theory]
        [InlineData("Toggle")]
        [InlineData("---")]
        public void MatchesSearch_Rejects_Unrelated_Text(string search)
        {
            var item = new SidebarItem { Id = "tagpicker", Name = "Tag-Auswahl", ControlName = "DaisyTagPicker" };

            Assert.False(item.MatchesSearch(search));
        }

        [Fact]
        public void MatchesSearch_Without_ControlName_Uses_Translated_Name_Only()
        {
            var item = new SidebarItem { Id = "modal-radii", Name = "Modal Corner Radii" };

            Assert.True(item.MatchesSearch("corner radii"));
            Assert.False(item.MatchesSearch("DaisyModal"));
        }

        [Theory]
        [InlineData("DaisyInput", true)]
        [InlineData("GlassSurface", true)]
        [InlineData("daisy modal", true)]
        [InlineData("InputDaisy", false)]
        [InlineData("DaisyButton", false)]
        public void MatchesSearch_Finds_Each_Search_Term_On_Its_Own(string search, bool expected)
        {
            var item = new SidebarItem
            {
                Id = "glass-controls",
                Name = "Glass Controls",
                SearchTerms = "DaisyGlassSurface DaisyNavbar DaisyInput DaisyBadge DaisyModal"
            };

            Assert.Equal(expected, item.MatchesSearch(search));
        }

        [AvaloniaFact]
        public void Icon_Keys_Resolve_To_The_Geometry_The_Resources_Hold_Now()
        {
            var resources = Application.Current!.Resources;
            var shipped = IconKeyConverter.Instance.Convert("DaisyIconHome", typeof(Geometry), null, CultureInfo.InvariantCulture);
            Assert.IsType<StreamGeometry>(shipped);

            // A geometry resolved earlier must not outlive the resources it came from.
            var replacement = StreamGeometry.Parse("M0,0 L1,1");
            resources["DaisyIconHome"] = replacement;
            try
            {
                Assert.Same(replacement, IconKeyConverter.Instance.Convert("DaisyIconHome", typeof(Geometry), null, CultureInfo.InvariantCulture));
            }
            finally
            {
                resources.Remove("DaisyIconHome");
            }

            Assert.Same(shipped, IconKeyConverter.Instance.Convert("DaisyIconHome", typeof(Geometry), null, CultureInfo.InvariantCulture));
            Assert.Null(IconKeyConverter.Instance.Convert("NoSuchIcon", typeof(Geometry), null, CultureInfo.InvariantCulture));
        }
    }
}
