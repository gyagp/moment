"""Regenerate the original clock icon; requires Pillow, only for asset editing."""
from pathlib import Path
from PIL import Image, ImageDraw

image = Image.new('RGBA', (256, 256))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((4, 4, 252, 252), radius=62, fill='#127C72')
draw.ellipse((62, 62, 194, 194), outline='white', width=10)
draw.line((128, 86, 128, 128, 163, 128), fill='white', width=10, joint='curve')
draw.ellipse((123, 123, 133, 133), fill='white')
destination = Path(__file__).resolve().parents[1] / 'src/Shike.App/Assets/Shike.ico'
destination.parent.mkdir(parents=True, exist_ok=True)
image.save(destination, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
