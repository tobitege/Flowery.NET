# Primary Documentation (llms-static/)

This folder holds the **curated, hand-written** documentation for Flowery.NET. Files here are the primary source for both the generated site (`docs/`) and the machine-readable `docs/llms.txt`.

## Default Workflow (curated-only)

1) Create or edit `llms-static/<Control>.md`.
2) Run `python Utils/generate_site.py`.
3) Open `docs/index.html`.

## Optional Enrichment with Auto-Parse

To include generated Properties/Enums/Examples, run:

```bash
python Utils/generate_docs.py --auto-parse   # builds llms/ with metadata + your curated text
python Utils/generate_site.py --use-generated
```

## File Naming

- One markdown file per control, matching the class name: `DaisyButton.md`, `DaisyLoading.md`, etc.
- Files are tracked in git; keep `docs/`and`llms/` out of version control.

## Suggested Template

````markdown
## Overview
What the control does and when to use it.

## Variants
| Variant | Description |
|---------|-------------|
| **Primary** | Main action styling |
| **Ghost**   | Low-emphasis option |

## Sizes
| Size | Description |
|------|-------------|
| Small | Compact |
| Medium | Default |

## Quick Examples
```xml
<controls:DaisyButton Variant="Primary" Content="Primary" />
<controls:DaisyButton Variant="Ghost" Content="Ghost" />
```

---
````

## Guidelines

- Keep it concise; tables work well for variants, sizes, or options.
- Add 2-3 short XAML snippets that show common usage.
- Use `---` at the end to separate curated content from any auto-parsed sections.
- HTML comments (`<!-- -->`) are stripped automatically.
- If you need Properties/Enums/Examples, use the auto-parse workflow above; curated-only mode will not generate those sections for you.

## What Goes Here

- Narrative descriptions and usage guidance.
- Variant/size/color explanations.
- Tips, gotchas, or patterns that are hard to infer from code.
- Minimal runnable snippets that illustrate the control.

The goal: make every control understandable without digging into source code. The generators will pick up whatever you put here and surface it directly in the site and `llms.txt`.
