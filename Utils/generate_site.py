#!/usr/bin/env python3
"""
Flowery.NET Static Site Generator

Converts markdown documentation into a static HTML website for GitHub Pages.

Usage:
    python Utils/generate_site.py                # Use curated llms-static/ only (default)
    python Utils/generate_site.py --use-generated # Use llms/ (auto-generated) docs

Input (markdown):
    Default mode (curated):
        llms-static/*.md         - Curated per-control docs
        llms/categories/*.md     - Category docs

    With --use-generated:
        llms/llms.txt            - Main overview
        llms/controls/*.md       - Per-control docs (auto-generated)
        llms/categories/*.md     - Category docs

Output (HTML):
    docs/index.html          - Main landing page
    docs/controls/*.html     - Per-control pages
    docs/categories/*.html   - Category pages
    docs/style.css           - Stylesheet
    docs/llms.txt            - Machine-readable docs for AI assistants

GitHub Pages Setup:
    1. Push the docs/ folder to your repo
    2. Go to Settings → Pages
    3. Source: "Deploy from a branch"
    4. Branch: main (or master), folder: /docs
    5. Save - site will be live in ~1 minute
"""

import argparse
import re
import shutil
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

        # Images - convert ![alt](src) to <img> and fix paths for controls/ subfolder
        def convert_image(m):
            alt = m.group(1)
            src = m.group(2)
            # If src is a local file (not http), prepend ../ for controls/ pages
            if not src.startswith(('http://', 'https://', '/')):
                src = '../' + src
            return f'<img src="{src}" alt="{alt}" style="max-width:100%;height:auto;">'
        html = re.sub(r'!\[([^\]]*)\]\(([^)]+)\)', convert_image, html)

        # Links - convert [text](url) to <a>
        html = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'<a href="\2">\1</a>', html)

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

    def __init__(self, docs_dir: Path, output_dir: Path, curated_dir: Path | None = None):
        self.docs_dir = docs_dir
        self.output_dir = output_dir
        self.curated_dir = curated_dir  # llms-static/ for curated-only mode
        self.converter = MarkdownToHtml()
        self.controls: list[dict] = []
        self.categories: list[dict] = []
        self.use_curated_only = curated_dir is not None

    def generate(self):
        """Generate the complete static site."""
        print("Flowery.NET Site Generator")
        print("=" * 40)
        if self.use_curated_only:
            print("Mode: CURATED ONLY (llms-static/)")
        else:
            print("Mode: GENERATED (llms/)")

        # Create output directories
        self.output_dir.mkdir(exist_ok=True)
        (self.output_dir / "controls").mkdir(exist_ok=True)
        (self.output_dir / "categories").mkdir(exist_ok=True)

        # Collect all controls
        print("\n[1/4] Scanning control docs...")
        if self.use_curated_only:
            # Read directly from llms-static/
            for md_file in sorted(self.curated_dir.glob("Daisy*.md")):
                name = md_file.stem
                self.controls.append({
                    'name': name,
                    'file': md_file,
                    'html_name': f"{name}.html"
                })
        else:
            # Read from llms/controls/
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

        # Collect categories (always from llms/categories/)
        print("\n[2/4] Scanning category docs...")
        categories_dir = self.docs_dir / "categories"
        if categories_dir.exists():
            for md_file in sorted(categories_dir.glob("*.md")):
                self.categories.append({
                    'name': md_file.stem.replace('-', ' ').title(),
                    'file': md_file,
                    'html_name': f"{md_file.stem}.html"
                })
            print(f"      Found {len(self.categories)} categories")
        else:
            print("      No categories folder found (run generate_docs.py first)")

        # Copy images from llms-static/ to docs/
        print("\n[3/5] Copying images...")
        self._copy_images()

        # Generate CSS
        print("\n[4/5] Generating stylesheet...")
        self._write_css()

        # Generate HTML pages
        print("\n[5/5] Generating HTML pages...")
        self._generate_shell()
        self._generate_home()
        self._generate_control_pages()
        self._generate_category_pages()

        print("\n" + "=" * 40)
        print("Site generated successfully!")
        print(f"Output: {self.output_dir}")
        print(f"Open:   {self.output_dir / 'index.html'}")

    def _copy_images(self):
        """Copy image files from llms-static/ to docs/ for HTML pages."""
        if not self.curated_dir:
            return
        image_extensions = ['*.gif', '*.png', '*.jpg', '*.jpeg', '*.webp', '*.svg']
        copied = 0
        for ext in image_extensions:
            for img_file in self.curated_dir.glob(ext):
                dest = self.output_dir / img_file.name
                shutil.copy2(img_file, dest)
                copied += 1
        print(f"      Copied {copied} image(s)")

    def _write_css(self):
        """Write the stylesheet."""
        css = ''':root {
    /* Dark Theme (Default) - Neutral/Zinc Palette */
    --bg: #0a0a0a;
    --bg-card: #171717;
    --bg-code: #262626;
    --text: #e5e5e5;
    --text-muted: #a3a3a3;
    --primary: #38bdf8;
    --primary-dim: #0ea5e9;
    --accent: #2dd4bf;
    --border: #404040;
    --link: #38bdf8;
    --link-visited: #7dd3fc;
    --font-sans: system-ui, -apple-system, sans-serif;
    --font-mono: 'Cascadia Code', 'Fira Code', Consolas, monospace;
}

[data-theme="light"] {
    --bg: #ffffff;
    --bg-card: #f9fafb;
    --bg-code: #f3f4f6;
    --text: #1f2937;
    --text-muted: #6b7280;
    --primary: #0284c7;
    --primary-dim: #0369a1;
    --accent: #0d9488;
    --border: #e5e7eb;
    --link: #0284c7;
    --link-visited: #0369a1;
}

* { box-sizing: border-box; margin: 0; padding: 0; }

body {
    font-family: var(--font-sans);
    background: var(--bg);
    color: var(--text);
    line-height: 1.6;
    min-height: 100vh;
    transition: background-color 0.3s, color 0.3s;
}

/* Links */
a {
    color: var(--link);
    text-decoration: none;
    transition: color 0.2s;
}

a:visited {
    color: var(--link-visited);
}

a:hover {
    color: var(--primary-dim);
    text-decoration: underline;
}

/* Shell Layout (index.html) */
.shell {
    display: grid;
    grid-template-columns: 260px 1fr;
    height: 100vh;
    overflow: hidden;
}

/* Sidebar */
.sidebar {
    background: var(--bg-card);
    border-right: 1px solid var(--border);
    padding: 1.5rem;
    overflow-y: auto;
    height: 100%;
}

.sidebar h1 {
    font-size: 1.25rem;
    color: var(--primary);
    margin-bottom: 0.25rem;
    align-items: center;
    gap: 0.5rem;
    display: flex;
    justify-content: space-between;
}

.brand {
    display: flex;
    align-items: center;
    gap: 0.75rem;
}

.theme-toggle {
    background: none;
    border: none;
    color: var(--text-muted);
    cursor: pointer;
    padding: 0.4rem;
    border-radius: 0.5rem;
    transition: all 0.2s;
    display: flex;
    align-items: center;
    justify-content: center;
}

.theme-toggle:hover {
    color: var(--primary);
    background: rgba(255, 255, 255, 0.1);
}

[data-theme="light"] .theme-toggle:hover {
    background: rgba(0, 0, 0, 0.05);
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
    margin: 0;
    padding: 0;
}

.sidebar li {
    margin-bottom: 0.2rem;
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

/* Viewer (Iframe) */
.viewer {
    width: 100%;
    height: 100%;
    border: none;
    background: var(--bg);
}

/* Content Pages (inside iframe) */
.content-body {
    padding: 2rem 3rem;
    max-width: 900px;
    margin: 0 auto;
}

.content-body h1 {
    font-size: 2rem;
    color: var(--text);
    margin-bottom: 0.5rem;
    border-bottom: 2px solid var(--primary);
    padding-bottom: 0.5rem;
}

.content-body h2 {
    font-size: 1.35rem;
    color: var(--accent);
    margin: 2rem 0 1rem;
}

.content-body h3 {
    font-size: 1.1rem;
    color: var(--text);
    margin: 1.5rem 0 0.75rem;
}

.content-body p {
    margin-bottom: 1rem;
    color: var(--text-muted);
}

.content-body strong {
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

/* Lists in content */
.content-body ul {
    margin: 1rem 0;
    padding-left: 1.5rem;
}

.content-body li {
    margin-bottom: 0.5rem;
    color: var(--text-muted);
}

/* Cards grid for home */
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

/* LLM documentation link */
.llm-link {
    background: var(--bg-card);
    border: 1px solid var(--border);
    border-left: 4px solid var(--accent);
    border-radius: 0.5rem;
    padding: 1rem 1.5rem;
    margin: 2rem 0;
}

.llm-link h2 {
    margin: 0 0 0.5rem;
    font-size: 1rem;
    color: var(--accent);
}

.llm-link p {
    margin: 0;
    color: var(--text-muted);
}

.llm-link a {
    color: var(--primary);
}

.github-link {
    color: var(--text-muted);
    text-decoration: none;
    transition: color 0.2s;
    display: inline-flex;
    line-height: 1;
}

.github-link:hover {
    color: var(--primary);
}

.github-icon {
    display: block;
}

/* Index page footer */
.footer-separator {
    margin: 3rem 0 1.5rem;
    border: none;
    border-top: 1px solid var(--border);
}

.index-footer {
    text-align: center;
    padding: 1rem 0 2rem;
    color: var(--text-muted);
}

.index-footer p {
    margin: 0 0 1rem;
    font-size: 0.9rem;
}

.index-footer .disclaimer {
    font-size: 0.8rem;
    opacity: 0.7;
    margin-bottom: 0.5rem;
}

.footer-links {
    display: flex;
    justify-content: center;
    gap: 1.5rem;
    flex-wrap: wrap;
}

.footer-links a {
    display: flex;
    align-items: center;
    gap: 0.4rem;
    color: var(--text-muted);
    text-decoration: none;
    font-size: 0.85rem;
    transition: color 0.2s;
}

.footer-links a:hover {
    color: var(--primary);
}

.footer-icon {
    flex-shrink: 0;
}

/* Breadcrumbs & Navigation */
.breadcrumbs {
    font-size: 0.85rem;
    color: var(--text-muted);
    margin-bottom: 2rem;
}

.breadcrumbs a {
    color: var(--text-muted);
    text-decoration: none;
    transition: color 0.2s;
}

.breadcrumbs a:hover {
    color: var(--primary);
}

.doc-nav {
    display: flex;
    justify-content: space-between;
    margin-top: 4rem;
    padding-top: 2rem;
    border-top: 1px solid var(--border);
}

.doc-nav a {
    display: inline-block;
    padding: 0.75rem 1.25rem;
    border-radius: 0.5rem;
    background: var(--bg-card);
    border: 1px solid var(--border);
    color: var(--text);
    text-decoration: none;
    font-size: 0.9rem;
    transition: all 0.2s;
}

.doc-nav a:hover {
    border-color: var(--primary);
    transform: translateY(-2px);
}

.nav-prev {
    margin-right: auto;
}

.nav-next {
    margin-left: auto;
}

/* Responsive */
.menu-toggle {
    display: none;
    position: fixed;
    top: 1rem;
    right: 1rem;
    z-index: 2000;
    background: var(--bg-card);
    border: 1px solid var(--border);
    color: var(--text);
    padding: 0.5rem;
    border-radius: 0.5rem;
    cursor: pointer;
    box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
}

.overlay {
    display: none;
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(0,0,0,0.5);
    z-index: 900;
    backdrop-filter: blur(2px);
}

@media (max-width: 768px) {
    .shell {
        grid-template-columns: 1fr;
    }

    .sidebar {
        /* Mobile sidebar hidden by default (off-canvas) */
        position: fixed;
        top: 0;
        left: 0;
        bottom: 0;
        width: 260px;
        z-index: 1000;
        transform: translateX(-100%);
        transition: transform 0.3s ease-in-out;
        box-shadow: 4px 0 24px rgba(0,0,0,0.5);
    }

    .sidebar.open {
        transform: translateX(0);
    }

    .menu-toggle {
        display: block;
    }

    .overlay.open {
        display: block;
    }

    .content-body {
        padding: 1.5rem;
        padding-top: 4rem; /* Space for toggle button */
    }
}
'''
        (self.output_dir / "style.css").write_text(css, encoding='utf-8')

    def _page_template(self, title: str, content: str, depth: int = 0) -> str:
        """Generate HTML page for content (loaded in iframe)."""
        css_prefix = "../" * depth
        return f'''<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{title}</title>
    <link rel="stylesheet" href="{css_prefix}style.css">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/github-dark.min.css">
    <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/languages/xml.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/languages/csharp.min.js"></script>
</head>
<body class="content-body">
    {content}
    <script>
        hljs.highlightAll();

        // Theme Sync Logic for Iframe Content
        function applyTheme(theme) {{
            document.documentElement.setAttribute('data-theme', theme);
        }}

        // 1. Initial Load: Try to get theme from localStorage
        const savedTheme = localStorage.getItem('theme') || 'dark';
        applyTheme(savedTheme);

        // 2. Listen for messages from parent (shell)
        window.addEventListener('message', (event) => {{
            if (event.data && event.data.type === 'setTheme') {{
                applyTheme(event.data.theme);
            }}
        }});
    </script>
</body>
</html>'''

    def _generate_shell(self):
        """Generate the main app shell (index.html) with sidebar and iframe."""
        
        sidebar_items = []
        
        # Home link
        sidebar_items.append('<li><a href="home.html" target="viewer" class="active">← Home</a></li>')

        # Categories
        if self.categories:
            sidebar_items.append('<li><h2>Categories</h2></li>')
            for cat in self.categories:
                sidebar_items.append(f'<li><a href="categories/{cat["html_name"]}" target="viewer">{cat["name"]}</a></li>')

        # Controls
        sidebar_items.append('<li><h2>Controls</h2></li>')
        for ctrl in self.controls:
            display_name = ctrl['name'].replace('Daisy', '')
            sidebar_items.append(f'<li><a href="controls/{ctrl["html_name"]}" target="viewer">{display_name}</a></li>')

        sidebar_html = '\n'.join(sidebar_items)

        html = f'''<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Flowery.NET Documentation</title>
    <link rel="stylesheet" href="style.css">
</head>
<body>
    <div class="shell">
        <div class="overlay"></div>
        <button class="menu-toggle" aria-label="Toggle Menu">
            <svg viewBox="0 0 24 24" width="24" height="24" stroke="currentColor" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round">
                <line x1="3" y1="12" x2="21" y2="12"></line>
                <line x1="3" y1="6" x2="21" y2="6"></line>
                <line x1="3" y1="18" x2="21" y2="18"></line>
            </svg>
        </button>

        <nav class="sidebar">
            <h1>
                <div class="brand">
                    <a href="https://github.com/tobitege/Flowery.NET" target="_blank" rel="noopener" class="github-link" title="View on GitHub">
                        <svg class="github-icon" viewBox="0 0 16 16" width="20" height="20">
                            <path fill="currentColor" d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/>
                        </svg>
                    </a> 
                    <span>Flowery.NET</span>
                </div>
                <button class="theme-toggle" aria-label="Toggle Theme" title="Toggle Theme">
                    <!-- Sun Icon (for Dark mode) -->
                    <svg class="sun-icon" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="display: none;">
                        <circle cx="12" cy="12" r="5"></circle>
                        <line x1="12" y1="1" x2="12" y2="3"></line>
                        <line x1="12" y1="21" x2="12" y2="23"></line>
                        <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
                        <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
                        <line x1="1" y1="12" x2="3" y2="12"></line>
                        <line x1="21" y1="12" x2="23" y2="12"></line>
                        <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
                        <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
                    </svg>
                    <!-- Moon Icon (for Light mode) -->
                    <svg class="moon-icon" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
                    </svg>
                </button>
            </h1>
            <p class="subtitle">Avalonia UI Components</p>
            <ul>
                {sidebar_html}
            </ul>
        </nav>
        <iframe name="viewer" class="viewer" src="home.html"></iframe>
    </div>

    <script>
        // --- Theme Logic ---
        const themeToggle = document.querySelector('.theme-toggle');
        const sunIcon = document.querySelector('.sun-icon');
        const moonIcon = document.querySelector('.moon-icon');
        const iframe = document.querySelector('iframe');

        // Check local storage or default to dark
        const currentTheme = localStorage.getItem('theme') || 'dark';
        applyTheme(currentTheme);

        function applyTheme(theme) {{
            document.documentElement.setAttribute('data-theme', theme);
            
            // Update icons
            if (theme === 'dark') {{
                sunIcon.style.display = 'block';
                moonIcon.style.display = 'none';
            }} else {{
                sunIcon.style.display = 'none';
                moonIcon.style.display = 'block';
            }}

            // Sync iframe (Direct access + PostMessage fallback for local files)
            try {{
                // 1. Try direct access (works for same origin)
                if (iframe.contentDocument && iframe.contentDocument.documentElement) {{
                    iframe.contentDocument.documentElement.setAttribute('data-theme', theme);
                }}
            }} catch(e) {{
                // console.log('Direct access restricted');
            }}
            
            // 2. PostMessage (works for cross-origin/local files)
            try {{
                if (iframe.contentWindow) {{
                    iframe.contentWindow.postMessage({{ type: 'setTheme', theme: theme }}, '*');
                }}
            }} catch(e) {{}}
        }}

        themeToggle.addEventListener('click', () => {{
            const current = document.documentElement.getAttribute('data-theme');
            const newTheme = current === 'dark' ? 'light' : 'dark';
            
            localStorage.setItem('theme', newTheme);
            applyTheme(newTheme);
        }});

        // When iframe loads, ensure it gets the theme
        iframe.addEventListener('load', () => {{
            const theme = localStorage.getItem('theme') || 'dark';
            applyTheme(theme);
        }});

        // --- Sidebar Logic ---
        const sidebar = document.querySelector('.sidebar');
        const overlay = document.querySelector('.overlay');
        const toggleBtn = document.querySelector('.menu-toggle');
        const links = document.querySelectorAll('.sidebar a');

        function toggleMenu() {{
            sidebar.classList.toggle('open');
            overlay.classList.toggle('open');
        }}

        function closeMenu() {{
            sidebar.classList.remove('open');
            overlay.classList.remove('open');
        }}

        toggleBtn.addEventListener('click', toggleMenu);
        overlay.addEventListener('click', closeMenu);

        // Handle active state & mobile close
        links.forEach(link => {{
            link.addEventListener('click', (e) => {{
                // Don't mess with external links
                if (link.target === '_blank') return;
                
                links.forEach(l => l.classList.remove('active'));
                link.classList.add('active');

                // Close menu on mobile when link is clicked
                if (window.innerWidth <= 768) {{
                    closeMenu();
                }}
            }});
        }});
    </script>
</body>
</html>'''
        (self.output_dir / "index.html").write_text(html, encoding='utf-8')

    def _generate_home(self):
        """Generate the home content page (home.html)."""
        # Generate llms.txt for AI assistants (combine all curated docs)
        if self.use_curated_only:
            llms_content = self._generate_llms_txt_from_curated()
        else:
            llms_content = (self.docs_dir / "llms.txt").read_text(encoding='utf-8')

        # Convert to HTML
        html_content = self.converter.convert(llms_content)

        # Insert LLM documentation link after Quick Start section
        llm_link_html = '''<div class="llm-link">
    <h2>For AI Assistants</h2>
    <p>📄 <a href="llms.txt"><strong>llms.txt</strong></a> — Machine-readable documentation in plain markdown format, optimized for LLMs and AI code assistants.</p>
</div>
'''
        # Insert after Quick Start (after the first </pre> which closes the code block)
        if '</pre>' in html_content:
            insert_pos = html_content.find('</pre>') + len('</pre>')
            html_content = html_content[:insert_pos] + '\n' + llm_link_html + html_content[insert_pos:]

        # Add control cards section
        cards_html = ['<h2>All Controls</h2>', '<div class="cards">']
        for ctrl in self.controls:
            display_name = ctrl['name'].replace('Daisy', '')
            cards_html.append(f'''<a href="controls/{ctrl['html_name']}" class="card">
    <h3>{display_name}</h3>
    <p>{ctrl['name']}</p>
</a>''')
        cards_html.append('</div>')

        # Add footer with project links
        footer_html = '''
<hr class="footer-separator">
<footer class="index-footer">
    <p class="disclaimer">This project is not affiliated with, endorsed by, or sponsored by DaisyUI or Avalonia UI.</p>
    <p>Built with inspiration from:</p>
    <div class="footer-links">
        <a href="https://daisyui.com" target="_blank" rel="noopener" title="DaisyUI">
            <svg class="footer-icon" viewBox="0 0 24 24" width="24" height="24"><path fill="currentColor" d="M12 2C6.477 2 2 6.477 2 12s4.477 10 10 10 10-4.477 10-10S17.523 2 12 2zm0 18c-4.411 0-8-3.589-8-8s3.589-8 8-8 8 3.589 8 8-3.589 8-8 8zm-1-13h2v6h-2zm0 8h2v2h-2z"/></svg>
            <span>DaisyUI</span>
        </a>
        <a href="https://avaloniaui.net" target="_blank" rel="noopener" title="Avalonia UI">
            <svg class="footer-icon" viewBox="0 0 24 24" width="24" height="24"><path fill="currentColor" d="M12 2L2 19h20L12 2zm0 4l7 11H5l7-11z"/></svg>
            <span>Avalonia UI</span>
        </a>
        <a href="https://github.com/saadeghi/daisyui" target="_blank" rel="noopener" title="DaisyUI GitHub">
            <svg class="footer-icon" viewBox="0 0 16 16" width="20" height="20"><path fill="currentColor" d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/></svg>
            <span>daisyui</span>
        </a>
        <a href="https://github.com/AvaloniaUI/Avalonia" target="_blank" rel="noopener" title="Avalonia GitHub">
            <svg class="footer-icon" viewBox="0 0 16 16" width="20" height="20"><path fill="currentColor" d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/></svg>
            <span>Avalonia</span>
        </a>
    </div>
</footer>
'''

        full_content = html_content + '\n' + '\n'.join(cards_html) + footer_html
        page = self._page_template("Documentation", full_content, depth=0)
        (self.output_dir / "home.html").write_text(page, encoding='utf-8')

    def _generate_llms_txt_from_curated(self) -> str:
        """Generate a master llms.txt from curated docs."""
        lines = []
        lines.append("# Flowery.NET Component Library")
        lines.append("")
        lines.append("Flowery.NET is an Avalonia UI component library inspired by DaisyUI.")
        lines.append("It provides styled controls for building modern desktop applications.")
        lines.append("")
        lines.append("## Quick Start")
        lines.append("")
        lines.append("Add the namespace to your AXAML:")
        lines.append("```xml")
        lines.append('xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"')
        lines.append("```")
        lines.append("")
        lines.append("")
        lines.append("## Common Patterns")
        lines.append("")
        lines.append("### Variants")
        lines.append("Most controls support a `Variant` property:")
        lines.append("- `Primary`, `Secondary`, `Accent` - Brand colors")
        lines.append("- `Info`, `Success`, `Warning`, `Error` - Status colors")
        lines.append("- `Neutral`, `Ghost`, `Link` - Subtle styles")
        lines.append("")
        lines.append("### Sizes")
        lines.append("Controls support a `Size` property:")
        lines.append("`ExtraSmall`, `Small`, `Medium` (default), `Large`, `ExtraLarge`")
        lines.append("")
        lines.append("### Theming")
        lines.append("Use `DaisyThemeManager` to switch themes:")
        lines.append("```csharp")
        lines.append('DaisyThemeManager.ApplyTheme("dracula");')
        lines.append("```")
        lines.append("")
        lines.append("Available themes: light, dark, cupcake, bumblebee, emerald, corporate,")
        lines.append("synthwave, retro, cyberpunk, valentine, halloween, garden, forest,")
        lines.append("aqua, lofi, pastel, fantasy, wireframe, black, luxury, dracula, cmyk,")
        lines.append("autumn, business, acid, lemonade, night, coffee, winter, dim, nord, sunset")
        lines.append("")
        return '\n'.join(lines)

    def _generate_control_pages(self):
        """Generate HTML pages for each control."""
        # 1. Build map of control -> category
        control_category_map = {}
        category_controls_map = {} # cat_name -> list of controls
        
        # Parse categories to find which controls belong where
        for cat in self.categories:
            cat_content = cat['file'].read_text(encoding='utf-8')
            # Extract control names from list items
            # - **[DaisyButton](../controls/DaisyButton.html)**
            found_controls = re.findall(r'\*\*\[?(Daisy\w+)', cat_content)
            category_controls_map[cat['name']] = found_controls
            for ctrl_name in found_controls:
                control_category_map[ctrl_name] = cat

        for ctrl in self.controls:
            md_content = ctrl['file'].read_text(encoding='utf-8')
            # Strip HTML comments from curated docs
            md_content = re.sub(r'<!--.*?-->', '', md_content, flags=re.DOTALL)
            
            # Fix Headings: If it starts with "# Overview", demote it and add proper title
            stripped_content = md_content.strip()
            if stripped_content.startswith('# Overview'):
                # Replace the first occurrence
                md_content = md_content.replace('# Overview', f'# {ctrl["name"]}\n\n## Overview', 1)
            elif not stripped_content.startswith('# '):
                # If no H1 at all, add one
                md_content = f"# {ctrl['name']}\n\n{md_content}"

            html_content = self.converter.convert(md_content)

            # --- Navigation & Breadcrumbs ---
            nav_html = ""
            category = control_category_map.get(ctrl['name'])
            
            if category:
                # Breadcrumbs
                nav_html += f'''<div class="breadcrumbs">
    <a href="../home.html">Home</a> &gt; 
    <a href="../categories/{category["html_name"]}">{category["name"]}</a>
</div>'''
                
                # Prev/Next
                siblings = category_controls_map.get(category['name'], [])
                try:
                    idx = siblings.index(ctrl['name'])
                    links = []
                    
                    if idx > 0:
                        prev_name = siblings[idx-1]
                        links.append(f'<a href="{prev_name}.html" class="nav-prev">← {prev_name.replace("Daisy", "")}</a>')
                    else:
                         links.append('<span></span>') # Spacer

                    if idx < len(siblings) - 1:
                        next_name = siblings[idx+1]
                        links.append(f'<a href="{next_name}.html" class="nav-next">{next_name.replace("Daisy", "")} →</a>')
                    else:
                        links.append('<span></span>') # Spacer

                    if any(l != '<span></span>' for l in links):
                        nav_html += f'<div class="doc-nav">{"".join(links)}</div>'
                except ValueError:
                    pass # Control not found in its category list (shouldn't happen if map is built correct)

            # Inject nav at top (breadcrumbs) and bottom (prev/next)
            # Find the end of content to append bottom nav
            full_page_content = nav_html.split('<div class="doc-nav">')[0] + html_content # Breadcrumbs + Content
            if '<div class="doc-nav">' in nav_html:
                 full_page_content += nav_html.split('</div>')[-2] + '</div>' # Append doc-nav

            # Actually, let's keep it simple: Breadcrumbs top, Nav bottom
            breadcrumbs = f'''<div class="breadcrumbs">
    <a href="../home.html">Home</a> &gt; 
    <a href="../categories/{category["html_name"]}">{category["name"]}</a>
</div>''' if category else f'<div class="breadcrumbs"><a href="../home.html">Home</a></div>'

            prev_next = ""
            if category:
                 siblings = category_controls_map.get(category['name'], [])
                 if ctrl['name'] in siblings:
                    idx = siblings.index(ctrl['name'])
                    prev_link = f'<a href="{siblings[idx-1]}.html">← {siblings[idx-1].replace("Daisy", "")}</a>' if idx > 0 else ""
                    next_link = f'<a href="{siblings[idx+1]}.html">{siblings[idx+1].replace("Daisy", "")} →</a>' if idx < len(siblings) - 1 else ""
                    
                    if prev_link or next_link:
                        prev_next = f'''<div class="doc-nav">
    <div class="nav-left">{prev_link}</div>
    <div class="nav-right">{next_link}</div>
</div>'''

            final_content = breadcrumbs + html_content + prev_next
            
            page = self._page_template(ctrl['name'], final_content, depth=1)
            (self.output_dir / "controls" / ctrl['html_name']).write_text(page, encoding='utf-8')

    def _generate_category_pages(self):
        """Generate HTML pages for each category."""
        for cat in self.categories:
            md_content = cat['file'].read_text(encoding='utf-8')
            html_content = self.converter.convert(md_content)
            page = self._page_template(cat['name'], html_content, depth=1)
            (self.output_dir / "categories" / cat['html_name']).write_text(page, encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(
        description="Generate Flowery.NET static documentation site.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python Utils/generate_site.py                # Use curated llms-static/ only (default)
  python Utils/generate_site.py --use-generated # Use llms/ (auto-generated) docs
        """
    )
    parser.add_argument(
        '--use-generated',
        action='store_true',
        default=False,
        help='Use llms/ (auto-generated) docs instead of curated llms-static/'
    )
    args = parser.parse_args()

    script_dir = Path(__file__).parent
    root_dir = script_dir.parent
    llms_dir = root_dir / "llms"
    curated_dir = root_dir / "llms-static"
    docs_dir = root_dir / "docs"

    if args.use_generated:
        # Use auto-generated llms/ folder
        if not llms_dir.exists():
            print("Error: llms/ folder not found. Run generate_docs.py --auto-parse first.")
            return
        generator = SiteGenerator(llms_dir, docs_dir, curated_dir=None)
    else:
        # Use curated llms-static/ folder (default)
        if not curated_dir.exists():
            print("Error: llms-static/ folder not found.")
            return
        generator = SiteGenerator(llms_dir, docs_dir, curated_dir=curated_dir)

    generator.generate()


if __name__ == "__main__":
    main()
