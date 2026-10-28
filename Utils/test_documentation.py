"""Regression checks for source API extraction and documentation routes.

Run from the repository root: python -B -m unittest discover -s Utils -p test_documentation.py
"""
import contextlib
from html.parser import HTMLParser
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


def parsed_elements(html: str) -> list[tuple[str, list[tuple[str, str]]]]:
    """The elements a browser builds from the HTML, with their attributes."""
    elements = []
    parser = HTMLParser()
    parser.handle_starttag = lambda tag, attributes: elements.append((tag, attributes))
    parser.feed(html)
    return elements


class SiteEscapingTests(unittest.TestCase):
    """The site converter treats markdown as untrusted: only safe HTML and URLs reach the pages."""

    def setUp(self):
        self.converter = MarkdownToHtml()

    def test_code_and_prose_are_escaped_and_entities_kept(self):
        html = self.converter.convert('Use `IList<Color>` or a Func<bool, bool> & keep &quot; and `a*b*c`.')
        self.assertIn('<code>IList&lt;Color&gt;</code>', html)
        self.assertIn('Func&lt;bool, bool&gt; &amp; keep &quot;', html)
        self.assertIn('<code>a*b*c</code>', html)
        self.assertIn('<pre><code class="language-xml">&lt;controls:DaisyButton /&gt;</code></pre>',
                      self.converter.convert('```xml\n<controls:DaisyButton />\n```'))

    def test_unsafe_html_is_escaped_and_comments_dropped(self):
        source = ('<script>alert(1)</script> <img src="a.png" onerror="alert(1)"> <iframe src="https://example.com">'
                  ' <img src="javascript:alert(1)"> <img src="a.png" style="width:url(x)"> <!-- hidden -->')
        html = self.converter.convert(source)
        for fragment in ('<script', '<img', '<iframe', 'hidden'):
            self.assertNotIn(fragment, html)
        self.assertIn('&lt;script&gt;', html)

    def test_allowed_tags_are_rebuilt_from_checked_attributes(self):
        html = self.converter.convert(
            '<img src="../images/A.png" alt="A &amp; B" style="max-width:50%;height:auto;box-shadow:0 2px 8px rgba(0,0,0,0.15)">'
            ' <br/> <details open><summary>More</summary>Text</details> <kbd>Ctrl</kbd>')
        self.assertIn('<img src="../images/A.png" alt="A &amp; B" '
                      'style="max-width:50%;height:auto;box-shadow:0 2px 8px rgba(0,0,0,0.15)">', html)
        for fragment in ('<br>', '<details open>', '<summary>More</summary>', '</details>', '<kbd>Ctrl</kbd>'):
            self.assertIn(fragment, html)

    def test_links_and_images_keep_only_safe_urls(self):
        html = self.converter.convert('[a](javascript:x) [b](JaVa&#115;cript:x) [c](java\tscript:x) '
                                      '![d](data:image/svg+xml,x)')
        self.assertEqual('<p>a b c d</p>', html)
        html = self.converter.convert('[e](x.md" onmouseover="alert(1))')
        self.assertIn('href="x.md&quot; onmouseover=&quot;alert(1"', html)
        html = self.converter.convert('[f](https://example.com/?q=1&r=2) [g](mailto:a@example.com) [h](#base-color)')
        self.assertIn('href="https://example.com/?q=1&amp;r=2" target="_blank" rel="noopener"', html)
        self.assertIn('href="mailto:a@example.com"', html)
        self.assertIn('href="#base-color"', html)

    def test_code_cannot_end_an_attribute(self):
        sources = ('<img src="a.png" alt="`x" onerror="alert(1)`">', '![`x" onerror="alert(1)`](a.png)',
                   '[a](`x" onclick="alert(1)`)', '<img src="`a.png" onerror="alert(1)`">')
        for source in sources:
            with self.subTest(source=source):
                elements = parsed_elements(self.converter.convert(source))
                self.assertNotIn('img', [tag for tag, _ in elements])
                self.assertEqual([], [name for _, attributes in elements for name, _ in attributes if name.startswith('on')])

    def test_headings_alerts_and_trusted_html(self):
        self.assertIn('<h2 id="tips--best-practices">Tips &amp; Best Practices</h2>',
                      self.converter.convert('## Tips & Best Practices'))
        self.assertIn('<h2 id="foot--bar"><code>Foo&lt;T&gt;</code> &amp; Bar</h2>',
                      self.converter.convert('## `Foo<T>` & Bar'))
        alert = self.converter.convert('> [!NOTE]\n> Use <b>bold</b> and `List<T>`.\n')
        self.assertIn('<div class="alert alert-note">', alert)
        self.assertIn('&lt;b&gt;bold&lt;/b&gt;', alert)
        self.assertIn('<code>List&lt;T&gt;</code>', alert)
        token = self.converter.trusted('<div class="tabbed-gallery"><input type="radio"></div>')
        html = self.converter.convert(f'## Overview\n{token}\n__TRUSTED_HTML_9__ __INLINE_CODE_9__')
        self.assertIn('<div class="tabbed-gallery"><input type="radio"></div>', html)
        self.assertNotIn(f'<p>{token}', html)
        self.assertIn('__TRUSTED_HTML_9__ __INLINE_CODE_9__', html)


if __name__ == '__main__':
    unittest.main()
