import os
from PIL import Image, ImageDraw, ImageFont

BASE_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_PNG = os.path.join(BASE_DIR, "renders", "title_card.png")
os.makedirs(os.path.dirname(OUT_PNG), exist_ok=True)

img = Image.new("RGBA", (1280, 720), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)

w, h = 1280, 720
cx, cy = w / 2, h / 2

font_path = "/System/Library/Fonts/Supplemental/Courier New Bold.ttf"
if not os.path.exists(font_path):
    font_path = "/System/Library/Fonts/Helvetica.ttc"

font_title = ImageFont.truetype(font_path, 54)
font_sub = ImageFont.truetype(font_path, 20)

panel_w, panel_h = 920, 160
px0, py0 = cx - panel_w / 2, cy - panel_h / 2
px1, py1 = cx + panel_w / 2, cy + panel_h / 2

# Black background panel
draw.rectangle([px0, py0, px1, py1], fill=(6, 10, 14, 235))
# Teal left accent line
draw.rectangle([px0, py0, px0 + 8, py1], fill=(41, 191, 191, 255))
# Border top/bottom lines
draw.line([(px0, py0), (px1, py0)], fill=(41, 191, 191, 140), width=2)
draw.line([(px0, py1), (px1, py1)], fill=(41, 191, 191, 140), width=2)

# Draw "THE CORVEX"
t_text = "THE CORVEX"
tb = draw.textbbox((0, 0), t_text, font=font_title)
tw = tb[2] - tb[0]
draw.text((cx - tw / 2, cy - 48), t_text, font=font_title, fill=(41, 191, 191, 255))

# Draw Subtitle
s_text = "SOMETHING GOT OUT.  THE ARMY SEALED THE VALLEY."
sb = draw.textbbox((0, 0), s_text, font=font_sub)
sw = sb[2] - sb[0]
draw.text((cx - sw / 2, cy + 20), s_text, font=font_sub, fill=(220, 225, 230, 220))

img.save(OUT_PNG)
print(f"TITLE CARD CREATED -> {OUT_PNG}")
