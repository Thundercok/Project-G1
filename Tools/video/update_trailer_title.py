import os
import shutil
import subprocess
from PIL import Image, ImageDraw, ImageFont

BASE_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VIDEO_IN = os.path.join(BASE_DIR, "CorvusSprawl_Trailer.mp4")
AUDIO_IN = os.path.join(BASE_DIR, "renders", "youtube_track.mp3")
FRAMES_DIR = os.path.join(BASE_DIR, "renders", "trailer_frames_temp")
VIDEO_OUT = os.path.join(BASE_DIR, "CorvusSprawl_Trailer.mp4")
STREAMING_OUT = os.path.join(BASE_DIR, "Assets", "StreamingAssets", "CorvusSprawl_Trailer.mp4")
FFMPEG = "/opt/homebrew/bin/ffmpeg"

def process_trailer():
    if os.path.exists(FRAMES_DIR):
        shutil.rmtree(FRAMES_DIR)
    os.makedirs(FRAMES_DIR, exist_ok=True)

    print("Extracting video frames...")
    subprocess.run([
        FFMPEG, "-y", "-i", VIDEO_IN,
        "-q:v", "2", f"{FRAMES_DIR}/frame_%04d.png"
    ], check=True)

    frame_files = sorted([f for f in os.listdir(FRAMES_DIR) if f.endswith(".png")])
    total_frames = len(frame_files)
    fps = 24.0
    title_start_frame = int(52.5 * fps) # From 52.5s onwards (~last 10s)

    print(f"Total frames: {total_frames}. Updating title text from frame {title_start_frame} onwards...")

    font_path = "/System/Library/Fonts/Supplemental/Courier New Bold.ttf"
    if not os.path.exists(font_path):
        font_path = "/System/Library/Fonts/Helvetica.ttc"

    font_title = ImageFont.truetype(font_path, 52)
    font_sub = ImageFont.truetype(font_path, 20)

    for idx, fname in enumerate(frame_files):
        if idx >= title_start_frame:
            fpath = os.path.join(FRAMES_DIR, fname)
            img = Image.open(fpath).convert("RGBA")
            overlay = Image.new("RGBA", img.size, (0, 0, 0, 0))
            draw = ImageDraw.Draw(overlay)

            w, h = img.size
            cx, cy = w / 2, h / 2

            # Draw central dark panel
            panel_w, panel_h = 920, 160
            px0, py0 = cx - panel_w / 2, cy - panel_h / 2
            px1, py1 = cx + panel_w / 2, cy + panel_h / 2

            # Black semi-transparent background
            draw.rectangle([px0, py0, px1, py1], fill=(8, 12, 16, 225))
            # Teal left accent line
            draw.rectangle([px0, py0, px0 + 8, py1], fill=(41, 191, 191, 255))
            # Border top/bottom lines
            draw.line([(px0, py0), (px1, py0)], fill=(41, 191, 191, 120), width=2)
            draw.line([(px0, py1), (px1, py1)], fill=(41, 191, 191, 120), width=2)

            # Draw "THE CORVEX"
            t_text = "THE CORVEX"
            tb = draw.textbbox((0, 0), t_text, font=font_title)
            tw = tb[2] - tb[0]
            draw.text((cx - tw / 2, cy - 48), t_text, font=font_title, fill=(41, 191, 191, 255))

            # Draw Subtitle "SOMETHING GOT OUT. THE ARMY SEALED THE VALLEY."
            s_text = "SOMETHING GOT OUT.  THE ARMY SEALED THE VALLEY."
            sb = draw.textbbox((0, 0), s_text, font=font_sub)
            sw = sb[2] - sb[0]
            draw.text((cx - sw / 2, cy + 18), s_text, font=font_sub, fill=(220, 225, 230, 220))

            combined = Image.alpha_composite(img, overlay).convert("RGB")
            combined.save(fpath)

    print("Re-encoding video with FFMPEG...")
    audio_args = ["-i", AUDIO_IN, "-map", "0:v:0", "-map", "1:a:0", "-c:a", "aac", "-b:a", "192k", "-af", "afade=t=out:st=60:d=2.9", "-shortest"] if os.path.exists(AUDIO_IN) else []

    temp_out = os.path.join(BASE_DIR, "CorvusSprawl_Trailer_Updated.mp4")
    cmd = [
        FFMPEG, "-y", "-framerate", "24",
        "-i", f"{FRAMES_DIR}/frame_%04d.png"
    ] + audio_args + [
        "-c:v", "libx264", "-pix_fmt", "yuv420p", temp_out
    ]

    subprocess.run(cmd, check=True)

    if os.path.exists(temp_out):
        shutil.move(temp_out, VIDEO_OUT)
        shutil.copy(VIDEO_OUT, STREAMING_OUT)
        print(f"SUCCESSFULLY UPDATED TRAILER TITLE TO 'THE CORVEX' -> {VIDEO_OUT}")

    shutil.rmtree(FRAMES_DIR, ignore_errors=True)

if __name__ == "__main__":
    process_trailer()
