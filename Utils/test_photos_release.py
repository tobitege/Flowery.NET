"""Run with python -B -m unittest discover -s Utils -p test_photos_release.py."""
from pathlib import Path
import tempfile
import unittest

from photos_release import release_metadata, verify_publish


class PhotosReleaseTests(unittest.TestCase):
    def test_first_release_does_not_reuse_library_or_gallery_tags(self):
        result = release_metadata('3.8.0', 'avalonia12', ['v3.8.0', 'v3.8.0-gallery.7'])
        self.assertEqual('v3.8.0-photos.1', result['tag'])
        self.assertEqual('3.8.0', result['lib_version'])
        self.assertEqual('false', result['is_prerelease'])

    def test_suffix_uses_numeric_maximum_and_exact_version(self):
        tags = ['v3.8.0-photos.9', 'v3.8.0-photos.12', 'v3.8.1-photos.99',
                'v3x8x0-photos.99', 'v3.8.0-photos.100-extra', 'v3.8.0-photos.bad']
        self.assertEqual('v3.8.0-photos.13', release_metadata('3.8.0', 'avalonia12', tags)['tag'])

    def test_stable_branches_share_a_counter_and_alpha_is_separate(self):
        tags = ['v3.8.0-photos.5', 'v3.8.0-alpha-photos.2']
        self.assertEqual('v3.8.0-photos.6', release_metadata('3.8.0', 'main', tags)['tag'])
        alpha = release_metadata('3.8.0', 'alpha', tags)
        self.assertEqual('v3.8.0-alpha-photos.3', alpha['tag'])
        self.assertEqual('true', alpha['is_prerelease'])

    def test_invalid_branch_or_version_cannot_become_a_tag(self):
        for version, branch in [('3.8.0', 'feature/test'), ('3.8.0\nother=value', 'main'),
                                ('3.8.0-preview', 'main'), ('', 'main')]:
            with self.subTest(version=version, branch=branch), self.assertRaises(ValueError):
                release_metadata(version, branch, [])

    def package_fixture(self):
        # Retain generated fixtures; the workspace policy forbids permanent cleanup.
        root = Path(tempfile.mkdtemp(prefix='flowery-photos-release-'))
        output = root / 'publish'
        sources = root / 'Flowery.NET.Gallery' / 'Assets'
        (sources / 'Fonts').mkdir(parents=True)
        for folder in ('Fonts', 'Samples'):
            (output / 'Assets' / folder).mkdir(parents=True)
        files = [('Fonts/Example.ttf', 'Fonts/Example.ttf'), ('Fonts/OFL.txt', 'Fonts/OFL.txt')]
        files += [(f'tile_{i}.jpg', f'Samples/tile_{i}.jpg') for i in range(9)]
        files.append(('placeholder1.jpg', 'Samples/placeholder1.jpg'))
        for source, destination in files:
            (sources / source).write_bytes(b'fixture content')
            (output / 'Assets' / destination).write_bytes(b'fixture content')
        for name in ('Flowery.NET.Photos.Desktop.exe', 'Flowery.NET.Photos.Desktop'):
            executable = output / name
            executable.write_bytes(b'executable fixture')
            executable.chmod(0o755)
        return root, output

    def test_complete_package_is_accepted_for_each_platform(self):
        root, output = self.package_fixture()
        for rid in ('win-x64', 'linux-x64', 'osx-arm64'):
            with self.subTest(rid=rid):
                verify_publish(root, output, rid)

    def test_missing_license_is_rejected(self):
        root, output = self.package_fixture()
        license_path = output / 'Assets' / 'Fonts' / 'OFL.txt'
        license_path.rename(license_path.with_suffix('.saved'))
        with self.assertRaisesRegex(ValueError, 'OFL.txt'):
            verify_publish(root, output, 'win-x64')

    def test_changed_sample_is_rejected(self):
        root, output = self.package_fixture()
        (output / 'Assets' / 'Samples' / 'tile_0.jpg').write_bytes(b'corrupt sample')
        with self.assertRaisesRegex(ValueError, 'tile_0.jpg'):
            verify_publish(root, output, 'win-x64')

    def test_empty_executable_is_rejected(self):
        root, output = self.package_fixture()
        (output / 'Flowery.NET.Photos.Desktop.exe').write_bytes(b'')
        with self.assertRaisesRegex(ValueError, 'Missing executable'):
            verify_publish(root, output, 'win-x64')


if __name__ == '__main__':
    unittest.main()
