"""Import selected, separately sourced props; never overwrite existing vendor assets.

Requires the user's cached Unity packages and Logs/kenney_holiday-kit.zip.
Run from the Unity project root, then execute Game.Editor.AdditionalCategoriesImporter.Import.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import tarfile
import zipfile

ROOT = Path(__file__).resolve().parents[2]
CACHE = Path(os.environ['APPDATA']) / 'Unity/Asset Store-5.x'
DEST = 'Assets/ItemSources/AdditionalCategories'
CONFIG = ROOT / 'Assets/_Game/Content/ItemCollection/AdditionalCategories.json'
LABELS = {'music': '악기·음악용품', 'art': '문구·미술용품',
          'christmas': '크리스마스', 'fashion': '패션·잡화'}
GUID = re.compile(rb'\b[0-9a-f]{32}\b')

MUSIC = {
    'Acoustic Guitar Beige': '어쿠스틱 기타', 'Electric Guitar Telecaster Blue': '일렉트릭 기타',
    'Electric Guitar Arrow Red': '플라잉 V 기타', 'Bass Guitar Black': '베이스 기타',
    'Bass Guitar Firebird Yellow': '파이어버드 베이스', 'Violin': '바이올린',
    'Violin Bow': '바이올린 활', 'Cello A': '첼로', 'Saxophone': '색소폰',
    'Trumpet': '트럼펫', 'Conga Drum': '콩가', 'Drum Barrel': '드럼',
    'Drum Stick': '드럼 스틱', 'Synth': '신시사이저', 'Dj Mixer': 'DJ 믹서',
    'Sound Speaker': '스피커', 'Amplifier': '앰프', 'Microfon Stand A': '마이크 스탠드',
    'Note Stand': '악보대', 'Piano Black': '그랜드 피아노',
}
ART = {
    'malbert': '이젤', 'canvas': '캔버스', 'palitra': '팔레트', 'brush': '그림 붓',
    'pencase': '연필꽂이', 'pen1': '초록 색연필', 'pen2': '주황 색연필',
    'pen3': '빨간 색연필', 'pen4': '노란 색연필', 'pen5': '파란 색연필',
    'Book': '쌓인 책', 'Book.001': '세워 둔 책', 'frame': '액자',
}
FASHION = {
    1: '접은 회청색 옷', 2: '접은 검정 옷', 3: '파란 셔츠', 4: '살구색 바지',
    5: '검정 숄더백', 6: '빨간 재킷', 7: '접은 흰옷', 8: '검정 재킷',
    9: '접은 청록색 옷', 10: '갈색 숄더백', 11: '접은 파란 옷', 12: '하늘색 바지',
    14: '분홍색 바지', 17: '살구색 셔츠', 19: '연노랑 셔츠', 22: '빨간 야구모자 묶음',
    27: '접은 보라색 토트백', 28: '접은 빨간 옷', 29: '파란 재킷', 30: '검정 야구모자',
}
CHRISTMAS = {
    'candy-cane-red': '빨간 지팡이 사탕', 'candy-cane-green': '초록 지팡이 사탕',
    'gingerbread-man': '진저브레드 맨', 'gingerbread-woman': '진저브레드 우먼',
    'lantern': '크리스마스 가로등', 'nutcracker': '호두까기 인형',
    'present-a-cube': '정육면체 선물상자', 'present-a-rectangle': '직사각형 선물상자',
    'present-a-round': '원형 선물상자', 'reindeer': '순록 장식', 'sled': '썰매',
    'snowflake-a': '눈꽃 장식 A', 'snowflake-b': '눈꽃 장식 B',
    'snowflake-c': '눈꽃 장식 C', 'snowman': '눈사람', 'snowman-hat': '모자 쓴 눈사람',
    'sock-red': '빨간 크리스마스 양말', 'sock-green-cane': '사탕 든 초록 양말',
    'tree-decorated': '장식한 크리스마스트리', 'wreath-decorated': '크리스마스 리스',
}


def write_new(path, payload, meta=None):
    target = (ROOT / path).resolve()
    if not target.is_relative_to(ROOT / 'Assets/ItemSources/AdditionalCategories'):
        raise ValueError('Unsafe import path: ' + path)
    if target.exists():
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(payload)
    if meta:
        target.with_name(target.name + '.meta').write_bytes(meta)


def entry(category, name, source, original, package):
    return dict(id=category + '_' + hashlib.sha1(source.encode()).hexdigest()[:10],
                category=category, categoryName=LABELS[category], displayName=name,
                source=source, originalSource=original, package=package,
                family=Path(original).stem, note='맵과 별도 소스; 프리팹 및 메시 중복 검사 적용')


def import_package(package_name, folder, category, selection):
    matches = list(CACHE.rglob(package_name + '.unitypackage'))
    if len(matches) != 1:
        raise ValueError('Expected one cached package: ' + package_name)
    with tarfile.open(matches[0]) as archive:
        members = {m.name: m for m in archive}
        records = {}
        for key, member in members.items():
            if key.endswith('/pathname'):
                path = archive.extractfile(member).read().decode('utf-8-sig').split('\n')[0].rstrip('\x00')
                records[key.split('/')[0]] = path
        remaps = {g.encode(): hashlib.md5(('additional-categories/' + package_name + '/' + g).encode()).hexdigest().encode()
                  for g in records}
        by_path = {path: g for g, path in records.items()}
        queue = [by_path[path] for path in selection]
        visited = set()
        while queue:
            g = queue.pop()
            if g in visited:
                continue
            visited.add(g)
            path = records[g]
            if g + '/asset' not in members:
                continue
            if Path(path).suffix.lower() in ('.cs', '.dll'):
                raise ValueError('Unexpected executable dependency: ' + path)
            payload = archive.extractfile(members[g + '/asset']).read()
            meta = archive.extractfile(members[g + '/asset.meta']).read()
            for data in (payload, meta):
                queue.extend(ref.decode() for ref in GUID.findall(data) if ref.decode() in records)
            remap = lambda data: GUID.sub(lambda m: remaps.get(m[0], m[0]), data)
            if payload.startswith(b'%YAML') or Path(path).suffix in ('.shadergraph', '.shadersubgraph'):
                payload = remap(payload)
            write_new(f'{DEST}/{folder}/' + path.removeprefix('Assets/'), payload, remap(meta))
        return [entry(category, name, f'{DEST}/{folder}/' + path.removeprefix('Assets/'), path, package_name)
                for path, name in selection.items()]


def main():
    items = import_package('Low Poly Musical Instruments', 'Music', 'music',
                           {f'Assets/Low Poly Instruments/Prefabs/{k}.prefab': v for k, v in MUSIC.items()})
    items += import_package('Low Poly Kids pack', 'Art', 'art',
                            {f'Assets/AG_Kids_Toys_Room/Prefabs/Props/AG_KidsRoom_{k}.prefab': v for k, v in ART.items()})
    items += import_package('Casual Furniture - Low Poly 3D Models Pack', 'Fashion', 'fashion',
                            {f'Assets/ithappy/Furniture_Casual/Prefabs/clothes/clothes_{k:03}.prefab': v for k, v in FASHION.items()})
    with zipfile.ZipFile(ROOT / 'Logs/kenney_holiday-kit.zip') as archive:
        for stem, name in CHRISTMAS.items():
            original = f'Models/FBX format/{stem}.fbx'
            source = f'{DEST}/Christmas/Models/{stem}.fbx'
            write_new(source, archive.read(original))
            items.append(entry('christmas', name, source, original, 'Kenney Holiday Kit 2.0 (CC0)'))
        write_new(f'{DEST}/Christmas/Models/Textures/colormap.png', archive.read('Models/FBX format/Textures/colormap.png'))
        write_new(f'{DEST}/Christmas/License.txt', archive.read('License.txt'))
    CONFIG.write_text(json.dumps({'items': items}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    for category in LABELS:
        print(category, sum(e['category'] == category for e in items))


if __name__ == '__main__':
    main()
