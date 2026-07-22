import os
import math
import random
from PIL import Image, ImageDraw, ImageFilter

def ensure_dir(path):
    os.makedirs(path, exist_ok=True)

def create_concrete_wall(size=256):
    img = Image.new("RGBA", (size, size), (135, 140, 145, 255))
    draw = ImageDraw.Draw(img)
    rng = random.Random(1337)
    
    # Add subtle noise
    pixels = img.load()
    for y in range(size):
        for x in range(size):
            n = rng.randint(-12, 12)
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (
                max(0, min(255, r + n)),
                max(0, min(255, g + n)),
                max(0, min(255, b + n)),
                a
            )
            
    # Draw concrete panel grid lines (2x2 panels)
    dark_line = (60, 64, 68, 255)
    light_line = (180, 185, 190, 255)
    
    half = size // 2
    # Vertical grid line
    draw.line([(half - 1, 0), (half - 1, size)], fill=dark_line, width=2)
    draw.line([(half + 1, 0), (half + 1, size)], fill=light_line, width=1)
    
    # Horizontal grid line
    draw.line([(0, half - 1), (size, half - 1)], fill=dark_line, width=2)
    draw.line([(0, half + 1), (size, half + 1)], fill=light_line, width=1)
    
    # Border seams
    draw.rectangle([0, 0, size - 1, size - 1], outline=(50, 54, 58, 255), width=2)
    
    # Corner rivets
    rivet_color = (40, 44, 48, 255)
    rivet_highlight = (190, 195, 200, 255)
    corners = [
        (16, 16), (half - 16, 16), (half + 16, 16), (size - 16, 16),
        (16, half - 16), (half - 16, half - 16), (half + 16, half - 16), (size - 16, half - 16),
        (16, half + 16), (half - 16, half + 16), (half + 16, half + 16), (size - 16, half + 16),
        (16, size - 16), (half - 16, size - 16), (half + 16, size - 16), (size - 16, size - 16),
    ]
    for cx, cy in corners:
        draw.ellipse([cx - 3, cy - 3, cx + 3, cy + 3], fill=rivet_color)
        draw.ellipse([cx - 2, cy - 2, cx, cy], fill=rivet_highlight)
        
    return img

def create_floor_metal_grid(size=256):
    img = Image.new("RGBA", (size, size), (60, 65, 70, 255))
    draw = ImageDraw.Draw(img)
    rng = random.Random(42)
    
    pixels = img.load()
    for y in range(size):
        for x in range(size):
            n = rng.randint(-8, 8)
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (max(0, min(255, r + n)), max(0, min(255, g + n)), max(0, min(255, b + n)), a)
            
    # Diamond plate pattern
    step = 16
    for y in range(0, size, step):
        for x in range(0, size, step):
            off = (y // step) % 2 * (step // 2)
            px = (x + off) % size
            # Small diamond
            draw.polygon([
                (px + step//2, y + 2),
                (px + step - 2, y + step//2),
                (px + step//2, y + step - 2),
                (px + 2, y + step//2)
            ], fill=(110, 115, 125, 255), outline=(30, 33, 38, 255))
            
    # Grid tile borders
    for i in range(0, size, 64):
        draw.line([(i, 0), (i, size)], fill=(25, 28, 32, 255), width=2)
        draw.line([(0, i), (size, i)], fill=(25, 28, 32, 255), width=2)
        
    return img

def create_hazard_stripe(size=256):
    img = Image.new("RGBA", (size, size), (220, 160, 15, 255))
    draw = ImageDraw.Draw(img)
    
    stripe_w = 24
    black = (30, 32, 35, 255)
    
    # Diagonal stripes
    for d in range(-size, size * 2, stripe_w * 2):
        points = [
            (d, 0),
            (d + stripe_w, 0),
            (d + stripe_w - size, size),
            (d - size, size)
        ]
        draw.polygon(points, fill=black)
        
    # Subtle grunge noise
    rng = random.Random(999)
    pixels = img.load()
    for y in range(size):
        for x in range(size):
            n = rng.randint(-15, 15)
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (max(0, min(255, r + n)), max(0, min(255, g + n)), max(0, min(255, b + n)), a)
            
    return img

def create_steel_panel(size=256):
    img = Image.new("RGBA", (size, size), (90, 95, 100, 255))
    draw = ImageDraw.Draw(img)
    rng = random.Random(777)
    
    pixels = img.load()
    for y in range(size):
        for x in range(size):
            n = rng.randint(-10, 10)
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (max(0, min(255, r + n)), max(0, min(255, g + n)), max(0, min(255, b + n)), a)
            
    # Beveled edge
    draw.rectangle([0, 0, size - 1, size - 1], outline=(150, 155, 160, 255), width=4)
    draw.rectangle([3, 3, size - 4, size - 4], outline=(40, 44, 48, 255), width=3)
    
    # Center horizontal seam
    half = size // 2
    draw.line([(4, half), (size - 5, half)], fill=(40, 44, 48, 255), width=3)
    draw.line([(4, half + 2), (size - 5, half + 2)], fill=(130, 135, 140, 255), width=1)
    
    return img

def create_alien_bio(size=256):
    img = Image.new("RGBA", (size, size), (15, 60, 60, 255))
    draw = ImageDraw.Draw(img)
    rng = random.Random(321)
    
    # Draw glowing bio veins
    for _ in range(12):
        x1, y1 = rng.randint(0, size), rng.randint(0, size)
        x2, y2 = rng.randint(0, size), rng.randint(0, size)
        cx, cy = (x1 + x2) // 2 + rng.randint(-30, 30), (y1 + y2) // 2 + rng.randint(-30, 30)
        
        vein_color = (0, rng.randint(180, 240), rng.randint(160, 220), 255)
        draw.line([(x1, y1), (cx, cy), (x2, y2)], fill=vein_color, width=rng.randint(2, 5))
        
    # Soft blur for bioluminescence
    img = img.filter(ImageFilter.GaussianBlur(radius=2))
    
    # Overlay bio-organic cell spots
    draw = ImageDraw.Draw(img)
    for _ in range(25):
        cx, cy = rng.randint(10, size - 10), rng.randint(10, size - 10)
        r = rng.randint(6, 18)
        c = rng.randint(0, 50)
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(0, 120 + c, 110 + c, 180), outline=(0, 240, 210, 220))
        
    return img

def main():
    out_dir = os.path.abspath("Assets/G1/Textures")
    ensure_dir(out_dir)
    
    textures = {
        "tex_concrete_wall.png": create_concrete_wall(),
        "tex_floor_metal_grid.png": create_floor_metal_grid(),
        "tex_hazard_stripe.png": create_hazard_stripe(),
        "tex_steel_panel.png": create_steel_panel(),
        "tex_alien_bio.png": create_alien_bio(),
    }
    
    for filename, img in textures.items():
        filepath = os.path.join(out_dir, filename)
        img.save(filepath)
        print(f"Generated texture: {filepath}")

if __name__ == "__main__":
    main()
