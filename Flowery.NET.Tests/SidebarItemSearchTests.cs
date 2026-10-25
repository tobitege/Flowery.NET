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
        [InlineData("DaisyGlassInput", true)]
        [InlineData("GlassNavbar", true)]
        [InlineData("glass modal", true)]
        [InlineData("InputDaisy", false)]
        [InlineData("DaisyGlassButton", false)]
        public void MatchesSearch_Finds_Each_Search_Term_On_Its_Own(string search, bool expected)
        {
            var item = new SidebarItem
            {
                Id = "glass-controls",
                Name = "Glass Controls",
                SearchTerms = "DaisyGlassNavbar DaisyGlassInput DaisyGlassBadge DaisyGlassModal"
            };

            Assert.Equal(expected, item.MatchesSearch(search));
        }
    }
}
