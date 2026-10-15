"""Regression checks for source API extraction and documentation routes.

Run from the repository root: python -B -m unittest discover -s Utils -p test_documentation.py
"""
import contextlib
import io
from pathlib import Path
import unittest
from unittest.mock import Mock, patch

from generate_docs import CSharpParser, DocumentationGenerator, MarkdownGenerator
from generate_site import MarkdownToHtml, SiteGenerator
from docs_manifest import GUIDE_FILES, page_manifest

ROOT = Path(__file__).resolve().parent.parent


class DocumentationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.controls = {control.name: control for control in DocumentationGenerator(ROOT, auto_parse=True)._parse_all_controls()}

    def test_multiline_nested_generic_and_default_arguments(self):
        source = Mock()
        source.name = 'Sample.cs'
        source.read_text.return_value = '''
public class Sample : Control
{
    public static readonly StyledProperty<IReadOnlyList<string>> ItemsProperty =
        AvaloniaProperty.Register<Sample, IReadOnlyList<string>>(
            nameof(Items), Array.Empty<string>(), inherits: false);
    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<Sample, double>(nameof(Scale), 0.4, validate: value => value > 0);
    public static readonly AttachedProperty<bool> MotionProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Motion", typeof(Sample), true, inherits: true);
    public static readonly DirectProperty<Sample, int> CountProperty =
        AvaloniaProperty.RegisterDirect<Sample, int>(nameof(Count), owner => owner.Count);
    public void Refresh(int index, string label = "a,b") { }
    private class Nested { public int NotSample { get; set; } }
}
'''
        control = CSharpParser().parse_file(source)
        props = {prop.name: prop for prop in control.properties}
        self.assertEqual('IReadOnlyList<string>', props['Items'].prop_type)
        self.assertEqual('Array.Empty<string>()', props['Items'].default)
        self.assertEqual('0.4', props['Scale'].default)
        self.assertEqual('true', props['Motion'].default)
        self.assertEqual('read-only', props['Count'].default)
        self.assertNotIn('NotSample', props)
        self.assertIn('public void Refresh(int index, string label = "a,b")', control.methods)

    def test_real_partials_inheritance_and_public_helpers_are_included(self):
        controls = self.controls
        self.assertEqual('0.4', next(prop.default for prop in controls['DaisyCarousel'].properties if prop.name == 'TransitionDuration'))
        scheduler = {prop.name for prop in controls['DaisyScheduler'].properties}
        self.assertTrue({'ResourceKindFilter', 'WeekRule', 'ClockFormat', 'SelectedAppointments'} <= scheduler)
        self.assertTrue(any('GetOccupiedResources(' in method for method in controls['DaisyScheduler'].methods))
        self.assertIn('Label', {prop.name for prop in controls['DaisyTextArea'].properties})
        self.assertTrue({'FlowKanban', 'FlowKanbanDialogBase', 'FlowerySizeManager', 'LocalizeExtensionBase', 'FloweryAnimationHelpers'} <= controls.keys())
        text = MarkdownGenerator(auto_parse=True).generate_master_index(list(controls.values()), {})
        self.assertIn('DaisyThemeManager.ApplyTheme("dracula")', text)
        self.assertNotIn('DaisyThemeManager.Instance', text)

    def test_fragment_and_guide_links_resolve_from_both_depths(self):
        converter = MarkdownToHtml()
        converter.pages = page_manifest(['DaisyThemeManager', 'DaisySizeDropdown'], GUIDE_FILES)
        source = '[Theme](DaisyThemeManager.md#theme-visibility-and-ordering)\n[Tokens](DesignTokens.md)\n[Size](DaisySizeDropdown.md)'
        nested = converter.convert(source, depth=1)
        self.assertIn('href="../controls/DaisyThemeManager.html#theme-visibility-and-ordering"', nested)
        self.assertIn('href="../DesignTokens.html"', nested)
        root = converter.convert(source, depth=0)
        self.assertIn('href="controls/DaisySizeDropdown.html"', root)
        self.assertIn('id="theme-visibility-and-ordering"', converter.convert('## Theme Visibility and Ordering'))

    def test_site_modes_share_navigation_index_and_guide_assets(self):
        manifests = []
        for generated in (False, True):
            site = SiteGenerator(ROOT / 'llms', ROOT / 'docs', ROOT / 'llms-static', use_generated=generated)
            methods = ('_copy_images', '_copy_guides', '_write_css', '_generate_shell', '_generate_home', '_generate_control_pages', '_generate_category_pages')
            with contextlib.ExitStack() as stack:
                stack.enter_context(patch.object(Path, 'mkdir'))
                stack.enter_context(contextlib.redirect_stdout(io.StringIO()))
                for method in methods:
                    stack.enter_context(patch.object(site, method))
                site.generate()
            manifests.append(site.converter.pages)
            index = site._generate_llms_txt_from_curated()
            for name in ('DaisyScheduler', 'FlowKanban', 'FlowKanbanDialogBase', 'LocalizeExtensionBase', 'working_with_animations'):
                self.assertIn(name, index)
            self.assertEqual(ROOT / 'llms-static', site.curated_dir)
        self.assertEqual(manifests[0], manifests[1])


if __name__ == '__main__':
    unittest.main()
