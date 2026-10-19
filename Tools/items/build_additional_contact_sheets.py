"""Compose the two-view Unity validation renders with names and final dimensions."""
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main():
    entries = json.loads(Path('Logs/additional-categories/import-result.json').read_text(encoding='utf-8'))['items']
    output = Path('docs/items/additional-categories')
    output.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 17)
    small = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 13)
    for category in ('music', 'art', 'christmas', 'fashion'):
        group = [e for e in entries if e['category'] == category]
        for suffix in ('', '_back'):
            sheet = Image.new('RGB', (5 * 300, math.ceil(len(group) / 5) * 300), '#202933')
            draw = ImageDraw.Draw(sheet)
            for index, entry in enumerate(group):
                image = Image.open(Path('Logs/additional-categories/renders') / (entry['id'] + suffix + '.png'))
                image.thumbnail((300, 245))
                x, y = index % 5 * 300, index // 5 * 300
                sheet.paste(image, (x + (300-image.width)//2, y))
                draw.text((x+8, y+246), f"{index+1:02} {entry['displayName']}", font=font, fill='white')
                size = entry['finalSize']
                draw.text((x+8, y+274), ' × '.join(f'{size[a]:.3f}' for a in 'xyz') + ' m', font=small, fill='#bbcad8')
            sheet.save(output / (category + suffix + '.jpg'), quality=93)
    print(f'Saved eight contact sheets to {output}')


if __name__ == '__main__':
    main()
