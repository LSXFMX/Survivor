# -*- coding: utf-8 -*-
"""
把 Resources/UI/AllySkullMark.png（"操纵灵魂的紫气丝线手套"图标）的灰白棋盘格背景
彻底抠成真透明 alpha，并裁掉前景外的空白边距让图标在 sprite 内更"紧凑"。

策略（沿用 make_tomb_skin_transparent.py 的洪水填充思路）：
1. 从 4 个角作为种子做 BFS：种子像素颜色必须满足 is_bg_color → 写入 bg_mask=1。
2. 邻居只有当自身像素颜色也满足 is_bg_color 才扩散——避免吃掉前景紫气流光。
3. 由于丝线/紫气是紫色（高饱和），手套是深紫接近黑（低亮度），都不满足 is_bg_color，
   不会被误删；棋盘格的灰白方块以及方块之间的浅灰抗锯齿过渡都会被一次性清空。
4. 写出 RGBA，背景 alpha=0，前景 alpha=255（二值化）。
5. 紧凑裁剪：扫描非透明像素的 bbox，加 4px padding 后 crop——让 sprite 内容更"满"，
   减少空白边距，避免上层逻辑把"图标看起来变小了一半"误以为是显示尺寸不够。
"""
import os
import shutil
from collections import deque
from PIL import Image

SRC = r"d:\Survivor\Survivor\Assets\Resources\UI\AllySkullMark.png"
DST = SRC  # 原地覆盖；旧文件已在 git 里有历史，按需 git checkout 即可回滚


def is_bg_color(r, g, b):
    """判断像素颜色是否属于"棋盘格灰白背景 / 白雾"。
    - 弱饱和（chrom 小）+ 中高亮度（lum >= 110）→ 视为背景。
    - 紫气流光是高饱和紫，手套是深色低亮度，都不满足，永远保留。"""
    mx = max(r, g, b)
    mn = min(r, g, b)
    chrom = mx - mn
    lum = (r + g + b) / 3
    if chrom <= 32 and lum >= 110:
        return True
    return False


def flood_fill_bg(img: Image.Image) -> Image.Image:
    img = img.convert("RGB")
    w, h = img.size
    rgb = img.load()

    bg_mask = bytearray(w * h)  # 0=未访问, 1=背景
    q = deque()
    seeds = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]
    for sx, sy in seeds:
        idx = sy * w + sx
        if bg_mask[idx] == 0 and is_bg_color(*rgb[sx, sy]):
            bg_mask[idx] = 1
            q.append((sx, sy))

    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if nx < 0 or nx >= w or ny < 0 or ny >= h:
                continue
            ni = ny * w + nx
            if bg_mask[ni]:
                continue
            r, g, b = rgb[nx, ny]
            if is_bg_color(r, g, b):
                bg_mask[ni] = 1
                q.append((nx, ny))

    out = Image.new("RGBA", (w, h))
    op = out.load()
    for y in range(h):
        row_off = y * w
        for x in range(w):
            if bg_mask[row_off + x]:
                op[x, y] = (0, 0, 0, 0)
            else:
                r, g, b = rgb[x, y]
                op[x, y] = (r, g, b, 255)
    return out


def tight_crop(img: Image.Image, padding: int = 6) -> Image.Image:
    """根据非透明像素 bbox 紧凑裁剪 + 少量 padding，避免 sprite 内大量空白。"""
    bbox = img.getbbox()  # 自动按 alpha>0 计算
    if bbox is None:
        return img
    l, t, r, b = bbox
    w, h = img.size
    l = max(0, l - padding)
    t = max(0, t - padding)
    r = min(w, r + padding)
    b = min(h, b + padding)
    return img.crop((l, t, r, b))


def main():
    if not os.path.exists(SRC):
        raise FileNotFoundError(SRC)

    src_img = Image.open(SRC)
    print(f"[INFO] Source: {SRC}  size={src_img.size}  mode={src_img.mode}")

    cleared = flood_fill_bg(src_img)
    print(f"[INFO] Flood-fill done.  size={cleared.size}  mode={cleared.mode}")

    cropped = tight_crop(cleared, padding=6)
    print(f"[INFO] Tight crop done.  size={cropped.size}  (前后裁掉了空白边距)")

    bak = DST + ".raw.bak"
    if os.path.exists(DST) and not os.path.exists(bak):
        shutil.copy2(DST, bak)
        print(f"[INFO] Backup -> {bak}")

    cropped.save(DST, format="PNG", optimize=True)
    print(f"[OK] Wrote {DST}")


if __name__ == "__main__":
    main()
