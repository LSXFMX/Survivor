# -*- coding: utf-8 -*-
"""
对纯洋红 (#FF00FF) 背景的 4 张图做 chroma key 抠图：
- 背景：(R 高, G 低, B 高, 且 R≈B, G 小)
- 主体：其它颜色

判定规则（非常宽容，确保所有粉色变体都吃掉）：
  R > 200 AND B > 180 AND G < 120 AND abs(R-B) < 80  → 背景
  （主体几乎不会出现这种"高红高蓝低绿"的颜色，因为我们的主体是
   金色(R大G中B小)、银白(三色接近)、青色(B大G大R小)、绿色(G大R小B小)等）

边缘 anti-alias 像素（半透明粉色羽化）通过"邻域 = 纯粉色"的二次扩张吃掉。
"""

import os
import shutil
from PIL import Image

SOURCES_TO_TARGETS = [
    (
        r"d:\Survivor\Survivor\generated-images\gacha_n8_v2\Pixel_art_treasure_pot_icon_on_2026-06-10T06-51-53.png",
        r"d:\Survivor\Survivor\Assets\像素幸存者资源包\存档装备图标\聚宝盆\FirstClearChest.png",
    ),
    (
        r"d:\Survivor\Survivor\generated-images\gacha_n8_v2\Pixel_art_RPG_weapon_icon_on_P_2026-06-10T06-51-59.png",
        r"d:\Survivor\Survivor\Assets\像素幸存者资源包\存档装备图标\通关装备\018.png",
    ),
    (
        r"d:\Survivor\Survivor\generated-images\gacha_n8_v2\Pixel_art_RPG_armor_icon_on_PU_2026-06-10T06-52-06.png",
        r"d:\Survivor\Survivor\Assets\像素幸存者资源包\存档装备图标\通关装备\019.png",
    ),
    (
        r"d:\Survivor\Survivor\generated-images\gacha_n8_v2\Pixel_art_RPG_accessory_icon_o_2026-06-10T06-52-05.png",
        r"d:\Survivor\Survivor\Assets\像素幸存者资源包\存档装备图标\通关装备\020.png",
    ),
]


def is_magenta_bg(r, g, b):
    """纯洋红 / 偏粉羽化判定。"""
    # 核心粉：高红 + 高蓝 + 低绿
    if r >= 200 and b >= 180 and g <= 120 and abs(r - b) <= 80:
        return True
    return False


def is_magenta_edge(r, g, b):
    """anti-alias 边缘：粉色与主体的中间色（紫粉、淡粉、肉色羽化）。"""
    # 偏粉的中间色：红和蓝都较高，绿明显小
    if r >= 160 and b >= 140 and g < r - 30 and g < b - 20:
        return True
    return False


def clean_isolated(rgba_img: Image.Image, min_n: int = 2) -> Image.Image:
    w, h = rgba_img.size
    src = rgba_img.load()
    out = Image.new("RGBA", (w, h))
    op = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x, y]
            if a == 0:
                op[x, y] = (0, 0, 0, 0)
                continue
            cnt = 0
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    if dx == 0 and dy == 0:
                        continue
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and src[nx, ny][3] > 0:
                        cnt += 1
            if cnt < min_n:
                op[x, y] = (0, 0, 0, 0)
            else:
                op[x, y] = (r, g, b, 255)
    return out


def remove_magenta_tint(rgba_img: Image.Image) -> Image.Image:
    """对边缘像素去粉色染色：如果像素仍带轻微粉色调（R+B 显著大于 G），
    把它的 R 拉到 G 附近，避免边缘有粉色 fringe。"""
    w, h = rgba_img.size
    src = rgba_img.load()
    out = Image.new("RGBA", (w, h))
    op = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x, y]
            if a == 0:
                op[x, y] = (0, 0, 0, 0)
                continue
            # 检测：R>G+20 AND B>G+10 且 R≈B
            if r > g + 20 and b > g + 10 and abs(r - b) <= 50 and g < 200:
                # 把 R 和 B 都拉向 G（去掉粉色染色）
                avg = (r + g + b) // 3
                r2 = min(r, max(g, avg))
                b2 = min(b, max(g, avg))
                op[x, y] = (r2, g, b2, a)
            else:
                op[x, y] = (r, g, b, a)
    return out


def process(src: str, dst: str):
    if not os.path.exists(src):
        print(f"[MISS] {src}")
        return
    bak = dst + ".bak2"
    if os.path.exists(dst) and not os.path.exists(bak):
        shutil.copy2(dst, bak)

    img = Image.open(src).convert("RGB")
    w, h = img.size
    px = img.load()
    out = Image.new("RGBA", (w, h))
    op = out.load()

    # 第一轮：纯粉去掉
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if is_magenta_bg(r, g, b):
                op[x, y] = (0, 0, 0, 0)
            else:
                op[x, y] = (r, g, b, 255)

    # 第二轮：从透明边缘向内扩，吃掉粉色 anti-alias
    src2 = out.load()
    for _ in range(3):
        changed = 0
        for y in range(h):
            for x in range(w):
                if src2[x, y][3] == 0:
                    continue
                # 是否邻接透明
                has_t = False
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and src2[nx, ny][3] == 0:
                        has_t = True
                        break
                if not has_t:
                    continue
                r, g, b, _ = src2[x, y]
                if is_magenta_edge(r, g, b):
                    src2[x, y] = (0, 0, 0, 0)
                    changed += 1
        if changed == 0:
            break

    # 第三轮：去边缘粉色染色
    out = remove_magenta_tint(out)

    # 1px 孤岛清理
    out = clean_isolated(out, min_n=2)

    out.save(dst, format="PNG", optimize=True)
    print(f"[OK] {os.path.basename(dst)}")


def main():
    for src, dst in SOURCES_TO_TARGETS:
        process(src, dst)


if __name__ == "__main__":
    main()
