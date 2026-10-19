"""Assemble the actual Blender QA renders into before/after comparison sheets."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from local_settings import authoring_path
from PIL import Image, ImageDraw, ImageFont
import shutil

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Temp/supplied-bear-smooth'
DEST = Path(authoring_path('compose_supplied_bear_preview_1'))
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 30)
small = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 20)
for view in ('front','side'):
    sheet = Image.new('RGB', (1800, 990), '#242424')
    draw = ImageDraw.Draw(sheet)
    for x, label, file in [(0,'제공해 주신 원본',f'smooth-0-{view}.png'),
                           (900,'표면 정리 후',f'final-{view}.png')]:
        draw.text((x+35,14),label,font=font,fill='white')
        sheet.paste(Image.open(SOURCE / file).convert('RGB'),(x,60))
    draw.text((35,962),'표면과 외곽선 비교를 위한 동일한 회색 조명 렌더',font=small,fill='#c4c4c4')
    sheet.save(DEST / f'comparison-{view}.png')
for name in ('front','side'):
    shutil.copy2(SOURCE / f'final-{name}.png',DEST / f'after-{name}.png')
frames = [Image.open(SOURCE / f'corrected-walk-{f:02}.png').convert('RGB') for f in (1,7,13,19)]
sheet = Image.new('RGB',(1800,510),'#242424')
draw = ImageDraw.Draw(sheet)
for index, im in enumerate(frames):
    x = index*450
    sheet.paste(im.resize((450,450)),(x,60))
    draw.text((x+30,18),f'Walk {1+index*6}',font=small,fill='white')
sheet.save(DEST / 'walk-poses.png')
