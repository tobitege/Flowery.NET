"""Checks of the character check for the documentation.

Run from the repository root: python -B -m unittest discover -s Utils -p test_check_characters.py
The special characters are built with chr(), so this file itself holds none of them.
"""
import contextlib
import io
import tempfile
import unicodedata
from pathlib import Path
import unittest

from check_characters import EMOJI_BLOCKS, ROOT, check_file, documentation_files, find_problems, main

ZWJ = chr(0x200D)
VS16 = chr(0xFE0F)


def text(*code_points: int) -> str:
    return ''.join(map(chr, code_points))


class CharacterCheckTests(unittest.TestCase):
    def test_text_and_emoji_pass(self):
        samples = [
            'Plain text\twith tabs\r\nand CRLF\n',
            'Japanese, Korean and Chinese: ' + text(0x65E5, 0x672C, 0x8A9E, 0xD55C, 0xAD6D, 0x4E2D),
            text(0x1F33C) + ' and box drawing ' + text(0x2500, 0x2502, 0x2705, 0x26A0) + VS16,
            text(0x1F468) + ZWJ + text(0x1F469) + ZWJ + text(0x1F467),  # family
            text(0x2764) + VS16 + ZWJ + text(0x1F525),  # heart on fire
            text(0x1F3F3) + VS16 + ZWJ + text(0x1F308),  # rainbow flag
            text(0x1F1EF, 0x1F1F5),  # flag of Japan
            text(0x1F3F4, 0xE0067, 0xE0062, 0xE0065, 0xE006E, 0xE0067, 0xE007F),  # flag of England
            '1' + VS16 + text(0x20E3) + ' ' + text(0x1F44D, 0x1F3FD),  # keycap, skin tone
            chr(0xFEFF) + 'byte order mark at the start',
        ]
        newer_emoji = next(code_point for code_point in EMOJI_BLOCKS if unicodedata.category(chr(code_point)) == 'Cn')
        samples.append(chr(newer_emoji))
        for sample in samples:
            with self.subTest(sample=ascii(sample)):
                self.assertEqual([], find_problems(sample))

    def test_hidden_characters_fail(self):
        samples = {
            'right-to-left override': 'a' + chr(0x202E) + 'b',
            'isolate': chr(0x2066) + 'a',
            'zero-width space': 'a' + chr(0x200B) + 'b',
            'joiner between letters': 'a' + ZWJ + 'b',
            'joiner after an emoji at the end': text(0x1F468) + ZWJ,
            'soft hyphen': 'a' + chr(0x00AD) + 'b',
            'no-break space': 'a' + chr(0x00A0) + 'b',
            'tags without a flag': 'Hi' + text(0xE0049, 0xE0067, 0xE006E),
            'flag tags without cancel tag': text(0x1F3F4, 0xE0067, 0xE0062),
            'byte order mark inside': 'a' + chr(0xFEFF),
            'control character': 'a' + chr(0x07),
            'private use': chr(0xE000),
        }
        for name, sample in samples.items():
            with self.subTest(name):
                self.assertTrue(find_problems(sample))

    def test_reports_line_column_and_name(self):
        self.assertEqual([(2, 3, 'U+200B ZERO WIDTH SPACE (Cf)')], find_problems('first\nab' + chr(0x200B) + 'c'))
        self.assertEqual(2, len(find_problems('Hi' + text(0xE0049, 0xE0067))))

    def test_files_and_exit_code(self):
        with tempfile.TemporaryDirectory() as folder, contextlib.redirect_stdout(io.StringIO()):
            folder = Path(folder)
            (folder / 'clean.md').write_text('Clean ' + text(0x1F33C), encoding='utf-8')
            (folder / 'image.png').write_bytes(bytes([0x89, 0x50, 0x4E, 0x47, 0x00, 0xFF]))
            self.assertEqual(0, main([str(folder)]))
            (folder / 'broken.md').write_bytes(b'caf\xe9')
            self.assertIn('not UTF-8', check_file(folder / 'broken.md')[0])
            (folder / 'hidden.py').write_text('x = 1' + chr(0x202E), encoding='utf-8')
            self.assertEqual(1, main([str(folder)]))

    def test_documentation_files_are_the_tracked_docs(self):
        files = set(documentation_files())
        for name in ('README.md', 'CHANGELOG.md', 'llms-static/DaisyInput.md', 'Utils/generate_site.py'):
            self.assertIn(ROOT / name, files)
        self.assertNotIn(ROOT / 'Flowery.NET/Controls/DaisyInput.cs', files)


if __name__ == '__main__':
    unittest.main()
