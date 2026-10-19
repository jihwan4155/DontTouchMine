import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from local_settings import authoring_path
import bpy
p=authoring_path('_inspect_blend_mats_1')
bpy.ops.wm.open_mainfile(filepath=p,use_scripts=False)
for o in bpy.context.scene.objects:
 if o.type=='MESH': print(o.name,[(m.name if m else None) for m in o.data.materials],len(o.material_slots),flush=True)
