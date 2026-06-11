# -*- coding: utf-8 -*-
"""把 ReviveBossFrame_0..5.png 拼成 GIF 供本地预览。"""
from PIL import Image
import os

SRC_DIR = r"d:\Survivor\Survivor\Assets\Resources\Effects"
OUT_GIF = r"d:\Survivor\Survivor\_tools\preview\revive_boss.gif"
os.makedirs(os.path.dirname(OUT_GIF), exist_ok=True)

frames = []
for i in range(6):
    p = os.path.join(SRC_DIR, f"ReviveBossFrame_{i}.png")
    img = Image.open(p).convert("RGBA")
    # 在 GIF 里给一个深灰底，方便看清紫色细节
    bg = Image.new("RGBA", img.size, (32, 28, 48, 255))
    bg.alpha_composite(img)
    frames.append(bg.convert("P", palette=Image.ADAPTIVE))

# 每帧 100ms，整体循环
frames[0].save(OUT_GIF, save_all=True, append_images=frames[1:], duration=100, loop=0, disposal=2)
print(f"[OK] {OUT_GIF}")
