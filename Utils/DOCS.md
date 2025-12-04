# Flowery.NET Documentation System

The pipeline is now **curated-first**. Hand-written markdown in `llms-static/` is the primary source. Auto-parsing of source code is optional when you want generated Properties/Enums/Examples.

## At a Glance

```
+--------------------+     +--------------------+
|   llms-static/     | --> |   Static HTML Site |
|   (curated docs)   |     |   (docs/ folder)   |
+--------------------+     +--------------------+
          ^                          ^
          |                          |
   Hand-written             generate_site.py
```

Optional enrichment with auto-parsed metadata:

```
+----------------------+    +-----------------+    +--------------------+
| C# + AXAML (source)  | -> |   llms/ (md)    | -> |   Static HTML Site |
+----------------------+    +-----------------+    +--------------------+
          ^                       ^                         ^
          |                       |                         |
    --auto-parse         generate_docs.py      generate_site.py --use-generated
```

---

## Folder Structure

```
Flowery.NET/
  Flowery.NET/Controls/           # C# control definitions
  Flowery.NET.Gallery/Examples/   # AXAML examples (only used with --auto-parse)
  llms-static/                    # Primary curated docs (tracked)
    README.md
    DaisyLoading.md
    ...
  llms/                           # Optional generated docs (gitignored)
    llms.txt
    controls/
    categories/
  docs/                           # Generated static site (gitignored)
    index.html
    controls/
    categories/
  Utils/                          # Tooling
    generate_docs.py
    generate_site.py
    DOCS.md (this file)
```

---

## Workflows

### Default (curated-only)
1) Write or edit `llms-static/<Control>.md`.
2) `python Utils/generate_site.py`
3) Open `docs/index.html`.

### Optional auto-parse (add Properties/Enums/Examples)
1) `python Utils/generate_docs.py --auto-parse` (creates `llms/` from source + curated content)
2) `python Utils/generate_site.py --use-generated` (renders site from `llms/`)

---

## Scripts

### generate_site.py (default entry point)
- **Default mode:** reads curated docs directly from `llms-static/` and emits `docs/` plus `docs/llms.txt`.
- **Flag:** `--use-generated` switches the input to `llms/` (produced by `generate_docs.py`), which includes auto-parsed Properties/Enums/Examples and categories.
- **Outputs:** `docs/index.html`, `docs/controls/*.html`, optional `docs/categories/*.html`, `docs/style.css`, `docs/llms.txt`.

Run:
```bash
python Utils/generate_site.py
# or
python Utils/generate_site.py --use-generated
```

### generate_docs.py (optional)
- Only required when you want auto-parsed metadata (Properties/Enums/Examples) or category markdown.
- Uses `--auto-parse` to read C# controls and AXAML examples, then merges curated content from `llms-static/` into the generated docs in `llms/`.
- Without `--auto-parse` it still writes `llms/` from curated content but skips Properties/Enums/Examples (the default workflow no longer needs this mode).

Run:
```bash
python Utils/generate_docs.py --auto-parse
```

---

## Quick Start

Curated-only site (recommended default):
```bash
python Utils/generate_site.py
start docs/index.html  # or open/xdg-open
```

Include auto-parsed Properties/Enums/Examples:
```bash
python Utils/generate_docs.py --auto-parse
python Utils/generate_site.py --use-generated
start docs/index.html
```

---

## Curated Documentation (llms-static/)

- One file per control: `llms-static/DaisyButton.md`, `llms-static/DaisyLoading.md`, etc.
- Files are tracked in git and are the **primary source** for the site and `docs/llms.txt`.
- HTML comments (`<!-- -->`) are stripped automatically.

Suggested structure:
````markdown
## Overview
Brief description of what this control does and when to use it.

## Variants
| Variant | Description |
|---------|-------------|
| **Primary** | Main action styling |
| **Ghost**   | Subtle/low-emphasis styling |

## Sizes
| Size | Description |
|------|-------------|
| Small | Compact UI |
| Medium | Default |

## Quick Examples
```xml
<controls:DaisyButton Variant="Primary" Content="Primary" />
<controls:DaisyButton Variant="Ghost" Content="Ghost" />
```

---
````

Best practices:
- Keep it concise; use tables for variants/sizes/colors when helpful.
- Add 2-3 quick XAML snippets for common use.
- Use `---` at the end to separate curated content from any auto-parsed sections.

---

## Adding a New Control (docs-first)

1) Create `llms-static/DaisyNewControl.md` with overview, key variants, and a couple of examples.  
2) Run `python Utils/generate_site.py` to rebuild the site.  
3) If you want generated Properties/Enums/Examples, run:
   ```bash
   python Utils/generate_docs.py --auto-parse
   python Utils/generate_site.py --use-generated
   ```

---

## Troubleshooting

- **Missing docs in site:** Ensure a matching `llms-static/<Control>.md` exists (or run the auto-parse workflow to produce `llms/controls/*.md`).
- **Categories not shown:** Categories come from `llms/categories/`; run `generate_docs.py --auto-parse` to regenerate them.
- **Broken links/404:** Re-run `generate_site.py` so the sidebar/index is rebuilt.
- **Want Properties/Enums/Examples:** Use the auto-parse workflow; curated-only mode will not create those tables automatically.

---

## Related Files

| File | Purpose |
|------|---------|
| `Utils/generate_site.py` | Builds the static site (default: curated docs) |
| `Utils/generate_docs.py` | Optional generator for auto-parsed metadata |
| `llms-static/README.md` | How to write curated docs |
| `.github/workflows/generate-docs.yml` | CI entrypoint |
| `docs/llms.txt` | Machine-readable docs for AI assistants (regenerated) |

---

Last updated: December 2025
