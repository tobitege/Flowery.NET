#!/usr/bin/env python3
"""
Flowery.NET Static Site Generator

Converts the generated markdown documentation into a static HTML website
suitable for GitHub Pages.

Usage:
    python Utils/generate_site.py

Input (markdown):
    llms/llms.txt            - Main overview
    llms/controls/*.md       - Per-control docs
    llms/categories/*.md     - Category docs

Output (HTML):
    docs/index.html          - Main landing page
    docs/controls/*.html     - Per-control pages
    docs/categories/*.html   - Category pages
    docs/style.css           - Stylesheet

GitHub Pages Setup:
    1. Push the docs/ folder to your repo
    2. Go to Settings → Pages
    3. Source: "Deploy from a branch"
    4. Branch: main (or master), folder: /docs
    5. Save - site will be live in ~1 minute
"""

import re
from pathlib import Path
from typing import Optional


class MarkdownToHtml:
    """Simple markdown to HTML converter."""

    def convert(self, markdown: str) -> str:
        """Convert markdown to HTML."""
        html = markdown

        # Code blocks - extract and replace with placeholders
        code_blocks = []
        def save_code_block(m):
            lang = m.group(1) or "text"
            code = self._escape_html(self._clean_code_block(m.group(2)))
            code_blocks.append(f'<pre><code class="language-{lang}">{code}</code></pre>')
            return f'__CODE_BLOCK_{len(code_blocks) - 1}__'

        html = re.sub(r'```(\w+)?\n(.*?)```', save_code_block, html, flags=re.DOTALL)

        # Inline code
        html = re.sub(r'`([^`]+)`', r'<code>\1</code>', html)

        # Headers
        html = re.sub(r'^### (.+)$', r'<h3>\1</h3>', html, flags=re.MULTILINE)
        html = re.sub(r'^## (.+)$', r'<h2>\1</h2>', html, flags=re.MULTILINE)
        html = re.sub(r'^# (.+)$', r'<h1>\1</h1>', html, flags=re.MULTILINE)

        # Bold and italic
        html = re.sub(r'\*\*(.+?)\*\*', r'<strong>\1</strong>', html)
        html = re.sub(r'\*(.+?)\*', r'<em>\1</em>', html)

        # Tables
        html = self._convert_tables(html)

        # Lists
        html = re.sub(r'^- (.+)$', r'<li>\1</li>', html, flags=re.MULTILINE)
        html = re.sub(r'(<li>.*</li>\n?)+', r'<ul>\g<0></ul>', html)

        # Paragraphs (lines not already wrapped and not placeholders)
        lines = html.split('\n')
        result = []
        for line in lines:
            stripped = line.strip()
            if stripped and not stripped.startswith('<') and not stripped.startswith('__CODE_BLOCK_'):
                result.append(f'<p>{stripped}</p>')
            else:
                result.append(line)
        html = '\n'.join(result)

        # Clean up empty paragraphs
        html = re.sub(r'<p>\s*</p>', '', html)

        # Restore code blocks
        for i, block in enumerate(code_blocks):
            html = html.replace(f'__CODE_BLOCK_{i}__', block)

        return html

    def _escape_html(self, text: str) -> str:
        """Escape HTML entities in code blocks."""
        return (text
                .replace('&', '&amp;')
                .replace('<', '&lt;')
                .replace('>', '&gt;'))

    def _clean_code_block(self, code: str) -> str:
        """Clean up code block content - remove excessive blank lines."""
        lines = code.split('\n')
        # Remove leading/trailing blank lines
        while lines and not lines[0].strip():
            lines.pop(0)
        while lines and not lines[-1].strip():
            lines.pop()
        # Collapse multiple consecutive blank lines into one
        result = []
        prev_blank = False
        for line in lines:
            is_blank = not line.strip()
            if is_blank:
                if not prev_blank:
                    result.append('')
                prev_blank = True
            else:
                result.append(line)
                prev_blank = False
        return '\n'.join(result)

    def _convert_tables(self, html: str) -> str:
        """Convert markdown tables to HTML."""
        lines = html.split('\n')
        result = []
        in_table = False
        table_lines = []

        for line in lines:
            if '|' in line and line.strip().startswith('|'):
                if not in_table:
                    in_table = True
                    table_lines = []
                table_lines.append(line)
            else:
                if in_table:
                    result.append(self._build_table(table_lines))
                    in_table = False
                    table_lines = []
                result.append(line)

        if in_table:
            result.append(self._build_table(table_lines))

        return '\n'.join(result)

    def _build_table(self, lines: list[str]) -> str:
        """Build HTML table from markdown table lines."""
        if len(lines) < 2:
            return '\n'.join(lines)

        html = ['<div class="table-wrapper"><table>']

        # Header row
        header_cells = [c.strip() for c in lines[0].split('|')[1:-1]]
        html.append('<thead><tr>')
        for cell in header_cells:
            html.append(f'<th>{cell}</th>')
        html.append('</tr></thead>')

        # Body rows (skip separator line)
        html.append('<tbody>')
        for line in lines[2:]:
            cells = [c.strip() for c in line.split('|')[1:-1]]
            html.append('<tr>')
            for cell in cells:
                # Convert inline code in cells
                cell = re.sub(r'`([^`]+)`', r'<code>\1</code>', cell)
                html.append(f'<td>{cell}</td>')
            html.append('</tr>')
        html.append('</tbody>')

        html.append('</table></div>')
        return '\n'.join(html)


class SiteGenerator:
    """Generates static HTML site from markdown docs."""

    def __init__(self, docs_dir: Path, output_dir: Path):
        self.docs_dir = docs_dir
        self.output_dir = output_dir
        self.converter = MarkdownToHtml()
        self.controls: list[dict] = []
        self.categories: list[dict] = []

    def generate(self):
        """Generate the complete static site."""
        print("Flowery.NET Site Generator")
        print("=" * 40)

        # Create output directories
        self.output_dir.mkdir(exist_ok=True)
        (self.output_dir / "controls").mkdir(exist_ok=True)
        (self.output_dir / "categories").mkdir(exist_ok=True)

        # Collect all controls
        print("\n[1/4] Scanning control docs...")
        controls_dir = self.docs_dir / "controls"
        for md_file in sorted(controls_dir.glob("*.md")):
            name = md_file.stem
            if name.startswith("Daisy"):
                self.controls.append({
                    'name': name,
                    'file': md_file,
                    'html_name': f"{name}.html"
                })
        print(f"      Found {len(self.controls)} controls")

        # Collect categories
        print("\n[2/4] Scanning category docs...")
        categories_dir = self.docs_dir / "categories"
        for md_file in sorted(categories_dir.glob("*.md")):
            self.categories.append({
                'name': md_file.stem.replace('-', ' ').title(),
                'file': md_file,
                'html_name': f"{md_file.stem}.html"
            })
        print(f"      Found {len(self.categories)} categories")

        # Generate CSS
        print("\n[3/4] Generating stylesheet...")
        self._write_css()

        # Generate HTML pages
        print("\n[4/4] Generating HTML pages...")
        self._generate_index()
        self._generate_control_pages()
        self._generate_category_pages()

        print("\n" + "=" * 40)
        print("Site generated successfully!")
        print(f"Output: {self.output_dir}")
        print(f"Open:   {self.output_dir / 'index.html'}")

    def _write_css(self):
        """Write the stylesheet."""
        css = '''/* Flowery.NET Documentation - Generated Stylesheet */
:root {
    --bg: #0f172a;
    --bg-card: #1e293b;
    --bg-code: #0d1117;
    --text: #e2e8f0;
    --text-muted: #94a3b8;
    --primary: #38bdf8;
    --primary-dim: #0ea5e9;
    --accent: #2dd4bf;
    --border: #334155;
    --font-sans: system-ui, -apple-system, sans-serif;
    --font-mono: 'Cascadia Code', 'Fira Code', Consolas, monospace;
}

* { box-sizing: border-box; margin: 0; padding: 0; }

body {
    font-family: var(--font-sans);
    background: var(--bg);
    color: var(--text);
    line-height: 1.6;
    min-height: 100vh;
}

.layout {
    display: grid;
    grid-template-columns: 260px 1fr;
    min-height: 100vh;
}

/* Sidebar */
.sidebar {
    background: var(--bg-card);
    border-right: 1px solid var(--border);
    padding: 1.5rem;
    position: sticky;
    top: 0;
    height: 100vh;
    overflow-y: auto;
}

.sidebar h1 {
    font-size: 1.25rem;
    color: var(--primary);
    margin-bottom: 0.25rem;
}

.sidebar .subtitle {
    font-size: 0.75rem;
    color: var(--text-muted);
    margin-bottom: 1.5rem;
}

.sidebar h2 {
    font-size: 0.7rem;
    text-transform: uppercase;
    letter-spacing: 0.1em;
    color: var(--text-muted);
    margin: 1.5rem 0 0.5rem;
}

.sidebar ul {
    list-style: none;
}

.sidebar a {
    display: block;
    padding: 0.35rem 0.75rem;
    color: var(--text-muted);
    text-decoration: none;
    font-size: 0.875rem;
    border-radius: 0.375rem;
    transition: all 0.15s;
}

.sidebar a:hover {
    color: var(--text);
    background: rgba(255,255,255,0.05);
}

.sidebar a.active {
    color: var(--primary);
    background: rgba(56, 189, 248, 0.1);
}

/* Main content */
.main {
    padding: 2rem 3rem;
    max-width: 900px;
}

.main h1 {
    font-size: 2rem;
    color: var(--text);
    margin-bottom: 0.5rem;
    border-bottom: 2px solid var(--primary);
    padding-bottom: 0.5rem;
}

.main h2 {
    font-size: 1.35rem;
    color: var(--accent);
    margin: 2rem 0 1rem;
}

.main h3 {
    font-size: 1.1rem;
    color: var(--text);
    margin: 1.5rem 0 0.75rem;
}

.main p {
    margin-bottom: 1rem;
    color: var(--text-muted);
}

.main strong {
    color: var(--text);
}

/* Code */
code {
    font-family: var(--font-mono);
    font-size: 0.85em;
    background: var(--bg-code);
    padding: 0.15em 0.4em;
    border-radius: 0.25rem;
    color: var(--primary);
}

pre {
    background: var(--bg-code);
    border: 1px solid var(--border);
    border-radius: 0.5rem;
    padding: 1rem;
    overflow-x: auto;
    margin: 1rem 0;
}

pre code {
    background: none;
    padding: 0;
    color: var(--text);
    font-size: 0.8rem;
    line-height: 1.5;
}

/* Tables */
.table-wrapper {
    overflow-x: auto;
    margin: 1rem 0;
}

table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.875rem;
}

th, td {
    text-align: left;
    padding: 0.75rem;
    border-bottom: 1px solid var(--border);
}

th {
    background: var(--bg-code);
    color: var(--text);
    font-weight: 600;
}

td {
    color: var(--text-muted);
}

tr:hover td {
    background: rgba(255,255,255,0.02);
}

/* Lists */
ul {
    margin: 1rem 0;
    padding-left: 1.5rem;
}

li {
    margin-bottom: 0.5rem;
    color: var(--text-muted);
}

/* Cards grid for index */
.cards {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
    gap: 1rem;
    margin: 1.5rem 0;
}

.card {
    background: var(--bg-card);
    border: 1px solid var(--border);
    border-radius: 0.5rem;
    padding: 1rem;
    text-decoration: none;
    transition: all 0.15s;
}

.card:hover {
    border-color: var(--primary);
    transform: translateY(-2px);
}

.card h3 {
    color: var(--text);
    font-size: 0.95rem;
    margin: 0 0 0.25rem;
}

.card p {
    color: var(--text-muted);
    font-size: 0.8rem;
    margin: 0;
}

/* Responsive */
@media (max-width: 768px) {
    .layout {
        grid-template-columns: 1fr;
    }
    .sidebar {
        position: relative;
        height: auto;
        border-right: none;
        border-bottom: 1px solid var(--border);
    }
    .main {
        padding: 1.5rem;
    }
}
'''
        (self.output_dir / "style.css").write_text(css, encoding='utf-8')

    def _page_template(self, title: str, content: str, active: str = "") -> str:
        """Generate full HTML page with sidebar."""
        sidebar_html = self._build_sidebar(active)
        css_path = "" if "/" not in active else "../"
        return f'''<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{title} - Flowery.NET</title>
    <link rel="stylesheet" href="{css_path}style.css">
    <!-- Highlight.js for syntax highlighting -->
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/github-dark.min.css">
    <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/languages/xml.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/languages/csharp.min.js"></script>
</head>
<body>
    <div class="layout">
        <nav class="sidebar">
            <h1>Flowery.NET</h1>
            <p class="subtitle">Avalonia UI Components</p>
            {sidebar_html}
        </nav>
        <main class="main">
            {content}
        </main>
    </div>
    <script>hljs.highlightAll();</script>
</body>
</html>'''

    def _build_sidebar(self, active: str) -> str:
        """Build sidebar navigation HTML."""
        lines = []

        # Home link
        home_class = ' class="active"' if active == "index" else ""
        lines.append(f'<a href="index.html"{home_class}>← Home</a>')

        # Categories
        lines.append('<h2>Categories</h2>')
        lines.append('<ul>')
        for cat in self.categories:
            cls = ' class="active"' if active == f"categories/{cat['html_name']}" else ""
            lines.append(f'<li><a href="categories/{cat["html_name"]}"{cls}>{cat["name"]}</a></li>')
        lines.append('</ul>')

        # Controls
        lines.append('<h2>Controls</h2>')
        lines.append('<ul>')
        for ctrl in self.controls:
            cls = ' class="active"' if active == f"controls/{ctrl['html_name']}" else ""
            display_name = ctrl['name'].replace('Daisy', '')
            lines.append(f'<li><a href="controls/{ctrl["html_name"]}"{cls}>{display_name}</a></li>')
        lines.append('</ul>')

        return '\n'.join(lines)

    def _generate_index(self):
        """Generate the main index page."""
        # Read llms.txt for overview content
        llms_content = (self.docs_dir / "llms.txt").read_text(encoding='utf-8')

        # Convert to HTML
        html_content = self.converter.convert(llms_content)

        # Add control cards section
        cards_html = ['<h2>All Controls</h2>', '<div class="cards">']
        for ctrl in self.controls:
            display_name = ctrl['name'].replace('Daisy', '')
            cards_html.append(f'''<a href="controls/{ctrl['html_name']}" class="card">
    <h3>{display_name}</h3>
    <p>{ctrl['name']}</p>
</a>''')
        cards_html.append('</div>')

        full_content = html_content + '\n' + '\n'.join(cards_html)
        page = self._page_template("Documentation", full_content, "index")
        (self.output_dir / "index.html").write_text(page, encoding='utf-8')

    def _generate_control_pages(self):
        """Generate HTML pages for each control."""
        for ctrl in self.controls:
            md_content = ctrl['file'].read_text(encoding='utf-8')
            html_content = self.converter.convert(md_content)
            page = self._page_template(ctrl['name'], html_content, f"controls/{ctrl['html_name']}")
            (self.output_dir / "controls" / ctrl['html_name']).write_text(page, encoding='utf-8')

    def _generate_category_pages(self):
        """Generate HTML pages for each category."""
        for cat in self.categories:
            md_content = cat['file'].read_text(encoding='utf-8')
            html_content = self.converter.convert(md_content)
            page = self._page_template(cat['name'], html_content, f"categories/{cat['html_name']}")
            (self.output_dir / "categories" / cat['html_name']).write_text(page, encoding='utf-8')


def main():
    script_dir = Path(__file__).parent
    root_dir = script_dir.parent
    llms_dir = root_dir / "llms"
    docs_dir = root_dir / "docs"

    if not llms_dir.exists():
        print("Error: llms/ folder not found. Run generate_docs.py first.")
        return

    generator = SiteGenerator(llms_dir, docs_dir)
    generator.generate()


if __name__ == "__main__":
    main()
