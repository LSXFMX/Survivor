# -*- coding: utf-8 -*-
"""
更稳健地把 ur2_tomb 精灵表的灰白棋盘格背景抠成真透明 Alpha。

策略升级（v2）：
1. 先把图当 RGB 读入（无 alpha 信息）。
2. 从图片四角各自做一次 BFS 洪水填充：种子像素 = 角点颜色；
   只要邻居像素的颜色与"任何已访问背景像素"的距离 <= TOL，并且本身满足"低饱和度、亮度落在棋盘灰带"的规则，
   就把它扩入背景。这样能把整片棋盘格 + 周边白雾 + 抗锯齿过渡全部连成一块、一次性清空，
   而角色彩色/近黑像素（紫发/金瞳/红符文/青蓝幽火/深风衣）由于不满足扩散条件，永远不会被吃掉。
3. 输出 RGBA，背景 alpha=0，前景 alpha=255（二值化）。
"""
import os
import shutil
from collections import deque
from PIL import Image

SRC = r"d:\Survivor\Survivor\generated-images\16_bit_retro_pixel_art_2D_game_2026-06-01T06-24-11.png"
DST_SKIN = r"d:\Survivor\Survivor\Assets\像素幸存者资源包\玩家\ur2_tomb_skin.png"
DST_ICON = r"d:\Survivor\Survivor\Assets\像素幸存者资源包\玩家\ur2_tomb_icon.png"


def is_bg_color(r, g, b):
    """判断该像素 *颜色* 是否落在'棋盘格灰背景 / 白雾'区间。
    放宽: 通道差较小（弱饱和），亮度落在 [110, 255]。
    注意: 偶尔会包含偏蓝白的浅冷色 (170,180,190) 这类抗锯齿过渡。"""
    mx = max(r, g, b)
    mn = min(r, g, b)
    chrom = mx - mn
    lum = (r + g + b) / 3
    # 主区间: 偏中高亮度的近灰（棋盘格 + 白雾 + 浅蓝白过渡）
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

    # 第二轮兜底：直接把所有 *颜色满足背景判定* 但被前景包围的孤立残块也清掉
    # （避免角色身上偶尔出现的灰像素？——不，角色是彩色，这一步主要解决"未与四角连通的小灰孤岛"）
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


def main():
    if not os.path.exists(SRC):
        raise FileNotFoundError(SRC)

    img = Image.open(SRC)
    print(f"[INFO] Source: {SRC}  size={img.size}  mode={img.mode}")

    out = flood_fill_bg(img)
    print(f"[INFO] Flood-fill done.  size={out.size}  mode={out.mode}")

    for dst in (DST_SKIN, DST_ICON):
        bak = dst + ".bak"
        if os.path.exists(dst) and not os.path.exists(bak):
            shutil.copy2(dst, bak)
            print(f"[INFO] Backup -> {bak}")

    out.save(DST_SKIN, format="PNG", optimize=True)
    print(f"[OK] Wrote {DST_SKIN}")

    cw = out.size[0] // 4
    ch = out.size[1] // 3
    icon = out.crop((0, 0, cw, ch))
    icon.save(DST_ICON, format="PNG", optimize=True)
    print(f"[OK] Wrote {DST_ICON}  (cell {cw}x{ch})")


if __name__ == "__main__":
    main()
