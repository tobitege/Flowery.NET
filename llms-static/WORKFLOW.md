# Supplementary Documentation Workflow

This document provides step-by-step instructions for creating supplementary documentation files for Flowery.NET controls. Follow these steps exactly.

---

## TASK OVERVIEW

You are creating markdown files that provide **human-friendly explanations** of Avalonia UI controls. These files enhance the auto-generated technical documentation with:

- Plain-English descriptions of what each variant/option does
- Visual/behavioral descriptions (since you can't see the actual UI)
- Tables explaining enum values and their effects
- Practical usage examples
- Tips and best practices

**Output location:** `llms-static/` folder
**File naming:** `DaisyControlName.md` (must match the C# class name exactly!)

---

## STEP 1: Identify the Control

Before writing documentation, you need to gather information from these sources:

### 1.1 Read the C# Control File

**Location:** `Flowery.NET/Controls/DaisyControlName.cs`

Extract:

- Class name (e.g., `DaisyButton`)
- Base class (what it inherits from)
- All `StyledProperty` definitions
- All enum definitions (usually at the top of the file)
- XML documentation comments (`/// <summary>`)

### 1.2 Read the AXAML Theme File (if exists)

**Location:** `Flowery.NET/Themes/DaisyControlName.axaml`

Look for:

- Visual structure (what elements make up the control)
- Animations (look for `<Animation>`or`<Storyboard>` tags)
- Style variations (different appearances based on properties)

### 1.3 Read the Example File

**Location:**`Flowery.NET.Gallery/Examples/*Examples.axaml`

Find the section for this control by searching for:

- `SectionId="controlname"` in a SectionHeader
- `<controls:DaisyControlName` elements

Extract:

- How the control is used in practice
- Which property combinations are shown
- Common patterns

---

## STEP 2: Determine What Needs Documentation

**Ask these questions:**

| Question | If YES → Document |
|----------|-------------------|
| Does it have multiple **variants** (enum with visual differences)? | Explain what each variant looks like/does |
| Does it have **size** options? | Table with sizes and use cases |
| Does it have **color** options? | Table with color meanings |
| Does it have **behavior modes**? | Explain when to use each |
| Is it a **container** for other controls? | Show composition patterns |
| Does it have **animations**? | Describe timing and visual effect |
| Are there **non-obvious usage patterns**? | Add tips/best practices |

**Skip creating a supplementary doc if:**

- The control is very simple (just a styled wrapper)
- The property names are self-explanatory
- There are no variants, just basic properties

---

## STEP 3: Write the Documentation

### 3.1 File Header (Required)

```markdown
<!-- Supplementary documentation for DaisyControlName -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->
```

### 3.2 Overview Section (Required)

Write a brief paragraph explaining:

- What the control does
- Key features (number of variants, size options, etc.)
- When to use this control

**Example:**

```markdown
## Overview

DaisyButton provides styled buttons with **8 color variants**, **5 style variants**, and **5 size options**. Supports icons, loading states, and can be used for actions, navigation, or form submission.
```

### 3.3 Variant Tables (If Applicable)

For each enum property that affects appearance or behavior:

```markdown
## Variant Options

| Variant | Description |
|---------|-------------|
| **Default** | Standard appearance, inherits theme styling |
| **Primary** | High emphasis, main actions |
| **Secondary** | Medium emphasis, alternative actions |
```

**Guidelines for descriptions:**

- Describe the VISUAL appearance or BEHAVIOR, not just restate the name
- Use concrete terms: "rounded corners", "bold text", "subtle shadow"
- Compare to familiar patterns: "like a tooltip", "similar to a badge"

### 3.4 Size Tables (If Applicable)

```markdown
## Size Options

| Size | Approximate Dimensions | Use Case |
|------|------------------------|----------|
| ExtraSmall | ~16px height | Inline text, compact UIs |
| Small | ~20px height | Toolbars, secondary actions |
| Medium | ~24px height | Default, general purpose |
| Large | ~32px height | Primary actions, emphasis |
| ExtraLarge | ~40px height | Hero sections, full-width |
```

### 3.5 Color Tables (If Applicable)

```markdown
## Color Options

| Color | Semantic Meaning |
|-------|------------------|
| Default | Inherits from theme (usually base-content) |
| Primary | Brand color, main actions |
| Secondary | Alternative brand color |
| Accent | Highlight, special emphasis |
| Neutral | Muted, low emphasis |
| Info | Informational (typically blue) |
| Success | Positive outcomes (typically green) |
| Warning | Caution needed (typically yellow/orange) |
| Error | Problems, destructive actions (typically red) |
```

### 3.6 Quick Examples (Required)

Show practical AXAML usage:

```markdown
## Quick Examples

\`\`\`xml
<!-- Basic usage -->
<controls:DaisyButton Content="Click me" />

<!-- With variant -->
<controls:DaisyButton Content="Save" Variant="Primary" />

<!-- With size -->
<controls:DaisyButton Content="Small" Size="Small" />

<!-- Combining options -->
<controls:DaisyButton Content="Delete" Variant="Error" Size="Large" />
\`\`\`
```

**Guidelines for examples:**

- Start with simplest usage
- Progress to more complex combinations
- Use realistic Content/text values
- Show 4-8 examples (not too few, not overwhelming)

### 3.7 Additional Sections (As Needed)

**For controls with animations:**

```markdown
## Animation Timing

| Animation | Duration | Notes |
|-----------|----------|-------|
| Fade in | 0.2s | On hover |
| Slide | 0.3s | Panel transition |
```

**For container controls:**

```markdown
## Content Structure

DaisyCard expects this structure:
- Header (optional): Title area
- Body: Main content
- Actions (optional): Buttons/links

\`\`\`xml
<controls:DaisyCard>
    <controls:DaisyCard.Header>Title</controls:DaisyCard.Header>
    <TextBlock>Card content here</TextBlock>
    <controls:DaisyCard.Actions>
        <controls:DaisyButton Content="Action" />
    </controls:DaisyCard.Actions>
</controls:DaisyCard>
\`\`\`
```

**For controls with special behaviors:**

```markdown
## Tips & Best Practices

- Use `IsEnabled="False"` to disable interaction while preserving visual state
- Combine with `DaisyTooltip` for additional context
- For forms, set `IsDefault="True"` on the primary action button
```

### 3.8 Closing Divider (Required)

Always end with a horizontal rule to separate from auto-generated content:

```markdown
---
```

---

## STEP 4: Verify the Output

### 4.1 Checklist

- [ ] File is named exactly `DaisyControlName.md` (matches C# class name)
- [ ] File is in `llms-static/` folder
- [ ] Has HTML comment header
- [ ] Has `## Overview` section
- [ ] Tables have `|` aligned properly (though not strictly required)
- [ ] Code examples use `controls:` namespace prefix
- [ ] Ends with `---`

### 4.2 Common Mistakes to Avoid

| Mistake | Correct Approach |
|---------|------------------|
| "Primary variant makes it primary" | "Primary variant uses the brand color with high visual emphasis" |
| Copying XML comments verbatim | Expand and explain in human terms |
| Missing namespace in examples | Always use `controls:DaisyControlName` |
| Documenting every property | Focus on variants/enums that need explanation |
| Very long descriptions | Keep table cells to 1-2 sentences max |

---

## COMPLETE EXAMPLE

Here is a complete supplementary doc for reference:

**File:** `llms-static/DaisyBadge.md`

```markdown
<!-- Supplementary documentation for DaisyBadge -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

## Overview

DaisyBadge displays small status indicators or labels, typically used for counts, tags, or status markers. Supports **4 style variants**, **9 colors**, and **5 sizes**. Can be used standalone or inside other controls like buttons.

## Style Variants

| Style | Description |
|-------|-------------|
| **Default** | Solid filled background with contrasting text |
| **Outline** | Transparent background with colored border |
| **Dash** | Similar to outline but with dashed border |
| **Ghost** | Very subtle, minimal visual weight |
| **Soft** | Tinted background, softer than solid |

## Size Options

| Size | Use Case |
|------|----------|
| ExtraSmall | Notification dots, minimal indicators |
| Small | Inline with text, compact layouts |
| Medium | Default, general purpose |
| Large | Emphasized badges, headers |
| ExtraLarge | Hero sections, large callouts |

## Quick Examples

\`\`\`xml
<!-- Basic badge -->
<controls:DaisyBadge Content="New" />

<!-- Colored badges -->
<controls:DaisyBadge Content="3" Color="Primary" />
<controls:DaisyBadge Content="Error" Color="Error" />
<controls:DaisyBadge Content="Done" Color="Success" />

<!-- Style variants -->
<controls:DaisyBadge Content="Outline" Style="Outline" Color="Primary" />
<controls:DaisyBadge Content="Ghost" Style="Ghost" />

<!-- Inside a button -->
<controls:DaisyButton Content="Messages">
    <controls:DaisyBadge Content="5" Color="Error" Size="Small" />
</controls:DaisyButton>

<!-- Empty badge (just a dot) -->
<controls:DaisyBadge Color="Success" Size="ExtraSmall" />
\`\`\`

## Tips

- Use `ExtraSmall` with no content for notification dots
- Pair `Error` color with counts for unread/urgent items
- `Outline` style works well on colored backgrounds

---
```

---

## CONTROLS THAT LIKELY NEED SUPPLEMENTARY DOCS

Based on complexity, these controls would benefit most:

### High Priority (Multiple Variants/Complex)

- DaisyButton (variants, styles, sizes, states)
- DaisyAlert (types, styles, icons)
- DaisyBadge (styles, sizes)
- DaisyCard (structure, variants)
- DaisyModal (placement, behavior)
- DaisyToast (positions, types)
- DaisyDrawer (placement, behavior)
- DaisyTabs (styles, behavior)
- DaisyProgress (styles, states)
- DaisyMenu (structure, states)

### Medium Priority (Some Variants)

- DaisyCheckBox (sizes, colors)
- DaisyToggle (sizes, colors)
- DaisyRadio (sizes, colors)
- DaisyInput (styles, sizes, states)
- DaisySelect (styles, sizes)
- DaisyAvatar (shapes, sizes)
- DaisyDivider (orientations, styles)
- DaisySteps (styles, states)
- DaisyTimeline (layout options)

### Lower Priority (Simple/Self-Explanatory)

- DaisyKbd (keyboard key display)
- DaisySkeleton (loading placeholder)
- DaisyStat (statistics display)

---

## EXECUTION STEPS FOR AI ASSISTANT

1. **List controls** needing documentation:

    ```txt
    List files in Flowery.NET/Controls/ matching Daisy*.cs
    ```

2. **For each control**, in order:

   a. Read the C# file
   b. Read the theme AXAML file (if exists)
   c. Search for examples in `Flowery.NET.Gallery/Examples/`
   d. Determine if supplementary docs are needed (use criteria above)
   e. If needed, write the markdown file

3. **Output** each file to `llms-static/DaisyControlName.md`

4. **After all files are created**, the user will run:

   ```bash
   python Utils/generate_docs.py
   python Utils/generate_site.py
   ```

---

## FINAL NOTES

- **Quality over quantity**: A well-written doc for complex controls is better than mediocre docs for everything
- **Be descriptive**: You can't see the UI, so describe what users will experience
- **Be practical**: Focus on what helps someone USE the control
- **Match the style**: Look at `llms-static/DaisyLoading.md` as the gold standard
