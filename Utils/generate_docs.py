#!/usr/bin/env python3
"""
Flowery.NET Documentation Generator

Generates markdown documentation from curated llms-static/ files.
Optionally parses C# control files and AXAML examples to supplement.

Usage:
    python Utils/generate_docs.py              # Use curated docs only (default)
    python Utils/generate_docs.py --auto-parse # Include auto-parsed Properties/Examples

================================================================================
CODE REQUIREMENTS FOR PARSING
================================================================================

For the documentation generator to correctly extract metadata, code must follow
these conventions:

C# CONTROL FILES (Flowery.NET/Controls/Daisy*.cs):
--------------------------------------------------
1. Class XML documentation must immediately precede the class definition:

   /// <summary>
   /// A Button control styled after DaisyUI's Button component.
   /// </summary>
   public class DaisyButton : Button

2. StyledProperty definitions must use this exact pattern:

   public static readonly StyledProperty<TYPE> NAMEProperty =
       AvaloniaProperty.Register<CLASS, TYPE>(nameof(NAME), DEFAULT);

3. Property XML documentation must immediately precede the StyledProperty:

   /// <summary>
   /// Gets or sets the button variant (Primary, Secondary, etc.).
   /// </summary>
   public static readonly StyledProperty<DaisyButtonVariant> VariantProperty = ...

4. Enums must be defined at namespace level with public access:

   public enum DaisyButtonVariant
   {
       Default,
       Primary,
       Secondary,
       ...
   }

AXAML EXAMPLE FILES (Flowery.NET.Gallery/Examples/*Examples.axaml):
-------------------------------------------------------------------
1. Each control section must start with a SectionHeader:

   <local:SectionHeader SectionId="button" Title="Button" />

2. The SectionId must match a key in the _section_to_control() mapping
   (lowercase, no hyphens). Add new mappings if creating new controls.

3. Sub-examples should be labeled with a TextBlock having FontWeight="SemiBold":

   <TextBlock Text="Colors" FontWeight="SemiBold" FontSize="14" Opacity="0.8"/>
   <WrapPanel>
       <controls:DaisyButton Variant="Primary" Content="Primary"/>
       ...
   </WrapPanel>

4. Sections are separated by DaisyDivider:

   <controls:DaisyDivider />

5. Control elements use the "controls:" namespace prefix:

   xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"

ADDING NEW CONTROLS:
--------------------
1. Create the C# control file following the patterns above
2. Add examples in the appropriate *Examples.axaml file
3. Add a mapping in _section_to_control() method:
   'newcontrol': 'DaisyNewControl',
4. Run: python Utils/generate_docs.py

OUTPUT:
-------
    llms/llms.txt            - Master index for LLMs
    llms/controls/*.md       - Per-control documentation
    llms/categories/*.md     - Category overviews

SUPPLEMENTARY DOCUMENTATION:
----------------------------
To add rich descriptions, usage guides, or variant explanations that won't be
overwritten by the generator, create markdown files in llms-static/:

    llms-static/DaisyLoading.md   - Extra docs for DaisyLoading control
    llms-static/DaisyButton.md    - Extra docs for DaisyButton control

The supplementary content is inserted AFTER the header/description and BEFORE the
auto-generated Properties section. HTML comments (<!-- -->) are stripped.

Example supplementary file:
    ## Overview
    DaisyLoading provides 11 animation variants...

    ## Animation Variants
    | Variant | Description |
    |---------|-------------|
    | Spinner | Classic rotating arc... |

================================================================================
"""

import argparse
import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional


@dataclass
class EnumInfo:
    """Represents a C# enum definition."""
    name: str
    values: list[str]
    description: str = ""


@dataclass
class PropertyInfo:
    """Represents a StyledProperty definition."""
    name: str
    prop_type: str
    default: str
    description: str = ""


@dataclass
class ControlInfo:
    """Represents a Daisy control class."""
    name: str
    base_class: str
    description: str
    properties: list[PropertyInfo] = field(default_factory=list)
    enums: list[EnumInfo] = field(default_factory=list)


@dataclass
class ExampleSnippet:
    """Represents an AXAML example snippet."""
    section_id: str
    title: str
    sub_examples: list[tuple[str, str]] = field(default_factory=list)  # (label, xaml)


# =============================================================================
# C# Parser
# =============================================================================

class CSharpParser:
    """Parses C# control files to extract metadata."""

    def parse_file(self, filepath: Path) -> Optional[ControlInfo]:
        """Parse a C# control file and extract metadata."""
        content = filepath.read_text(encoding='utf-8')

        # Extract enums
        enums = self._extract_enums(content)

        # Extract class info
        target_name = filepath.stem
        class_info = self._extract_class(content, target_name)
        if not class_info:
            return None

        class_name, base_class, description = class_info

        # Extract properties
        properties = self._extract_properties(content)

        return ControlInfo(
            name=class_name,
            base_class=base_class,
            description=description,
            properties=properties,
            enums=enums
        )

    def _extract_enums(self, content: str) -> list[EnumInfo]:
        """Extract all enum definitions from the file."""
        enums = []
        enum_pattern = re.compile(
            r'public\s+enum\s+(\w+)\s*\{([^}]+)\}',
            re.DOTALL
        )
        for match in enum_pattern.finditer(content):
            enum_name = match.group(1)
            enum_body = match.group(2)
            values = []
            for line in enum_body.split('\n'):
                line = line.strip()
                if not line or line.startswith('//'):
                    continue
                value_match = re.match(r'(\w+)', line)
                if value_match:
                    values.append(value_match.group(1))
            enums.append(EnumInfo(name=enum_name, values=values))
        return enums

    def _extract_class(self, content: str, target_name: str) -> Optional[tuple[str, str, str]]:
        """
        Extract class info for the target class name.
        Returns (class_name, base_class, description).
        """
        # Regex to match class definition:
        # public [static] [sealed|partial] class Name [: Base]
        class_pattern = re.compile(
            r'public\s+(?:static\s+|sealed\s+|partial\s+)*class\s+(\w+)(?:\s*:\s*([\w\.]+))?',
            re.MULTILINE
        )

        # Find all classes
        matches = list(class_pattern.finditer(content))

        selected_match = None

        # 1. Try to find exact match with filename
        for m in matches:
            if m.group(1) == target_name:
                selected_match = m
                break

        # 2. Fallback: Find first class starting with "Daisy"
        if not selected_match:
            for m in matches:
                if m.group(1).startswith("Daisy"):
                    selected_match = m
                    break

        if not selected_match:
            return None

        class_name = selected_match.group(1)
        base_class = selected_match.group(2) or "Object" # Handle no base class (static)

        # Find the summary comment immediately before the class
        class_start = selected_match.start()
        before_class = content[:class_start]

        # Look for the last summary comment before class definition
        summary_pattern = re.compile(
            r'///\s*<summary>\s*(.*?)\s*</summary>',
            re.DOTALL
        )

        summaries = list(summary_pattern.finditer(before_class))
        description = ""
        if summaries:
            last_summary = summaries[-1]
            # Check if summary is close to class definition (within 300 chars to be safe)
            if class_start - last_summary.end() < 300:
                raw_desc = last_summary.group(1)
                # Clean the description
                description = self._clean_summary(raw_desc)

        return class_name, base_class, description

    def _extract_properties(self, content: str) -> list[PropertyInfo]:
        """Extract all StyledProperty definitions."""
        properties = []
        seen = set()

        # Split content into property blocks
        lines = content.split('\n')
        i = 0
        while i < len(lines):
            line = lines[i]

            # Look for StyledProperty definition
            prop_match = re.search(
                r'public\s+static\s+readonly\s+StyledProperty<([^>]+)>\s+(\w+)Property',
                line
            )
            if prop_match:
                prop_type = prop_match.group(1).strip()
                prop_name_with_property = prop_match.group(2)
                prop_name = prop_name_with_property  # Will extract actual name from nameof

                # Find the nameof and default value
                block = line
                j = i + 1
                paren_count = line.count('(') - line.count(')')
                while paren_count > 0 and j < len(lines):
                    block += '\n' + lines[j]
                    paren_count += lines[j].count('(') - lines[j].count(')')
                    j += 1

                # Extract nameof
                nameof_match = re.search(r'nameof\((\w+)\)', block)
                if nameof_match:
                    prop_name = nameof_match.group(1)

                # Extract default value
                default = self._extract_default(block, prop_type)

                # Look for summary comment above
                description = ""
                k = i - 1
                while k >= 0 and k >= i - 5:
                    if '/// <summary>' in lines[k]:
                        # Gather the summary
                        summary_lines = []
                        for m in range(k, min(k + 5, len(lines))):
                            summary_lines.append(lines[m])
                            if '</summary>' in lines[m]:
                                break
                        summary_text = '\n'.join(summary_lines)
                        desc_match = re.search(r'<summary>\s*(.*?)\s*</summary>', summary_text, re.DOTALL)
                        if desc_match:
                            description = self._clean_summary(desc_match.group(1))
                        break
                    k -= 1

                if prop_name not in seen:
                    seen.add(prop_name)
                    properties.append(PropertyInfo(
                        name=prop_name,
                        prop_type=prop_type,
                        default=default,
                        description=description
                    ))

            i += 1

        return properties

    def _extract_default(self, block: str, prop_type: str) -> str:
        """Extract default value from property registration block."""
        # Look for the default value after nameof - handle nested parentheses
        # Pattern: nameof(X), DefaultValue)
        match = re.search(r'nameof\(\w+\)\s*,\s*(.+?)\)\s*;', block, re.DOTALL)
        if match:
            default_raw = match.group(1).strip()
            # Handle nested constructor calls like new Thickness(32)
            paren_count = 0
            result = []
            for char in default_raw:
                if char == '(':
                    paren_count += 1
                    result.append(char)
                elif char == ')':
                    if paren_count > 0:
                        paren_count -= 1
                        result.append(char)
                    else:
                        break  # End of default value
                else:
                    result.append(char)
            default_raw = ''.join(result).strip()
            if default_raw:
                return self._clean_default(default_raw, prop_type)
        return "-"

    def _clean_summary(self, text: str) -> str:
        """Clean XML documentation text."""
        if not text:
            return ""
        # Remove /// markers
        text = re.sub(r'^\s*///\s*', '', text, flags=re.MULTILINE)
        # Remove XML tags
        text = re.sub(r'<[^>]+>', '', text)
        # Collapse whitespace
        text = ' '.join(text.split())
        return text.strip()

    def _clean_default(self, default: str, prop_type: str) -> str:
        """Clean and simplify default value representation."""
        if not default:
            return "null" if "?" in prop_type else "-"
        default = default.strip().rstrip(',')
        # Simplify common patterns
        if 'new Thickness(' in default:
            match = re.search(r'Thickness\((\d+)\)', default)
            if match:
                return f"Thickness({match.group(1)})"
            return "Thickness"
        if 'Color.FromArgb' in default:
            return "Color(semitransparent)"
        if 'Colors.' in default:
            return default.replace('Colors.', '')
        # Enum defaults
        if '.' in default:
            return default.split('.')[-1]
        return default


# =============================================================================
# AXAML Parser
# =============================================================================

class AxamlParser:
    """Parses AXAML example files to extract snippets."""

    def parse_file(self, filepath: Path) -> list[ExampleSnippet]:
        """Parse an AXAML file and extract example snippets."""
        content = filepath.read_text(encoding='utf-8')
        snippets = []

        # Find all section headers
        section_pattern = re.compile(
            r'<local:SectionHeader\s+(?:SectionId="([^"]+)"\s+)?Title="([^"]+)"[^/]*/?>',
            re.IGNORECASE
        )

        divider_pattern = re.compile(r'<controls:DaisyDivider\s*/?>', re.IGNORECASE)

        sections = []
        for match in section_pattern.finditer(content):
            section_id = match.group(1) or self._id_from_title(match.group(2))
            title = match.group(2)
            start_pos = match.end()
            sections.append({
                'id': section_id,
                'title': title,
                'start': start_pos
            })

        # Find section boundaries
        for i, section in enumerate(sections):
            next_section_start = sections[i + 1]['start'] if i + 1 < len(sections) else len(content)

            divider_match = divider_pattern.search(content, section['start'])
            if divider_match and divider_match.start() < next_section_start:
                end_pos = divider_match.start()
            else:
                end_pos = next_section_start

            section_content = content[section['start']:end_pos]

            # Extract sub-examples
            sub_examples = self._extract_sub_examples(section_content)

            if sub_examples:
                snippets.append(ExampleSnippet(
                    section_id=section['id'],
                    title=section['title'],
                    sub_examples=sub_examples
                ))

        return snippets

    def _id_from_title(self, title: str) -> str:
        """Generate section ID from title."""
        return title.lower().replace(' ', '-').replace('/', '-')

    def _extract_sub_examples(self, content: str) -> list[tuple[str, str]]:
        """Extract labeled sub-examples from content."""
        sub_examples = []

        # Look for TextBlock with FontWeight="SemiBold" as labels
        label_pattern = re.compile(
            r'<TextBlock\s+Text="([^"]+)"[^>]*FontWeight="SemiBold"[^>]*/?>',
            re.IGNORECASE
        )

        matches = list(label_pattern.finditer(content))

        for idx, match in enumerate(matches):
            label = match.group(1)
            start = match.end()
            end = matches[idx + 1].start() if idx + 1 < len(matches) else len(content)
            snippet = content[start:end]

            controls = self._extract_control_elements(snippet)
            if controls:
                sub_examples.append((label, controls))

        # If no labeled sub-examples, extract controls directly
        if not sub_examples:
            controls = self._extract_control_elements(content)
            if controls:
                sub_examples.append(("Example", controls))

        return sub_examples

    def _extract_control_elements(self, content: str, target_control: str = None) -> str:
        """Extract and format control elements."""
        controls = []

        # Match self-closing or paired control elements with any namespace prefix
        # Matches: controls:DaisyButton, colorpicker:DaisyColorWheel, weather:DaisyWeatherCard, etc.
        self_closing = re.compile(r'<(\w+):(Daisy\w+)([^>]*)/>')
        paired_pattern = re.compile(r'<(\w+):(Daisy\w+)([^>]*)>')

        for match in self_closing.finditer(content):
            ns_prefix = match.group(1)
            control_name = match.group(2)
            # Skip dividers and unrelated controls if we have a target
            if control_name == 'DaisyDivider':
                continue
            attrs = match.group(3)
            xaml = self._format_control(control_name, attrs, None)
            if xaml and xaml not in controls:
                controls.append(xaml)
            if len(controls) >= 3:
                break

        if len(controls) < 3:
            for match in paired_pattern.finditer(content):
                ns_prefix = match.group(1)
                control_name = match.group(2)
                if control_name == 'DaisyDivider':
                    continue
                attrs = match.group(3)
                start = match.end()
                # Try to find end tag with same namespace prefix
                end_tag = f'</{ns_prefix}:{control_name}>'
                end_pos = content.find(end_tag, start)
                if end_pos != -1:
                    inner = content[start:end_pos].strip()
                    xaml = self._format_control(control_name, attrs, inner)
                    if xaml and xaml not in controls:
                        controls.append(xaml)
                if len(controls) >= 3:
                    break

        return '\n'.join(controls) if controls else ""

    def _format_control(self, control_name: str, attrs: str, inner: Optional[str]) -> str:
        """Format a control element for documentation."""
        # Skip certain layout containers
        if control_name in ('DaisyDivider',):
            return ""

        # Clean attributes
        attrs = self._clean_attrs(attrs)

        if inner and inner.strip():
            # Normalize and simplify inner content
            inner = self._normalize_indentation(inner)
            inner = self._simplify_inner(inner)
            if inner:
                return f'<controls:{control_name}{attrs}>\n    {inner}\n</controls:{control_name}>'
            else:
                return f'<controls:{control_name}{attrs}/>'
        else:
            return f'<controls:{control_name}{attrs}/>'

    def _normalize_indentation(self, content: str) -> str:
        """Normalize whitespace - collapse to single line for cleaner output."""
        # Collapse all whitespace (newlines, multiple spaces) into single spaces
        normalized = ' '.join(content.split())
        # Truncate if too long
        if len(normalized) > 150:
            normalized = normalized[:147] + "..."
        return normalized

    def _clean_attrs(self, attrs: str) -> str:
        """Clean attributes string."""
        # Remove layout and event attributes
        remove_patterns = [
            r'\s*Margin="[^"]*"',
            r'\s*Width="\d+"',
            r'\s*Height="\d+"',
            r'\s*HorizontalAlignment="[^"]*"',
            r'\s*VerticalAlignment="[^"]*"',
            r'\s*Click="[^"]*"',
            r'\s*x:Name="[^"]*"',
            r'\s*Tag="[^"]*"',
            r'\s*ToolTip\.Tip="[^"]*"',
        ]
        for pattern in remove_patterns:
            attrs = re.sub(pattern, '', attrs)

        # Collapse whitespace
        attrs = ' '.join(attrs.split())
        if attrs:
            attrs = ' ' + attrs
        return attrs

    def _simplify_inner(self, inner: str) -> str:
        """Simplify inner content."""
        # First normalize the indentation
        inner = self._normalize_indentation(inner)

        # If it contains complex nested controls, just indicate content
        if '<controls:' in inner or '<StackPanel' in inner or '<Grid' in inner:
            # Check for PathIcon
            if '<PathIcon' in inner:
                path_match = re.search(r'<PathIcon[^>]*Data="([^"]{1,100})"[^>]*/>', inner)
                if path_match:
                    return f'<PathIcon Data="{path_match.group(1)[:50]}..."/>'
                path_match = re.search(r'<PathIcon[^>]*Data="\{[^}]+\}"[^>]*/>', inner)
                if path_match:
                    return path_match.group(0)
            return "<!-- Content -->"

        # For simpler content, clean it up
        inner = inner.strip()
        if len(inner) > 100:
            return inner[:100] + "..."
        return inner


# =============================================================================
# Markdown Generator
# =============================================================================

class MarkdownGenerator:
    """Generates markdown documentation files."""

    def __init__(self, extras_dir: Path | None = None, auto_parse: bool = False):
        """Initialize with optional supplementary docs directory."""
        self.extras_dir = extras_dir
        self.auto_parse = auto_parse

    def _load_extra(self, control_name: str) -> str:
        """Load supplementary documentation for a control if it exists."""
        if not self.extras_dir:
            return ""
        extra_file = self.extras_dir / f"{control_name}.md"
        if extra_file.exists():
            content = extra_file.read_text(encoding='utf-8')
            # Remove HTML comments (metadata comments)
            content = re.sub(r'<!--.*?-->', '', content, flags=re.DOTALL)
            return content.strip()
        return ""

    def generate_control_doc(self, control: ControlInfo, examples: list[ExampleSnippet]) -> str:
        """Generate markdown documentation for a control."""
        lines = []

        # Header
        lines.append(f"# {control.name}")
        lines.append("")

        # Description
        if control.description:
            lines.append(control.description)
        else:
            simple_name = control.name.replace('Daisy', '')
            lines.append(f"A {simple_name} control styled after DaisyUI.")
        lines.append("")

        # Base class
        lines.append(f"**Inherits from:** `{control.base_class}`")
        lines.append("")

        # Load and insert supplementary documentation from llms-static/
        extra_content = self._load_extra(control.name)
        if extra_content:
            lines.append(extra_content)
            lines.append("")

        # Only include auto-parsed sections if flag is set
        if self.auto_parse:
            # Properties table
            if control.properties:
                lines.append("## Properties")
                lines.append("")
                lines.append("| Property | Type | Default | Description |")
                lines.append("|----------|------|---------|-------------|")
                for prop in control.properties:
                    desc = prop.description if prop.description else "-"
                    # Truncate long descriptions
                    if len(desc) > 80:
                        desc = desc[:77] + "..."
                    lines.append(f"| {prop.name} | `{prop.prop_type}` | {prop.default} | {desc} |")
                lines.append("")

            # Enums
            if control.enums:
                lines.append("## Enum Values")
                lines.append("")
                for enum in control.enums:
                    lines.append(f"### {enum.name}")
                    lines.append("")
                    lines.append(f"`{', '.join(enum.values)}`")
                    lines.append("")

            # Examples
            if examples:
                lines.append("## Usage Examples")
                lines.append("")
                seen_xaml = set()
                example_count = 0
                for example in examples:
                    for label, xaml in example.sub_examples:
                        # Normalize XAML for comparison (remove variable text content)
                        normalized = self._normalize_xaml_for_comparison(xaml)
                        if normalized in seen_xaml:
                            continue
                        seen_xaml.add(normalized)

                        # Skip labels that look like data-bound values
                        if self._is_data_label(label):
                            label = "Example"

                        lines.append(f"### {label}")
                        lines.append("")
                        lines.append("```xml")
                        lines.append(xaml)
                        lines.append("```")
                        lines.append("")

                        example_count += 1
                        if example_count >= 5:
                            break
                    if example_count >= 5:
                        break

        return '\n'.join(lines)

    def _normalize_xaml_for_comparison(self, xaml: str) -> str:
        """Normalize XAML to detect structural duplicates."""
        # Extract just tag names in sequence (most aggressive dedup)
        tags = re.findall(r'</?(?:controls:)?(\w+)', xaml)
        return ' '.join(tags)

    def _is_data_label(self, label: str) -> bool:
        """Check if a label looks like data-bound content rather than a description."""
        # Labels from bindings
        if label.startswith('{') or label.startswith('Binding'):
            return True
        # All caps labels are likely data values
        if label.isupper() and len(label) > 3:
            return True
        # Labels with quotes are data values
        if '"' in label or "'" in label:
            return True
        return False

    def generate_category_doc(self, category: str, controls: list[ControlInfo]) -> str:
        """Generate markdown documentation for a category."""
        lines = []

        lines.append(f"# {category}")
        lines.append("")
        lines.append(f"This category contains {len(controls)} controls:")
        lines.append("")

        for control in controls:
            desc = control.description if control.description else f"A {control.name.replace('Daisy', '')} control."
            # Don't truncate - show full description in category pages
            lines.append(f"- **[{control.name}](../controls/{control.name}.html)**: {desc}")

        lines.append("")
        lines.append("See individual control documentation for detailed usage.")
        lines.append("")

        return '\n'.join(lines)

    def generate_master_index(self, controls: list[ControlInfo], categories: dict[str, list[str]]) -> str:
        """Generate the master llms.txt index file."""
        lines = []

        lines.append("# Flowery.NET Component Library")
        lines.append("")
        lines.append("Flowery.NET is an Avalonia UI component library inspired by DaisyUI.")
        lines.append("It provides styled controls for building modern desktop applications.")
        lines.append("")
        lines.append("## Documentation Structure")
        lines.append("")
        lines.append("- `docs/llms.txt` - This file (overview and quick reference)")
        lines.append("- `docs/controls/*.md` - Per-control documentation with properties, enums, and examples")
        lines.append("- `docs/categories/*.md` - Category overviews grouping related controls")
        lines.append("")
        lines.append("## Quick Start")
        lines.append("")
        lines.append("Add the namespace to your AXAML:")
        lines.append("```xml")
        lines.append('xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"')
        lines.append("```")
        lines.append("")
        lines.append("## Controls Overview")
        lines.append("")
        lines.append("| Control | Description | Key Properties |")
        lines.append("|---------|-------------|----------------|")

        for control in sorted(controls, key=lambda c: c.name):
            # Skip non-Daisy classes
            if not control.name.startswith('Daisy'):
                continue
            desc = control.description
            if not desc:
                desc = f"{control.name.replace('Daisy', '')} control"
            if len(desc) > 50:
                desc = desc[:47] + "..."
            props = ", ".join(p.name for p in control.properties[:3])
            if len(control.properties) > 3:
                props += ", ..."
            lines.append(f"| [{control.name}](controls/{control.name}.html) | {desc} | {props} |")

        lines.append("")

        # Categories
        if categories:
            lines.append("## Categories")
            lines.append("")
            for cat_name, cat_controls in categories.items():
                daisy_controls = [c for c in cat_controls if c.startswith('Daisy')]
                if daisy_controls:
                    lines.append(f"### {cat_name}")
                    lines.append(", ".join(daisy_controls))
                    lines.append("")

        # Common patterns
        lines.append("## Common Patterns")
        lines.append("")
        lines.append("### Variants")
        lines.append("Most controls support a `Variant` property:")
        lines.append("- `Primary`, `Secondary`, `Accent` - Brand colors")
        lines.append("- `Info`, `Success`, `Warning`, `Error` - Status colors")
        lines.append("- `Neutral`, `Ghost`, `Link` - Subtle styles (on some controls)")
        lines.append("")
        lines.append("```xml")
        lines.append('<controls:DaisyButton Variant="Primary" Content="Primary"/>')
        lines.append('<controls:DaisyAlert Variant="Success">Operation completed!</controls:DaisyAlert>')
        lines.append("```")
        lines.append("")
        lines.append("### Sizes")
        lines.append("Controls support a `Size` property with values:")
        lines.append("`ExtraSmall`, `Small`, `Medium` (default), `Large`, `ExtraLarge`")
        lines.append("")
        lines.append("```xml")
        lines.append('<controls:DaisyButton Size="Large" Content="Large Button"/>')
        lines.append('<controls:DaisyInput Size="Small" Watermark="Small input"/>')
        lines.append("```")
        lines.append("")
        lines.append("### Theming")
        lines.append("Use `DaisyThemeManager` to switch themes programmatically:")
        lines.append("```csharp")
        lines.append('DaisyThemeManager.Instance.CurrentTheme = "dracula";')
        lines.append("```")
        lines.append("")
        lines.append("Or use theme controls:")
        lines.append("```xml")
        lines.append('<controls:DaisyThemeDropdown/>')
        lines.append('<controls:DaisyThemeSwap LightTheme="light" DarkTheme="dark"/>')
        lines.append("```")
        lines.append("")
        lines.append("Available themes: light, dark, cupcake, bumblebee, emerald, corporate,")
        lines.append("synthwave, retro, cyberpunk, valentine, halloween, garden, forest,")
        lines.append("aqua, lofi, pastel, fantasy, wireframe, black, luxury, dracula, cmyk,")
        lines.append("autumn, business, acid, lemonade, night, coffee, winter, dim, nord, sunset")
        lines.append("")

        return '\n'.join(lines)


# =============================================================================
# Main Generator
# =============================================================================

class DocumentationGenerator:
    """Main documentation generator that orchestrates parsing and output."""

    def __init__(self, root_dir: Path, auto_parse: bool = False):
        self.root_dir = root_dir
        self.controls_dir = root_dir / "Flowery.NET" / "Controls"
        self.examples_dir = root_dir / "Flowery.NET.Gallery" / "Examples"
        self.output_dir = root_dir / "llms"
        self.supplementary_dir = root_dir / "llms-static"
        self.auto_parse = auto_parse

        self.csharp_parser = CSharpParser()
        self.axaml_parser = AxamlParser()
        self.md_generator = MarkdownGenerator(extras_dir=self.supplementary_dir, auto_parse=auto_parse)

        self.category_mapping = {
            "ActionsExamples": "Actions",
            "CardsExamples": "Cards & Layout",
            "DataDisplayExamples": "Data Display",
            "DataInputExamples": "Data Input",
            "DividerExamples": "Layout",
            "FeedbackExamples": "Feedback",
            "LayoutExamples": "Layout",
            "NavigationExamples": "Navigation",
            "ThemingExamples": "Theming",
            "CustomControls": "Custom",
            "ColorPickerExamples": "Color Picker"
        }

    def generate(self):
        """Generate all documentation."""
        print("Flowery.NET Documentation Generator")
        print("=" * 40)
        if self.auto_parse:
            print("Mode: FULL (curated + auto-parsed)")
        else:
            print("Mode: CURATED ONLY (llms-static/)")

        # Create output directories
        self.output_dir.mkdir(exist_ok=True)
        (self.output_dir / "controls").mkdir(exist_ok=True)
        (self.output_dir / "categories").mkdir(exist_ok=True)

        # Parse all controls (needed for control list even in curated-only mode)
        print("\n[1/4] Parsing C# control files...")
        controls = self._parse_all_controls()
        print(f"      Found {len(controls)} controls")

        # Parse examples only if auto_parse is enabled
        examples_by_control: dict[str, list[ExampleSnippet]] = {}
        if self.auto_parse:
            print("\n[2/4] Parsing AXAML example files...")
            examples_by_control = self._parse_all_examples()
            print(f"      Found examples for {len(examples_by_control)} controls")
        else:
            print("\n[2/4] Skipping AXAML parsing (curated-only mode)")

        # Generate per-control docs
        print("\n[3/4] Generating control documentation...")
        extras_count = 0
        for control in controls:
            examples = examples_by_control.get(control.name, [])
            doc = self.md_generator.generate_control_doc(control, examples)
            output_path = self.output_dir / "controls" / f"{control.name}.md"
            output_path.write_text(doc, encoding='utf-8')
            # Check if supplementary docs were merged
            if self.supplementary_dir.exists():
                extra_file = self.supplementary_dir / f"{control.name}.md"
                if extra_file.exists():
                    extras_count += 1
        print(f"      Generated {len(controls)} control docs")
        print(f"      Used {extras_count} curated docs from llms-static/")
        if self.auto_parse:
            print(f"      Included auto-parsed Properties/Enums/Examples")

        # Generate category docs
        print("\n[4/4] Generating category and index documentation...")
        categories = self._categorize_controls(controls, examples_by_control)
        for cat_name, cat_controls in categories.items():
            cat_control_infos = [c for c in controls if c.name in cat_controls]
            doc = self.md_generator.generate_category_doc(cat_name, cat_control_infos)
            safe_name = cat_name.lower().replace(' ', '-').replace('&', 'and')
            output_path = self.output_dir / "categories" / f"{safe_name}.md"
            output_path.write_text(doc, encoding='utf-8')

        # Generate master index
        master_doc = self.md_generator.generate_master_index(controls, categories)
        (self.output_dir / "llms.txt").write_text(master_doc, encoding='utf-8')

        print("\n" + "=" * 40)
        print("Documentation generated successfully!")
        print(f"Output directory: {self.output_dir}")

    def _parse_all_controls(self) -> list[ControlInfo]:
        """Parse all C# control files, including those in subfolders."""
        controls = []
        # Search recursively in Controls folder and all subfolders
        for filepath in self.controls_dir.glob("**/Daisy*.cs"):
            if "Converter" in filepath.name:
                continue
            control = self.csharp_parser.parse_file(filepath)
            if control:
                controls.append(control)
        return controls

    def _parse_all_examples(self) -> dict[str, list[ExampleSnippet]]:
        """Parse all AXAML example files and map to controls."""
        examples_by_control: dict[str, list[ExampleSnippet]] = {}

        for filepath in self.examples_dir.glob("*Examples.axaml"):
            snippets = self.axaml_parser.parse_file(filepath)
            for snippet in snippets:
                control_name = self._section_to_control(snippet.section_id)
                if control_name:
                    if control_name not in examples_by_control:
                        examples_by_control[control_name] = []
                    examples_by_control[control_name].append(snippet)

        return examples_by_control

    def _section_to_control(self, section_id: str) -> Optional[str]:
        """Map a section ID to a control name."""
        normalized = section_id.lower().replace('-', '').replace('_', '')

        mappings = {
            'button': 'DaisyButton',
            'dropdown': 'DaisySelect',
            'fab': 'DaisyFab',
            'modal': 'DaisyModal',
            'modalradii': 'DaisyModal',
            'swap': 'DaisySwap',
            'card': 'DaisyCard',
            'checkbox': 'DaisyCheckBox',
            'fileinput': 'DaisyFileInput',
            'input': 'DaisyInput',
            'radio': 'DaisyRadio',
            'range': 'DaisyRange',
            'rating': 'DaisyRating',
            'select': 'DaisySelect',
            'textarea': 'DaisyTextArea',
            'toggle': 'DaisyToggle',
            'alert': 'DaisyAlert',
            'badge': 'DaisyBadge',
            'loading': 'DaisyLoading',
            'progress': 'DaisyProgress',
            'skeleton': 'DaisySkeleton',
            'toast': 'DaisyToast',
            'accordion': 'DaisyAccordion',
            'avatar': 'DaisyAvatar',
            'carousel': 'DaisyCarousel',
            'chatbubble': 'DaisyChatBubble',
            'collapse': 'DaisyCollapse',
            'countdown': 'DaisyCountdown',
            'diff': 'DaisyDiff',
            'divider': 'DaisyDivider',
            'kbd': 'DaisyKbd',
            'list': 'DaisyList',
            'stat': 'DaisyStat',
            'table': 'DaisyTable',
            'timeline': 'DaisyTimeline',
            'breadcrumbs': 'DaisyBreadcrumbs',
            'dock': 'DaisyDock',
            'drawer': 'DaisyDrawer',
            'menu': 'DaisyMenu',
            'navbar': 'DaisyNavbar',
            'pagination': 'DaisyPagination',
            'steps': 'DaisySteps',
            'tabs': 'DaisyTabs',
            'statusdot': 'DaisyStatusIndicator',
            'radialprogressbar': 'DaisyRadialProgress',
            'indicator': 'DaisyIndicator',
            'mask': 'DaisyMask',
            'stack': 'DaisyStack',
            'hero': 'DaisyHero',
            'join': 'DaisyJoin',
            'mockup': 'DaisyMockup',
            'hovergallery': 'DaisyHoverGallery',
            'glass': 'DaisyGlass',
            'textrotate': 'DaisyTextRotate',
            # Color Picker controls
            'colorslider': 'DaisyColorSlider',
            'colorwheel': 'DaisyColorWheel',
            'colorgrid': 'DaisyColorGrid',
            'coloreditor': 'DaisyColorEditor',
            'screenpicker': 'DaisyScreenColorPicker',  # matches SectionId in ColorPickerExamples.axaml
            'screencolorpicker': 'DaisyScreenColorPicker',
            'colorpickerdialog': 'DaisyColorPickerDialog',
            'colorpicker': 'DaisyColorPickerDialog',
            # Custom controls (from CustomControls.axaml)
            'modifierkeys': 'DaisyModifierKeys',
            'weathercard': 'DaisyWeatherCard',
            'currentweather': 'DaisyWeatherCurrent',
            'weatherforecast': 'DaisyWeatherForecast',
            'weathermetrics': 'DaisyWeatherMetrics',
        }

        return mappings.get(normalized)

    def _categorize_controls(self, controls: list[ControlInfo],
                            examples_by_control: dict[str, list[ExampleSnippet]]) -> dict[str, list[str]]:
        """Categorize controls based on example files."""
        categories: dict[str, list[str]] = {}

        # Look for all AXAML files that are in the category mapping
        for stem, cat_name in self.category_mapping.items():
            filepath = self.examples_dir / f"{stem}.axaml"
            if not filepath.exists():
                continue
                
            snippets = self.axaml_parser.parse_file(filepath)

            for snippet in snippets:
                control_name = self._section_to_control(snippet.section_id)
                if control_name:
                    if cat_name not in categories:
                        categories[cat_name] = []
                    if control_name not in categories[cat_name]:
                        categories[cat_name].append(control_name)

        return categories


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(
        description="Generate Flowery.NET documentation from curated llms-static/ files.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python Utils/generate_docs.py              # Use curated docs only (default)
  python Utils/generate_docs.py --auto-parse # Include auto-parsed Properties/Examples
        """
    )
    parser.add_argument(
        '--auto-parse',
        action='store_true',
        default=False,
        help='Include auto-parsed Properties, Enums, and Examples from source files'
    )
    args = parser.parse_args()

    script_dir = Path(__file__).parent
    root_dir = script_dir.parent

    generator = DocumentationGenerator(root_dir, auto_parse=args.auto_parse)
    generator.generate()


if __name__ == "__main__":
    main()
