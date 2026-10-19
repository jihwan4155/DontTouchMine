import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from local_settings import authoring_path
import bpy,json
from mathutils import Vector
P=Path(authoring_path('_inspect_crawl_stability_1'))
OUT=Path(authoring_path('_inspect_crawl_stability_2'))
for name in ['BasicPlayerCapsule_Bear_CrawlSplit.blend','BasicPlayerCapsule_Bear_CrawlFinal.blend']:
 bpy.ops.wm.open_mainfile(filepath=str(P/name),use_scripts=False)
 b=bpy.data.objects['Body'];s=bpy.context.scene;r=bpy.data.objects['DGN_Armature']
 print('MODEL',name,len(b.data.vertices),[(k.name,round(max((v.co-b.data.shape_keys.key_blocks[0].data[i].co).length for i,v in enumerate(k.data)),4)) for k in b.data.shape_keys.key_blocks],[(m.name,m.type) for m in b.modifiers],flush=True)
 print('ACTIVE',r.animation_data.action.name,'KEY',b.data.shape_keys.animation_data.action.name,'BOUNDS',[(min(v.co[i] for v in b.data.vertices),max(v.co[i] for v in b.data.vertices)) for i in range(3)],flush=True)
 print('WEIGHTS',[(round(y,2),{g.name:round(sum(next((a.weight for a in v.groups if a.group==g.index),0) for v in b.data.vertices if abs(v.co.y-y)<.05),2) for g in b.vertex_groups if g.name in ['Hips','Spine','UpperLeg.L','UpperLeg.R']}) for y in [.5,.6,.7,.8,.9,1]],flush=True)
