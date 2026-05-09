"""
Crops landscape JPG photos to portrait ratio (9:16 = 720x1280) from center,
saves results to D:/my.dev/eryggGames/assets/backgrounds/
"""
from PIL import Image
import os, glob

SRC  = r"D:\Users\Ellis\Pictures\Landscapes"
DEST = r"D:\my.dev\eryggGames\assets\backgrounds"
OUT_W, OUT_H = 720, 1280
TARGET_RATIO = OUT_W / OUT_H   # ~0.5625

os.makedirs(DEST, exist_ok=True)

jpgs = [f for f in glob.glob(os.path.join(SRC, "*"))
        if f.lower().endswith(".jpg")]

ok = skip = 0
for path in sorted(jpgs):
    name = os.path.basename(path)
    dest_path = os.path.join(DEST, name)

    try:
        with Image.open(path) as img:
            w, h = img.size

            # Determine crop box to match 9:16 from center
            img_ratio = w / h
            if img_ratio > TARGET_RATIO:
                # Wider than target — crop sides
                new_w = int(h * TARGET_RATIO)
                x0 = (w - new_w) // 2
                box = (x0, 0, x0 + new_w, h)
            else:
                # Taller than target — crop top/bottom
                new_h = int(w / TARGET_RATIO)
                y0 = (h - new_h) // 2
                box = (0, y0, w, y0 + new_h)

            cropped = img.crop(box).resize((OUT_W, OUT_H), Image.LANCZOS)
            cropped.save(dest_path, "JPEG", quality=85)
            ok += 1
    except Exception as e:
        print(f"  SKIP {name}: {e}")
        skip += 1

print(f"Done: {ok} saved, {skip} skipped → {DEST}")
