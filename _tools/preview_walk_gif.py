"""把第 0 行的 4 帧合成成一个 12fps 循环 GIF 预览。"""
from PIL import Image
import os

src = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Assets", "怪物prefab", "gate", "351_爱给网_aigei_com.png"
)
img = Image.open(src).convert("RGBA")

frames = []
for i in range(4):
    cell = img.crop((i * 48, 0, (i + 1) * 48, 48))
    # 放大 4 倍，浅灰背景便于观察
    big = cell.resize((48 * 4, 48 * 4), Image.NEAREST)
    bg = Image.new("RGBA", big.size, (200, 200, 210, 255))
    bg.alpha_composite(big)
    frames.append(bg.convert("P", palette=Image.ADAPTIVE))

out = os.path.join(os.path.dirname(__file__), "preview_walk.gif")
frames[0].save(
    out,
    save_all=True,
    append_images=frames[1:],
    duration=83,   # 12 fps ≈ 83.3ms / frame
    loop=0,
    disposal=2,
)
print(f"[OK] {out}")
