import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from local_settings import authoring_path
import bpy
out=Path(authoring_path('_crawl_video_qa_1'))
s=bpy.context.scene
s.render.resolution_x=1280;s.render.resolution_y=720;s.render.resolution_percentage=100
ed=s.sequence_editor_create()
for row,name in enumerate(['현재상태','원하는상태']):
 strip=ed.strips.new_movie(name,str(Path(authoring_path('_crawl_video_qa_2'))/(name+'.mp4')),channel=1,frame_start=1)
 print('VIDEO',row,strip.frame_duration,flush=True)
 for j,t in enumerate([.2,.4,.6]):
  s.frame_set(int(strip.frame_duration*t));s.render.filepath=str(out/f'ref_{row}_{j}.png');bpy.ops.render.render(write_still=True)
 ed.strips.remove(strip)
