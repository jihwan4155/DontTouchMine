import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from local_settings import authoring_path
import json
import shutil

ROOT = Path(authoring_path('install_crawl_cylinder_1'))
ART = Path(authoring_path('install_crawl_cylinder_2'))
SRC = ART / "crawl-cylinder"
DEST = ROOT / "Assets" / "Scenes" / "CharacterTest" / "First"
BACKUP = ART / "before-crawl-cylinder-fix"
BACKUP.mkdir(parents=True, exist_ok=True)

manifest = json.loads((ART / "manifest.json").read_text(encoding="utf-8"))
changed = set(json.loads((SRC / "corrective-report.json").read_text(encoding="utf-8"))["clips"])
entries = [entry for entry in manifest if entry["clip"] in changed]

for entry in entries:
    name = entry["file"]
    target = DEST / name
    source = SRC / name
    if not source.exists():
        raise FileNotFoundError(source)
    if not target.exists():
        raise FileNotFoundError(target)
    backup = BACKUP / name
    if not backup.exists():
        shutil.copy2(target, backup)
    shutil.copy2(source, target)

print(f"INSTALLED {len(entries)}")
print(f"BACKUP {BACKUP}")
