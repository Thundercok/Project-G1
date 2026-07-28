import os
from PIL import Image, ImageDraw, ImageFont

BASE_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_PNG = os.path.join(BASE_DIR, "renders", "title_card_full.png")
os.makedirs(os.path.dirname(OUT_PNG), exist_ok=True)

w, h = 1280, 720
cx, cy = w / 2, h / 2

img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)

font_path = "/System/Library/Fonts/Supplemental/Courier New Bold.ttf"
if not os.path.exists(font_path):
    font_path = "/System/Library/Fonts/Helvetica.ttc"

# Massive extra-bold fonts
font_title = ImageFont.truetype(font_path, 102)
font_sub = ImageFont.truetype(font_path, 25)

# Solid full-screen blackout (no box frames, no borders)
draw.rectangle([0, 0, 1280, 720], fill=(2, 3, 5, 255))

# Draw "THE CORVEX" in massive extra-bold text with heavy stroke outline
t_text = "THE CORVEX"
tb = draw.textbbox((0, 0), t_text, font=font_title)
tw = tb[2] - tb[0]
draw.text((cx - tw / 2, cy - 65), t_text, font=font_title,
          fill=(41, 215, 215, 255), stroke_width=10, stroke_fill=(0, 0, 0, 255))

# Draw Subtitle "SOMETHING GOT OUT.  THE ARMY SEALED THE VALLEY."
s_text = "SOMETHING GOT OUT.  THE ARMY SEALED THE VALLEY."
sb = draw.textbbox((0, 0), s_text, font=font_sub)
sw = sb[2] - sb[0]
draw.text((cx - sw / 2, cy + 50), s_text, font=font_sub,
          fill=(240, 245, 250, 255), stroke_width=5, stroke_fill=(0, 0, 0, 255))

img.save(OUT_PNG)
print(f"FRAMELESS BOLD TITLE CARD CREATED -> {OUT_PNG}")
