"""Version and package checks for the manually triggered Photos release."""
import argparse
import filecmp
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET


PHOTOS_RUNTIMES = ('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-arm64', 'osx-x64')


def release_metadata(version, branch, tags):
    if branch not in ('avalonia12', 'main', 'alpha'):
        raise ValueError('Unsupported release branch')
    if not re.fullmatch(r'\d+\.\d+\.\d+', version):
        raise ValueError('Expected a numeric major.minor.patch library version')
    prerelease = branch == 'alpha'
    prefix = f'v{version}{"-alpha" if prerelease else ""}-photos.'
    pattern = re.compile(re.escape(prefix) + r'([1-9]\d*)')
    suffixes = [int(match[1]) for tag in tags if (match := pattern.fullmatch(tag))]
    tag = prefix + str(max(suffixes, default=0) + 1)
    return dict(lib_version=version, tag=tag, display_version=tag[1:],
                is_prerelease=str(prerelease).lower())


def verify_publish(root, output, rid):
    if rid not in PHOTOS_RUNTIMES:
        raise ValueError('Unsupported Photos runtime')
    windows = rid.startswith('win-')
    executable = output / ('Flowery.NET.Photos.Desktop.exe' if windows else 'Flowery.NET.Photos.Desktop')
    if not executable.is_file() or executable.stat().st_size == 0:
        raise ValueError(f'Missing executable: {executable}')
    if not windows and not os.access(executable, os.X_OK):
        raise ValueError(f'Executable permission is missing: {executable}')
    assets = root / 'Flowery.NET.Gallery' / 'Assets'
    fonts = sorted((assets / 'Fonts').glob('*.ttf')) + sorted((assets / 'Fonts').glob('*.otf'))
    samples = sorted(assets.glob('wallpaper_*.jpg')) + sorted(assets.glob('tile_*.jpg')) + [assets / 'placeholder1.jpg']
    if not fonts or len(samples) != 10:
        raise ValueError('Expected fonts and ten sample photos in the source tree')
    for folder, sources in (('Fonts', fonts + [assets / 'Fonts' / 'OFL.txt']), ('Samples', samples)):
        for source in sources:
            target = output / 'Assets' / folder / source.name
            if not target.is_file() or not filecmp.cmp(source, target, shallow=False):
                raise ValueError(f'Missing or changed package asset: {target}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    version = commands.add_parser('version')
    version.add_argument('--branch', required=True)
    verify = commands.add_parser('verify')
    verify.add_argument('--rid', required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    if args.command == 'verify':
        verify_publish(root, root / 'publish', args.rid)
        print(f'Photos package verified for {args.rid}: executable, fonts, license, ten samples')
        return
    library_version = ET.parse(root / 'Directory.Build.props').findtext('./PropertyGroup/Version', '').strip()
    tags = subprocess.check_output(['git', 'tag', '--list'], cwd=root, text=True).splitlines()
    metadata = release_metadata(library_version, args.branch, tags)
    for key, value in metadata.items():
        print(f'{key}={value}')


if __name__ == '__main__':
    main()
