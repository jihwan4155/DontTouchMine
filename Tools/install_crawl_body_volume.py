import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from local_settings import authoring_path
import json
import shutil

ROOT = Path(__file__).resolve().parents[1]
OUT = Path(authoring_path('install_crawl_body_volume_1'))
SOURCE = OUT/'crawl-volume-fixed'
BACKUP = OUT/'before-crawl-volume-fix'
BACKUP.mkdir(exist_ok=True)
DEST = ROOT/'Assets/Scenes/CharacterTest/First'
entries = json.loads((SOURCE/'manifest.json').read_text())
for entry in entries:
    target = DEST/entry['file']
    backup = BACKUP/entry['file']
    if not backup.exists():
        shutil.copy2(target, backup)
    shutil.copy2(SOURCE/entry['file'], target)
print('INSTALLED', len(entries), 'crawl-volume FBXs')
