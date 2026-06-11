"""把 sprite sheet 第一行 (4 帧行走) 放大 6 倍输出，方便检查细节。"""
from PIL import Image
import os

src = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Assets", "怪物prefab", "gate", "351_爱给网_aigei_com.png"
)
img = Image.open(src).convert("RGBA")
row0 = img.crop((0, 0, 192, 48))
# 放大 6x，用 NEAREST 保持像素感
big = row0.resize((192 * 6, 48 * 6), Image.NEAREST)
# 给透明背景上一个浅灰色方便观察轮廓
bg = Image.new("RGBA", big.size, (200, 200, 210, 255))
bg.alpha_composite(big)
out = os.path.join(os.path.dirname(__file__), "preview_row0.png")
bg.save(out)
print(f"[OK] {out}")
