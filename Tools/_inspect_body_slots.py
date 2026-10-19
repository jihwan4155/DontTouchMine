import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from local_settings import authoring_path
import bpy
p=authoring_path('_inspect_body_slots_1')
bpy.ops.wm.open_mainfile(filepath=p,use_scripts=False)
o=bpy.data.objects['Body']; from collections import Counter
print('slots',[(i,m.name if m else None) for i,m in enumerate(o.data.materials)])
print('poly',Counter(p.material_index for p in o.data.polygons))
