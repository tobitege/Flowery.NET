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
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        raise ValueError(f"XML parse error in {path}: {e}") from e

    root = tree.getroot()
    keys: set[str] = set()

    for data_elem in root.findall("data"):
        name = data_elem.get("name")
        if name:
            keys.add(name)

    return keys


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Verify that all localized .resx files contain all keys from a default .resx file. "
            "Only checks <data name=...> entries."
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

    args = parser.parse_args(argv)

    default_resx: Path = args.default_resx
    directory: Path = args.directory if args.directory is not None else default_resx.parent

    if not default_resx.is_file():
        print(f"ERROR: default_resx not found: {default_resx}", file=sys.stderr)
        return 2

    if not directory.is_dir():
        print(f"ERROR: directory not found: {directory}", file=sys.stderr)
        return 2

    try:
        default_keys = _extract_resx_keys(default_resx)
    except ValueError as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return 2

    localized_files = sorted(directory.glob(f"{default_resx.stem}.*{default_resx.suffix}"))
    localized_files = [p for p in localized_files if p.name != default_resx.name]

    if not localized_files:
        print(f"No localized files found in {directory} matching {default_resx.stem}.*{default_resx.suffix}")
        return 0

    had_missing = False

    for resx_path in localized_files:
        try:
            keys = _extract_resx_keys(resx_path)
        except ValueError as e:
            print(f"ERROR: {e}", file=sys.stderr)
            had_missing = True
            continue

        missing = sorted(default_keys - keys)
        if missing:
            had_missing = True
            print(f"MISSING ({len(missing)}): {resx_path}")
            for k in missing:
                print(f"  - {k}")

    if had_missing:
        return 1

    print(f"OK: All {len(localized_files)} localized .resx files contain all {len(default_keys)} keys.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
