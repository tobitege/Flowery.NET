from __future__ import annotations

import argparse
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


_REPO_ROOT = os.path.realpath(Path(__file__).parent.parent)


def _repo_path(value: str) -> Path:
    candidate = os.path.realpath(os.path.join(_REPO_ROOT, value))
    repo_prefix = _REPO_ROOT + os.sep
    if candidate != _REPO_ROOT and not candidate.startswith(repo_prefix):
        raise argparse.ArgumentTypeError("path must stay within the repository root")
    return Path(candidate)


def _extract_resx_keys(path: Path) -> set[str]:
    safe_path = os.path.realpath(path)
    if not safe_path.startswith(_REPO_ROOT + os.sep):
        raise ValueError(f"path must stay within the repository root: {path}")

    try:
        tree = ET.parse(safe_path)
    except ET.ParseError as e:
        raise ValueError(f"XML parse error in {safe_path}: {e}") from e

    root = tree.getroot()
    keys: set[str] = set()
    for data_elem in root.findall("data"):
        name = data_elem.get("name")
        if name:
            keys.add(name)
    return keys


def _read_text_preserve_bom(path: Path) -> tuple[str, bool]:
    raw = path.read_bytes()
    has_bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    return text, has_bom


def _detect_newline(text: str) -> str:
    return "\r\n" if "\r\n" in text else "\n"


def _extract_data_blocks_by_key(default_resx_text: str) -> dict[str, str]:
    """Extract exact <data ...>...</data> blocks from the default RESX.

    This is intentionally text-based (not XML roundtrip) to preserve formatting.
    """

    nl = _detect_newline(default_resx_text)
    lines = default_resx_text.splitlines(keepends=True)

    blocks: dict[str, str] = {}

    capturing = False
    current_key: str | None = None
    current_lines: list[str] = []

    for line in lines:
        if not capturing:
            # Only handle the common format: <data name="KEY" ...>
            if "<data" in line and "name=\"" in line:
                start = line.find('name="')
                if start != -1:
                    start += len('name="')
                    end = line.find('"', start)
                    if end != -1:
                        current_key = line[start:end]
                        capturing = True
                        current_lines = [line]
                        continue
        else:
            current_lines.append(line)
            if "</data>" in line:
                if current_key:
                    block = "".join(current_lines)
                    # Ensure the block ends with a newline for clean insertion
                    if not block.endswith(nl):
                        block += nl
                    blocks[current_key] = block
                capturing = False
                current_key = None
                current_lines = []

    return blocks


def _insert_missing_blocks(target_text: str, blocks_to_append: list[str]) -> str:
    if not blocks_to_append:
        return target_text

    nl = _detect_newline(target_text)
    insert_marker = "</root>"
    idx = target_text.rfind(insert_marker)
    if idx == -1:
        raise ValueError("Target RESX does not contain </root>")

    insertion = "".join(blocks_to_append)

    # Add a blank line before insertion if not already present
    prefix = "" if target_text[:idx].endswith(nl + nl) else nl

    return target_text[:idx] + prefix + insertion + target_text[idx:]


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Copy missing <data name=...> entries from a default .resx file into localized .resx files. "
            "Existing keys are never overwritten; missing keys are appended near the end of each file."
        )
    )
    parser.add_argument(
        "default_resx",
        type=_repo_path,
        help="Path within this repository to the default .resx file (e.g. FloweryStrings.resx)",
    )
    parser.add_argument(
        "directory",
        type=_repo_path,
        nargs="?",
        default=None,
        help="Repository directory containing localized .resx files (defaults to default_resx parent)",
    )
    parser.add_argument(
        "--prefix",
        type=str,
        default=None,
        help="Only sync keys that start with this prefix (e.g. Theme_)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print what would change, but do not write files",
    )

    args = parser.parse_args(argv)

    default_resx: Path = args.default_resx
    directory: Path = args.directory if args.directory is not None else default_resx.parent
    prefix: str | None = args.prefix

    if not default_resx.is_file():
        print(f"ERROR: default_resx not found: {default_resx}", file=sys.stderr)
        return 2

    if not directory.is_dir():
        print(f"ERROR: directory not found: {directory}", file=sys.stderr)
        return 2

    default_text, _default_has_bom = _read_text_preserve_bom(default_resx)

    try:
        default_keys = _extract_resx_keys(default_resx)
    except ValueError as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return 2

    if prefix is not None:
        default_keys = {k for k in default_keys if k.startswith(prefix)}

    blocks_by_key = _extract_data_blocks_by_key(default_text)

    localized_files = sorted(directory.glob(f"{default_resx.stem}.*{default_resx.suffix}"))
    localized_files = [p for p in localized_files if p.name != default_resx.name]

    if not localized_files:
        print(f"No localized files found in {directory} matching {default_resx.stem}.*{default_resx.suffix}")
        return 0

    changed = 0

    for resx_path in localized_files:
        try:
            existing_keys = _extract_resx_keys(resx_path)
        except ValueError as e:
            print(f"ERROR: {e}", file=sys.stderr)
            return 2

        missing = sorted(default_keys - existing_keys)
        if not missing:
            continue

        blocks: list[str] = []
        missing_without_block: list[str] = []

        for key in missing:
            block = blocks_by_key.get(key)
            if block is None:
                missing_without_block.append(key)
            else:
                blocks.append(block)

        if missing_without_block:
            print(f"ERROR: {default_resx} is missing extractable <data> blocks for:", file=sys.stderr)
            for k in missing_without_block:
                print(f"  - {k}", file=sys.stderr)
            return 2

        target_text, has_bom = _read_text_preserve_bom(resx_path)
        try:
            new_text = _insert_missing_blocks(target_text, blocks)
        except ValueError as e:
            print(f"ERROR: {e} ({resx_path})", file=sys.stderr)
            return 2

        changed += 1
        print(f"UPDATED (+{len(missing)} keys): {resx_path}")

        if not args.dry_run:
            out = new_text.encode("utf-8")
            if has_bom:
                out = b"\xef\xbb\xbf" + out
            resx_path.write_bytes(out)

    if changed == 0:
        print("No changes needed.")
    else:
        print(f"Done. Updated {changed} file(s).")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
