"""Verify standalone build references, product identity and optional package hashes."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument('--bundle', type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
manifest = json.loads((root/'Packaging/PanelCladdingEditor/package-manifest.json').read_text(encoding='utf-8-sig'))
plugin_id = manifest['pluginId'].lower()
assert plugin_id != '00000000-0000-0000-0000-000000000000'
for path in (root/'src/PanelCladdingEditor/AssemblyInfo.cs', root/'src/PanelCladdingEditor/PanelCladdingEditorPlugin.cs'):
    assert plugin_id in path.read_text(encoding='utf-8-sig').lower(), f'GUID mismatch: {path}'

references = 0
for project in root.rglob('*.csproj'):
    if any(part in {'bin','obj','bin-testhost','obj-testhost','artifacts','.git'} for part in project.parts):
        continue
    for element in ET.parse(project).iter('ProjectReference'):
        target = (project.parent/element.attrib['Include'].replace('\\','/')).resolve()
        assert target.is_relative_to(root), f'External project reference: {project}: {target}'
        assert target.is_file(), f'Missing project reference: {target}'
        references += 1
solution = (root/'PanelCladding.sln').read_text(encoding='utf-8-sig')
assert 'MCP_Rhino' not in solution
for match in re.finditer(r'= "[^"]+", "([^"]+\.csproj)"', solution):
    assert (root/match[1].replace('\\','/')).is_file()

hashes = 0
if args.bundle:
    bundle = args.bundle.resolve()
    metadata = json.loads((bundle/'bundle-manifest.json').read_text(encoding='utf-8-sig'))
    assert metadata['name'] == manifest['name'] and metadata['version'] == manifest['version']
    for entry in metadata['files']:
        path = (bundle/entry['path']).resolve()
        assert path.is_relative_to(bundle) and path.is_file()
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], f'Hash mismatch: {path}'
        hashes += 1
    files = {p.name for p in (bundle/'Plugin').rglob('*') if p.is_file()}
    assert 'PanelCladdingEditor.rhp' in files and 'PanelCladdingEditor.dll' not in files
    assert not any(name.startswith(('MCP_Rhino','ModelContextProtocol')) for name in files)
print(json.dumps({'result':'PASS','projectReferences':references,'pluginId':plugin_id,'bundleHashes':hashes}))
