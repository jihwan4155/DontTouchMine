import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from local_settings import authoring_path
import bpy,json
P=Path(authoring_path('inspect_crawl_torso_1'))
bpy.ops.wm.open_mainfile(filepath=str(P/'BasicPlayerCapsule_Bear_AllActions.blend'),use_scripts=False)
r=bpy.data.objects['DGN_Armature'];b=bpy.data.objects['Body'];s=bpy.context.scene
r.animation_data.action=bpy.data.actions['Crawl_Forward_CurrentBear'];s.frame_set(18)
for n in ['Hips','Spine','Neck','Head','UpperLeg.L','Leg.L','UpperArm.L']:
 p=r.pose.bones.get(n)
 if p:print('BONE',n,'rest',tuple(p.bone.head_local),tuple(p.bone.tail_local),'pose',tuple(p.head),tuple(p.tail))
for lo,hi in [(0.4,.6),(.6,.8),(.8,1), (1,1.2),(1.2,1.4)]:
 vs=[v for v in b.data.vertices if lo<v.co.y<hi and abs(v.co.x)<.3]
 totals={}
 for v in vs:
  for g in v.groups:
   n=b.vertex_groups[g.group].name;totals[n]=totals.get(n,0)+g.weight/max(1,len(vs))
 print('BAND',lo,hi,len(vs),totals)
print('MODS',[(m.name,m.type) for m in b.modifiers]);print('TRANSFORMS',b.matrix_world,r.matrix_world)
