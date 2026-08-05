#!/usr/bin/env python3
"""
Count total LoC (non-empty, comments removed) across all *.cs and *.axaml files.

Default output: a single integer (the total).

Notes:
- C#: strips // and /* */ while respecting string/char literals (including verbatim strings @"...").
- AXAML: strips XML comments <!-- ... -->.
- Skips common generated/build directories (bin, obj, .vs, etc.).
"""

from __future__ import annotations

import argparse
import os
import subprocess
from pathlib import Path


_REPO_ROOT = os.path.realpath(Path(__file__).parent.parent)


def _repo_path(value: str) -> Path:
    candidate = os.path.realpath(os.path.join(_REPO_ROOT, value))
    repo_prefix = _REPO_ROOT + os.sep
    if candidate != _REPO_ROOT and not candidate.startswith(repo_prefix):
        raise argparse.ArgumentTypeError("path must stay within the repository root")
    return Path(candidate)


def _strip_xml_comments(text: str) -> str:
    out: list[str] = []
    i = 0
    n = len(text)
    in_comment = False

    while i < n:
        if not in_comment and text.startswith("<!--", i):
            in_comment = True
            i += 4
            continue

        if in_comment:
            if text.startswith("-->", i):
                in_comment = False
                i += 3
                continue
            # Preserve newlines so line counting stays stable.
            ch = text[i]
            if ch == "\n":
                out.append("\n")
            i += 1
            continue

        out.append(text[i])
        i += 1

    return "".join(out)


def _strip_csharp_comments(text: str) -> str:
    out: list[str] = []
    i = 0
    n = len(text)

    NORMAL = 0
    LINE_COMMENT = 1
    BLOCK_COMMENT = 2
    STRING = 3
    VERBATIM_STRING = 4
    CHAR = 5

    state = NORMAL

    while i < n:
        ch = text[i]
        nxt = text[i + 1] if i + 1 < n else ""

        if state == NORMAL:
            if ch == "/" and nxt == "/":
                state = LINE_COMMENT
                i += 2
                continue
            if ch == "/" and nxt == "*":
                state = BLOCK_COMMENT
                i += 2
                continue
            if ch == "@" and nxt == '"':
                state = VERBATIM_STRING
                out.append('@')
                out.append('"')
                i += 2
                continue
            if ch == '"':
                state = STRING
                out.append(ch)
                i += 1
                continue
            if ch == "'":
                state = CHAR
                out.append(ch)
                i += 1
                continue

            out.append(ch)
            i += 1
            continue

        if state == LINE_COMMENT:
            if ch == "\n":
                out.append("\n")
                state = NORMAL
            i += 1
            continue

        if state == BLOCK_COMMENT:
            if ch == "*" and nxt == "/":
                state = NORMAL
                i += 2
                continue
            if ch == "\n":
                out.append("\n")
            i += 1
            continue

        if state == STRING:
            out.append(ch)
            if ch == "\\" and i + 1 < n:
                out.append(text[i + 1])
                i += 2
                continue
            if ch == '"':
                state = NORMAL
            i += 1
            continue

        if state == VERBATIM_STRING:
            out.append(ch)
            if ch == '"':
                # In verbatim strings, "" is an escaped quote.
                if nxt == '"':
                    out.append('"')
                    i += 2
                    continue
                state = NORMAL
            i += 1
            continue

        if state == CHAR:
            out.append(ch)
            if ch == "\\" and i + 1 < n:
                out.append(text[i + 1])
                i += 2
                continue
            if ch == "'":
                state = NORMAL
            i += 1
            continue

    return "".join(out)


def _count_non_empty_lines(text: str) -> int:
    return sum(1 for line in text.splitlines() if line.strip())


def _try_git_root(start: Path) -> Path | None:
    try:
        p = subprocess.run(
            ["git", "-C", str(start), "rev-parse", "--show-toplevel"],
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            check=True,
        )
        return Path(p.stdout.strip())
    except Exception:
        return None


def _git_files(repo_root: Path) -> list[Path]:
    p = subprocess.run(
        ["git", "-C", str(repo_root), "ls-files"],
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
        check=True,
    )
    return [repo_root / line for line in p.stdout.splitlines() if line]


def _walk_files(repo_root: Path, debug: bool = False) -> list[Path]:
    skip_dirs = {
        ".git",
        ".vs",
        ".idea",
        ".vscode",
        "bin",
        "obj",
        "TestResults",
        "terminals",
        "node_modules",
        "packages",
    }
    files: list[Path] = []
    for root, dirs, filenames in os.walk(repo_root):
        root_path = Path(root)
        
        if debug:
            for d in dirs:
                skip_reason = None
                if d in skip_dirs:
                    skip_reason = "in skip_dirs"
                elif d.startswith("!"):
                    skip_reason = "starts with !"
                elif (root_path / d).is_symlink():
                    skip_reason = "is symlink/junction"
                if skip_reason:
                    print(f"SKIP: {root_path / d} ({skip_reason})", file=__import__('sys').stderr)
        
        # Skip junctions/symlinks and folders starting with "!"
        dirs[:] = [
            d for d in dirs
            if d not in skip_dirs 
            and not d.startswith("!")
            and not (root_path / d).is_symlink()
        ]
        for name in filenames:
            files.append(root_path / name)
    return files


def _find_sln_root(start: Path) -> Path | None:
    """Walk upward to find a directory containing a .sln file."""
    current = start
    for _ in range(10):  # Limit search depth
        if any(current.glob("*.sln")):
            return current
        parent = current.parent
        if parent == current:
            break
        current = parent
    return None


def main() -> int:
    parser = argparse.ArgumentParser(description="Count total LoC (no comments) for *.cs and *.axaml.")
    parser.add_argument(
        "path",
        nargs="?",
        type=_repo_path,
        default=Path(_REPO_ROOT),
        help="Repository path within this checkout (defaults to the checkout root).",
    )
    parser.add_argument("--use-git", action="store_true", help="Use git ls-files instead of walking the filesystem.")
    parser.add_argument("-v", "--verbose", action="store_true", help="Print file count and repo root info.")
    parser.add_argument("--debug", action="store_true", help="Show skipped directories.")
    args = parser.parse_args()

    start: Path = args.path
    
    # Try to find repo root by .sln file first, then fall back to start path
    repo_root = None
    if args.use_git:
        repo_root = _try_git_root(start)
    if repo_root is None:
        repo_root = _find_sln_root(start) or start

    if args.verbose:
        print(f"Repo root: {repo_root}", file=__import__('sys').stderr)

    all_paths = _git_files(repo_root) if (args.use_git and _try_git_root(repo_root) is not None) else _walk_files(repo_root, debug=args.debug)

    total = 0
    file_count = 0
    cs_files = 0
    cs_lines = 0
    axaml_files = 0
    axaml_lines = 0
    
    for path in all_paths:
        suffix = path.suffix.lower()
        if suffix not in {".cs", ".axaml"}:
            continue
        if not path.is_file():
            continue

        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue

        file_count += 1
        if suffix == ".cs":
            stripped = _strip_csharp_comments(text)
            lines = _count_non_empty_lines(stripped)
            cs_files += 1
            cs_lines += lines
        else:
            stripped = _strip_xml_comments(text)
            lines = _count_non_empty_lines(stripped)
            axaml_files += 1
            axaml_lines += lines

        total += lines

    if args.verbose:
        import sys
        print(f"Files processed: {file_count} ({cs_files} .cs, {axaml_files} .axaml)", file=sys.stderr)
        print(f"Lines: {cs_lines:,} .cs + {axaml_lines:,} .axaml = {total:,} total", file=sys.stderr)

    print(total)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
