"""
Strip near-black background from dragon eyes image, keep eyeballs intact.
Logic: pixels outside the two eye disks are flat black with no red/purple tint.
We detect those by: (max(rgb) very low) AND (saturation very low) -> alpha 0.
Eyeball sclera is deep crimson-purple — has red dominance — keep it.
"""
from PIL import Image
import numpy as np
import sys, os

SRC = r"d:\Survivor\Survivor\generated-images\dragon_eyes_v5\Two_massive_isolated_dragon_EY_2026-06-09T07-42-06.png"
DST = r"d:\Survivor\Survivor\Assets\Resources\Effects\ReviveDragonEye.png"

img = Image.open(SRC).convert("RGBA")
arr = np.array(img)
r, g, b, a = arr[..., 0], arr[..., 1], arr[..., 2], arr[..., 3]

mx = np.maximum(np.maximum(r, g), b).astype(np.int16)
mn = np.minimum(np.minimum(r, g), b).astype(np.int16)
sat = mx - mn  # simple saturation proxy

# Background = very dark AND nearly grayscale (no purple/red tint)
bg_mask = (mx < 28) & (sat < 10)

# Soft alpha falloff for slightly-above-threshold pixels to avoid hard edge
# brightness between 28..55 with low sat -> partial transparency
soft_mask = (~bg_mask) & (mx < 55) & (sat < 14)
soft_alpha = np.clip(((mx - 28) / (55 - 28)) * 255, 0, 255).astype(np.uint8)

new_a = a.copy()
new_a[bg_mask] = 0
new_a[soft_mask] = np.minimum(new_a[soft_mask], soft_alpha[soft_mask])

arr[..., 3] = new_a
out = Image.fromarray(arr, mode="RGBA")

# Trim transparent padding for tighter sprite, then re-pad to original aspect
bbox = out.getbbox()
if bbox:
    cropped = out.crop(bbox)
    print(f"Cropped bbox: {bbox}, new size: {cropped.size}")
    cropped.save(DST, "PNG")
else:
    out.save(DST, "PNG")

print(f"Saved: {DST}")
print(f"Size: {os.path.getsize(DST)} bytes")
