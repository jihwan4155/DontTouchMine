"""Read-only GUID and byte-copy audit. Run from the repository root.

Unity's AdditionalCategoriesImporter additionally compares normalized mesh triangles.
Neither check proves visual dissimilarity after arbitrary re-export or remodeling.
"""
import collections
import hashlib
import json
from pathlib import Path
import re


def audit():
    index, collisions = {}, []
    for meta in Path('Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([a-f0-9]{32})', meta.read_text(encoding='utf-8', errors='replace'), re.M)
        if not match:
            continue
        guid, path = match[1], meta.as_posix()[:-5]
        if guid in index:
            collisions.append([index[guid], path])
        index[guid] = path
    queue = [p.as_posix() for p in Path('Assets/_Game/Content/Scenes').rglob('*.unity')
             if p.name not in {'ItemGallery.unity', 'ItemLab.unity'}]
    references, found = re.compile(r'guid:\s*([a-f0-9]{32})'), set()
    while queue:
        path = queue.pop()
        if path in found:
            continue
        found.add(path)
        if Path(path).suffix.lower() not in {'.unity', '.prefab'}:
            continue
        for guid in references.findall(Path(path).read_text(encoding='utf-8', errors='replace')):
            dependency = index.get(guid, '')
            if Path(dependency).suffix.lower() in {'.prefab', '.fbx', '.obj', '.asset'}:
                queue.append(dependency)
    hashes = collections.defaultdict(list)
    for path in found:
        if Path(path).suffix.lower() in {'.fbx', '.obj', '.asset'}:
            hashes[hashlib.sha256(Path(path).read_bytes()).hexdigest()].append(path)
    copies = []
    for path in Path('Assets/ItemSources/AdditionalCategories').rglob('*'):
        if path.is_file() and path.suffix.lower() in {'.fbx', '.obj', '.asset'}:
            matches = hashes.get(hashlib.sha256(path.read_bytes()).hexdigest())
            if matches:
                copies.append([path.as_posix(), matches])
    result = dict(guidCollisions=collisions, mapGeometryPaths=sorted(found),
                  byteIdenticalMapModels=copies,
                  limitations='Byte hashes cannot detect modified re-exports. Run Unity mesh checks and review renders too.')
    output = Path('Logs/additional-categories/static-audit.json')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(f'{len(index)} GUIDs; {len(collisions)} collisions; {len(copies)} byte-identical map models')
    if collisions or copies:
        raise SystemExit(1)


if __name__ == '__main__':
    audit()
