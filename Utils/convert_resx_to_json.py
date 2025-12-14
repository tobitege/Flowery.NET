#!/usr/bin/env python3
"""
Convert RESX files to JSON format for Flowery.NET WASM localization.
Usage: python Utils/convert_resx_to_json.py
"""

import os
import json
import xml.etree.ElementTree as ET
from pathlib import Path


def parse_resx(resx_path: str) -> dict:
    """Parse a RESX file and return a dictionary of name -> value."""
    tree = ET.parse(resx_path)
    root = tree.getroot()
    
    result = {}
    for data in root.findall('data'):
        name = data.get('name')
        value_elem = data.find('value')
        if name and value_elem is not None and value_elem.text:
            result[name] = value_elem.text
    
    return result


def convert_resx_to_json(resx_dir: str, output_dir: str):
    """Convert all RESX files in a directory to JSON."""
    resx_path = Path(resx_dir)
    output_path = Path(output_dir)
    output_path.mkdir(parents=True, exist_ok=True)
    
    # Find all RESX files
    resx_files = list(resx_path.glob('FloweryStrings*.resx'))
    
    for resx_file in resx_files:
        # Determine language code from filename
        name = resx_file.stem
        if name == 'FloweryStrings':
            lang_code = 'en'
        else:
            # FloweryStrings.de.resx -> de
            lang_code = name.replace('FloweryStrings.', '')
        
        # Parse RESX
        data = parse_resx(str(resx_file))
        
        # Write JSON
        json_file = output_path / f"{lang_code}.json"
        with open(json_file, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
        
        print(f"✓ Converted {resx_file.name} -> {json_file.name} ({len(data)} entries)")


if __name__ == '__main__':
    # Default paths
    resx_dir = 'Flowery.NET/Localization'
    output_dir = 'Flowery.NET/Localization'
    
    if os.path.exists(resx_dir):
        convert_resx_to_json(resx_dir, output_dir)
        print(f"\nDone! JSON files created in {output_dir}")
    else:
        print(f"Error: Directory not found: {resx_dir}")
        print("Run this script from the repository root.")
