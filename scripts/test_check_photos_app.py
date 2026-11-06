"""Tests how check_photos_app.py finds the copy that Photos saved.

    python3 -B -m unittest discover -s scripts -p "test_*.py"
"""

from pathlib import Path
import tempfile
import unittest

import check_photos_app


class NewPhotosTests(unittest.TestCase):
    def test_the_temporary_file_of_a_save_is_not_the_saved_copy(self):
        with tempfile.TemporaryDirectory() as folder:
            photos = Path(folder)
            (photos / "photo.png").write_bytes(b"")
            before = set(photos.iterdir())
            (photos / "photo (edited).png.18058754c9aa440b8da9788c930e04c5.tmp").write_bytes(b"")
            self.assertEqual([], check_photos_app.new_photos(photos, before))
            (photos / "photo (edited).png").write_bytes(b"")
            self.assertEqual([photos / "photo (edited).png"], check_photos_app.new_photos(photos, before))


if __name__ == "__main__":
    unittest.main()
