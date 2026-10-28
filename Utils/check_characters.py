#!/usr/bin/env python3
"""Fails on characters that do not print, except those that emoji are made of.

Hidden characters can make text read differently than it looks: bidirectional controls reorder it
("Trojan Source"), zero-width characters and Unicode tags hide words, for example instructions in
docs/llms.txt for AI assistants. Emoji stay allowed: their joiners and the tags of subdivision flags
pass inside an emoji sequence only.

Usage: python Utils/check_characters.py
It checks the text files of the documentation that git tracks; add a new file with git add first.
"""

import subprocess
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
# Git pathspecs of the documentation that the docs workflow covers
DOCUMENTATION = ('*.md', 'llms-static', 'Utils')
TEXT_SUFFIXES = {'.md', '.txt', '.py', '.js', '.css', '.html', '.json', '.yml', '.yaml'}

BYTE_ORDER_MARK = '\ufeff'
ZERO_WIDTH_JOINER = '\u200d'
VARIATION_SELECTOR = '\ufe0f'
WAVING_BLACK_FLAG = '\U0001f3f4'
CANCEL_TAG = '\U000e007f'
TAG_CHARACTERS = range(0xE0020, 0xE007F)
# Blocks of emoji; code points unknown to this Python's Unicode data there are newer emoji
EMOJI_BLOCKS = range(0x1F000, 0x1FB00)


def is_emoji(char: str) -> bool:
    return ord(char) in EMOJI_BLOCKS or unicodedata.category(char) == 'So'


def find_problems(text: str) -> list[tuple[int, int, str]]:
    """Return (line, column, description) for each character that does not print outside an emoji."""
    problems = []
    line, column = 1, 0
    index = 0
    while index < len(text):
        char = text[index]
        column += 1
        length = 1
        if char == '\n':
            line, column = line + 1, 0
        elif not is_allowed(text, index):
            name = unicodedata.name(char, 'unnamed')
            problems.append((line, column, f'U+{ord(char):04X} {name} ({unicodedata.category(char)})'))
        elif char == WAVING_BLACK_FLAG:
            length += tag_sequence_length(text, index + 1)
        index += length
    return problems


def is_allowed(text: str, index: int) -> bool:
    char = text[index]
    if char.isprintable() or char in '\t\r\n':
        return True
    if char == BYTE_ORDER_MARK:
        return index == 0
    if char == ZERO_WIDTH_JOINER:
        if index == 0 or index + 1 == len(text):
            return False
        before, after = text[index - 1], text[index + 1]
        return (is_emoji(before) or before == VARIATION_SELECTOR) and is_emoji(after)
    return unicodedata.category(char) == 'Cn' and ord(char) in EMOJI_BLOCKS


def tag_sequence_length(text: str, start: int) -> int:
    """Length of the tags of a subdivision flag after its black flag, up to the cancel tag; 0 when there are none."""
    end = start
    while end < len(text) and ord(text[end]) in TAG_CHARACTERS:
        end += 1
    return end - start + 1 if start < end < len(text) and text[end] == CANCEL_TAG else 0


def check_file(path: Path) -> list[str]:
    name = path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else str(path)
    try:
        text = path.read_bytes().decode('utf-8')
    except UnicodeDecodeError as error:
        return [f'{name}: not UTF-8 ({error.reason} at byte {error.start})']
    return [f'{name}:{line}:{column}: {description}' for line, column, description in find_problems(text)]


def documentation_files() -> list[Path]:
    """The tracked text files of the documentation."""
    output = subprocess.run(['git', 'ls-files', '-z', '--', *DOCUMENTATION], cwd=ROOT, check=True,
                            capture_output=True).stdout.decode('utf-8')
    return [ROOT / name for name in output.split('\0') if name and Path(name).suffix.lower() in TEXT_SUFFIXES]


def report(files: list[Path]) -> int:
    problems = [problem for path in files for problem in check_file(path)]
    for problem in problems:
        print(problem)
    print(f'{len(files)} files checked, {len(problems)} problems')
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(report(documentation_files()))
