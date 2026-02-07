# Agent Rules

## Agent Behavior

### CRITICAL: THE 3-ITERATION RULE

**THIS IS NON-NEGOTIABLE. VIOLATION OF THIS RULE IS UNACCEPTABLE.**

If an issue cannot be fixed within **3 consecutive attempts using the SAME approach**, the AI assistant MUST:

1. **STOP making further changes immediately**
2. **Explicitly state**: "I have attempted this 3 times and cannot solve it with this approach."
3. **Ask the user** how they want to proceed before making ANY more modifications
4. **NOT switch to a different approach** without user approval or new user directions

**Rationale**: Endless iteration on a broken approach wastes time, introduces regressions, and frustrates the user. It is BETTER to admit failure early than to keep flailing with random changes that make things worse.

**What counts as an "attempt"**:

- Each code modification targeting the same issue = 1 attempt
- Switching fundamental approach (e.g., Canvas -> Grid, Margin -> Padding) ONLY IF USER APPROVES!
- Minor tweaks to the same approach (e.g., changing values from 8 to 10) do NOT reset the counter

To not hinder iterations completely: a user's indication to continue inactivates this rule for the rest of the session!

### CRITICAL: NEVER REVERT OR SWITCH APPROACHES WITHOUT ASKING

**THIS IS NON-NEGOTIABLE. VIOLATION OF THIS RULE IS UNACCEPTABLE.**

When a solution is not working as expected:

1. **DO NOT revert code** without explicit user approval
2. **DO NOT switch to a different approach** without explicit user approval
3. **DO NOT "try another thing"** - ASK FIRST what the user wants to do
4. **STOP and present options** - Let the user decide the path forward

**Examples of FORBIDDEN behavior:**

- ? "The PNGs aren't loading, let me switch back to SVGs" (without asking)
- ? "This approach has issues, I'll try a different one" (without asking)
- ? "Let me revert this change since it's not working" (without asking)

**Required behavior:**

- ? "The PNGs aren't loading. Options: A) Switch to SVGs, B) Fix PNG deployment, C) Something else. What do you prefer?"
- ? "This isn't working as expected. Should I revert or try to fix it?"
- ? "I see Issue X. Before I change anything, what's your preference?"

**Rationale**: The user may have additional context, may prefer to debug the current approach, or may want to test on other platforms first. Unilateral reversions waste time and destroy progress.

### "No Change" Is a Valid Outcome

When the user reports an issue or asks for investigation **without specifying a required change**:

1. **Investigate first** - Understand the current behavior and why it exists
2. **Assess whether a change is needed** - Sometimes the behavior is correct/intentional
3. **If no obvious solution exists, ASK** - Do NOT go on a "coding spree" trying random fixes
4. **Report findings and recommend** - Present options including "no change" if appropriate

**Rationale**: It is better to confirm with the user before making changes than to introduce unnecessary code churn, potential regressions, or deviations from established design patterns. The user may have context that makes "no change" the correct answer.

**Example scenarios where "no change" may be correct**:

- Behavior follows an established design system (e.g., DaisyUI patterns)
- The "issue" is theme-specific and other themes work correctly
- A workaround already exists (e.g., using a different variant/property)
- The behavior is intentional and documented

- **Think Before You Code**:

Before making any code change, trace through the FULL execution flow. For control/UI changes specifically:

  1. When are properties set? (Construction time vs. after construction)
  2. Do property changes need callbacks to propagate to child elements?
  3. What is the initialization order? (Constructor -> property setters -> Loaded event)
  4. Are there async/deferred events that fire after synchronous code completes?
  
  **Do NOT make partial fixes and iterate.** Analyze the complete picture first, then implement the full solution in one pass.

- **No Flip-Flopping on Approaches**: When the user asks to try a specific approach, commit to that approach fully and let them test it. Don't second-guess midway and switch to an alternative solution. If the first approach doesn't work, the user will provide feedback and we can discuss alternatives together.

### Tool Usage Selection

- **Favor Filesystem MCP Tools**: Use tools like `mcp_filesystem_list_directory`, `mcp_filesystem_search_files`, and `mcp_filesystem_read_file` instead of terminal commands (`ls`, `dir`, `find`, `cat`) whenever possible. These tools provide better interactivity, structured metadata, and safer operation handling.

- **Use Terminal ripgrep for Complex Search**: While `mcp_filesystem_search_files` is preferred for simple glob-based file finding, use terminal `rg` (ripgrep) for complex content searching across the codebase. The internal `grep_search` tool frequently fails. To avoid premature output truncation in long results, interleave with `clear`:

  ```bash
  clear && rg "pattern" path/to/search
  ```

  Examples:
  - Search for function: `clear && rg "function_name" .`
  - Search in specific files: `clear && rg "pattern" --glob "*.ps1"`
  - Case insensitive: `clear && rg -i "pattern" .`
  - Search for exact string: `clear && rg -F "string" .`

### grep_search Tool Bug: File Path Returns 0 Results

**Known Issue**: The `grep_search` tool's description says it works "within files or directories", but it **DOES NOT WORK when `SearchPath` is a single file**. It silently returns 0 results even when matches exist.

**Example of the bug**:

```text
# BAD - Returns 0 results even though "Style" appears 130+ times:
grep_search(Query="Style", SearchPath="d:/github/Project/Themes/File.xaml")
# Result: "No results found"

# GOOD - Same query works when using directory + Includes filter:
grep_search(Query="Style", SearchPath="d:/github/Project/Themes", Includes=["File.xaml"])
# Result: 50+ matches returned correctly
```

**Workaround**: Always use a **directory** as `SearchPath` and use the `Includes` parameter to filter to specific files:

- NO: `SearchPath="path/to/file.cs"` -> Will return 0 results
- YES: `SearchPath="path/to"`, `Includes=["file.cs"]` -> Works correctly

### PowerShell Best Practices (Windows)

- **Encoding Discipline**: Always pass `-Encoding utf8` and prefer `-Raw` when you need exact file content; this avoids cp1252 output errors and line-splitting surprises.
- **Python Unicode Output**: If a Python script prints Unicode, use `python -X utf8` or set `$env:PYTHONIOENCODING = "utf-8"` before running it.
- **ASCII-Only Checks**: Scan for both non-ASCII (>0x7F) and control chars (<0x20 excluding tab/CR/LF); a simple `[^\x00-\x7F]` regex is not enough.
- **Hidden Control Chars**: If a patch fails to match a line, suspect invisible control characters and replace by codepoint via script instead of manual edits.

### Git Bash CRLF Garbled Output Issue (Windows)

**WARNING: When using `sed`, `cat`, `head`, `tail`, or similar text tools on Windows files, ALWAYS pipe through `| tr -d '\r'` to strip carriage returns. Otherwise output will be garbled.**

Example:

```bash
sed -n '100,110p' file.cs | tr -d '\r'
```

The `\r` (carriage return) in CRLF line endings causes the cursor to jump back to the start of the line, making subsequent text overwrite previous content and producing unreadable output.

### Git Bash Windows Path Handling (Windows)

**WARNING: When using terminal tools (like `rg`, `sed`, `find`) in Git Bash on Windows, AVOID absolute paths with backslashes (e.g., `d:\path\to\file`).**

The shell or tools will often misinterpret backslashes as escape characters, leading to "file not found" errors (e.g., `d:\github` becomes `d:github`).

**Correct usage:**

1. **Use relative paths** with forward slashes: `rg "pattern" folder/file.cs`
2. **Set the Working Directory** (`Cwd`) to the base directory of your search.
3. If you MUST use absolute paths, use forward slashes: `/d/github/Flowery.Uno/...` (Git Bash style) or `d:/github/Flowery.Uno/...`.

## Avalonia Clipboard Usage

### Image Clipboard (Windows-only)

Avalonia's built-in clipboard API (`DataObject`, `SetDataObjectAsync`) is deprecated and doesn't reliably copy images. For Windows, use **WinForms interop**:

```csharp
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
private static void SetBitmapClipboardData(byte[] pngBytes)
{
    if (pngBytes == null || pngBytes.Length == 0) return;
    using var stream = new MemoryStream(pngBytes);
    using var image = System.Drawing.Image.FromStream(stream);
    System.Windows.Forms.Clipboard.SetImage(image);
}
```

### Text Clipboard (Cross-platform)

For text, use Avalonia's built-in clipboard:

```csharp
var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
if (clipboard != null)
    await clipboard.SetTextAsync(textContent);
```

### Cross-Platform Project Setup

When a project needs WinForms clipboard on Windows but must remain buildable on other platforms:

1. **Conditional TFM** in `.csproj`:

   ```xml
   <TargetFramework Condition="$([MSBuild]::IsOSPlatform('Windows'))">net8.0-windows</TargetFramework>
   <TargetFramework Condition="!$([MSBuild]::IsOSPlatform('Windows'))">net8.0</TargetFramework>
   ```

2. **Conditional WinForms** in `.csproj`:

   ```xml
   <PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
     <UseWindowsForms>true</UseWindowsForms>
     <DefineConstants>$(DefineConstants);WINDOWS</DefineConstants>
   </PropertyGroup>
   ```

3. **Conditional compilation** in code:

   ```csharp
   #if WINDOWS
   if (OperatingSystem.IsWindows())
   {
       SetBitmapClipboardData(pngBytes);
   }
   else
   #endif
   {
       // Fallback: save to temp file, copy path to clipboard
       var tempPath = Path.Combine(Path.GetTempPath(), "screenshot.png");
       await File.WriteAllBytesAsync(tempPath, pngBytes);
       await clipboard.SetTextAsync(tempPath);
   }
   ```

**Key points:**

- `UseWindowsForms` requires `net8.0-windows` TFM (SDK enforced)
- Use `#if WINDOWS` preprocessor directives to guard WinForms code
- Mark Windows-specific methods with `[SupportedOSPlatform("windows")]`
- Always provide a fallback for non-Windows platforms

## Thorough Refactoring & Coding Standards

To prevent regressions and compilation errors during complex refactors, follow these strict verification steps:

- **Strict Property & Field Sync**: When renaming or deleting a property, field, or method, you MUST perform a file-wide search (e.g., `grep_search` or `rg`) for all references and update them. Do not rely on memory or "obvious" guesses about where a variable is used.
- **Verify Infrastructure**: Before calling a constructor or accessing a collection/field, verify its definition in the current file. Do NOT assume the existence of convenience constructors (e.g., `new MyItem(id, name)`) or private tracking collections (e.g., `_itemLabels`) without explicit proof from a `view_file` or `view_file_outline` call.
- **Method Signature Uniqueness**: When introducing a new method or overload (e.g., `RegisterItemLabelUpdate`), check the entire file for existing signatures to avoid `CS0111` (duplicate member) errors.
- **Atomic State Integrity**: When replacing a construction pattern (e.g., moving from a `StackPanel` to a custom control), ensure ALL secondary logic attached to the old elements-such as hover transitions, selection colors, and event handlers-is correctly migrated to the new structure.
- **Constructor vs. Property Initializers**: If a class does not have a matching constructor, always use C# property initializers `{ Prop1 = value, Prop2 = value }` instead of assuming positional parameters.

## Analyzer and ReSharper Hygiene

- Keep `using` directives minimal; remove any that are unused after edits; check for existing `GlobalUsings.cs`
- Avoid redundant default initializers (e.g., `= false` on `bool`, `= 0` on numeric) unless it changes behavior.
- Prefer concise pattern checks (`is not null`, property patterns) over separate null/type checks that trigger "merge into pattern" suggestions.
- When a null check is only guarding a type test, use a single type pattern (`obj is SomeType foo`) instead of `obj is not null` + type check.
- Use `is { }` when you only need to assert non-null, and prefer property/collection patterns (e.g., `Panel { Children: [var child] }`) instead of manual null/Count/index checks.
- Keep XML doc `<param>` tags in sync with method signatures whenever you add or change parameters.
- Avoid introducing unused helpers; if a helper must exist for future use, add a local suppression directive and a short justification.
- Match nullability on event handlers (`object? sender`, `RoutedEventArgs e`) to avoid nullability warnings.
- Use the narrowest visibility (usually `private`) for helpers unless the API is intended for external use.
- Avoid capturing outer variables in local functions, lambdas, or nested types; pass values explicitly to constructors or method parameters to prevent "captured variable" warnings.
- Omit redundant generic type arguments and let the compiler infer them to avoid "type argument specification is redundant" warnings.
