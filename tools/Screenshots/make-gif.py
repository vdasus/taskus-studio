"""Joins the PNG frames from the Screenshots tool into docs/images/demo.gif.

Run from the repo root: uv run --with pillow tools/Screenshots/make-gif.py
"""
from pathlib import Path

from PIL import Image

WIDTH = 1080
FRAME_MS = 2500

frames_dir = Path("temp/screenshots")
frames = []
for path in sorted(frames_dir.glob("*.png")):
    image = Image.open(path).convert("RGB")
    image = image.resize((WIDTH, round(image.height * WIDTH / image.width)), Image.LANCZOS)
    frames.append(image.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE))

if not frames:
    raise SystemExit(f"No PNG frames in {frames_dir}; run: dotnet run --project tools/Screenshots")
out = Path("docs/images/demo.gif")
frames[0].save(out, save_all=True, append_images=frames[1:], duration=FRAME_MS, loop=0, optimize=True)
print(f"{out}: {len(frames)} frames, {out.stat().st_size // 1024} KiB")
