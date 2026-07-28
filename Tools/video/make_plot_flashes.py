import os
from PIL import Image, ImageDraw, ImageFont

BASE_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RENDERS_DIR = os.path.join(BASE_DIR, "renders")
os.makedirs(RENDERS_DIR, exist_ok=True)

w, h = 1280, 720
cx, cy = w / 2, h / 2

font_path = "/System/Library/Fonts/Supplemental/Courier New Bold.ttf"
if not os.path.exists(font_path):
    font_path = "/System/Library/Fonts/Helvetica.ttc"

# Extra Bold large fonts
font_headline = ImageFont.truetype(font_path, 64)
font_sub = ImageFont.truetype(font_path, 24)

flashes = [
    ("flash_01.png", "AN EXPERIMENT GONE WRONG", "CORVUS RESEARCH ANNEX — SUB-LEVEL C"),
    ("flash_02.png", "THE INFECTED ESCAPED", "CONTAINMENT BREACH IN SECTOR 4"),
    ("flash_03.png", "THE ARMY SEALED THE VALLEY", "FORTIFIED PERIMETER — NO ONE LEAVES ALIVE"),
    ("flash_04.png", "CONTAIN THE OUTBREAK", "EAST RIDGE — CRADLE STATION ACCESS"),
]

for filename, headline, sub in flashes:
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Headline text - FRAMELESS, BORDERLESS with 8px heavy black stroke outline
    hb = draw.textbbox((0, 0), headline, font=font_headline)
    hw = hb[2] - hb[0]
    draw.text((cx - hw / 2, cy - 40), headline, font=font_headline,
              fill=(41, 215, 215, 255), stroke_width=8, stroke_fill=(2, 4, 6, 255))

    # Subtitle text - 4px heavy black stroke outline
    sb = draw.textbbox((0, 0), sub, font=font_sub)
    sw = sb[2] - sb[0]
    draw.text((cx - sw / 2, cy + 36), sub, font=font_sub,
              fill=(240, 245, 250, 255), stroke_width=4, stroke_fill=(2, 4, 6, 255))

    out_path = os.path.join(RENDERS_DIR, filename)
    img.save(out_path)
    print(f"FRAMELESS FLASH CARD CREATED -> {out_path}")
