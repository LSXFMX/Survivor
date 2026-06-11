# -*- coding: utf-8 -*-
"""
N8 通关装备 + 聚宝盆 抠图 v3

诊断结果：AI 把棋盘格背景 + 圆形/矩形阴影光晕都画进 RGB 通道里了。
- 棋盘格灰: chrom 很小 (<=20), lum 在 85~200 之间。
- 阴影暗色: chrom 很小, lum 在 30~110 之间。
- 暖色羽化光晕: chrom 较小 (<=35), 与主体颜色饱和度比相邻像素低。

主体特征：
- 金色: 高饱和橙黄, chrom 大
- 银/白银: chrom 小, 但 lum >= 215（纯白银 > 235，高光区接近 250+）
- 青晶/绿宝石: 高饱和
- 紫宝石: 高饱和
- 描线黑: lum < 50, 但 chrom 也很小 → 容易被误伤

策略（纯颜色规则，不依赖 floodfill 连通）：
1. 透明判定：
   - 棋盘格区: chrom <= 20 且 85 <= lum <= 205  → 透明
   - 阴影暗灰: chrom <= 25 且 30 <= lum <= 100, **且不在描线邻接区**

   为了避免吃掉黑色描线：保留所有 chrom <= 25 但 lum <= 30 的像素（纯描线黑）。
   实际上观察到描线 lum 是 0~25，棋盘格深灰最低也有 85，区间不重叠，可以放心用。

2. 暖色羽化光晕：观察到 020 心 的橘色光晕 chrom 在 50~120, lum 在 150~230，
   和主体金色边非常接近 → 难以纯颜色规则区分。
   → 改用 floodfill 但只接受"低饱和"扩散，光晕里夹的橘色不会被吃掉，需要后处理。
   → 简化：只用规则 1，让 020 残留暖光晕，下一轮再单独处理。

3. 最后清理 1px 孤岛。
"""

import os
import shutil
from PIL import Image

TARGETS = [
    r"d:\Survivor\Survivor\Assets\像素幸存者资源包\存档装备图标\聚宝盆\FirstClearChest.png",
    r"d:\Survivor\Survivor\Assets\像素幸存者资源包\存档装备图标\通关装备\018.png",
    r"d:\Survivor\Survivor\Assets\像素幸存者资源包\存档装备图标\通关装备\019.png",
    r"d:\Survivor\Survivor\Assets\像素幸存者资源包\存档装备图标\通关装备\020.png",
]


def is_bg_rule(r, g, b):
    """纯颜色规则判定背景。"""
    mx = max(r, g, b); mn = min(r, g, b)
    chrom = mx - mn
    lum = (r + g + b) / 3
    # 棋盘格灰 / 阴影
    if chrom <= 20 and 70 <= lum <= 210:
        return True
    # 偏冷的暗灰光晕（聚宝盆周围深蓝灰）
    if chrom <= 35 and 50 <= lum <= 200 and b > r and (b - r) <= 25:
        return True
    return False


def is_bg_rule_warm_halo(r, g, b):
    """更宽的暖色光晕判定，仅用于二次扫描。"""
    mx = max(r, g, b); mn = min(r, g, b)
    chrom = mx - mn
    lum = (r + g + b) / 3
    # 暖色羽化（020 心周围橘色辐射）：低中饱和的暖色，与主体差距大
    if 40 <= chrom <= 90 and 130 <= lum <= 210 and r >= g >= b:
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


def process(path: str, apply_warm_halo: bool = False):
    if not os.path.exists(path):
        print(f"[SKIP] {path}")
        return
    bak = path + ".bak"
    if os.path.exists(bak):
        shutil.copy2(bak, path)
        print(f"[RESTORE] {bak}")
    else:
        shutil.copy2(path, bak)

    img = Image.open(path).convert("RGB")
    w, h = img.size
    px = img.load()

    out = Image.new("RGBA", (w, h))
    op = out.load()
    cleared = 0
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            bg = is_bg_rule(r, g, b)
            if not bg and apply_warm_halo:
                bg = is_bg_rule_warm_halo(r, g, b)
            if bg:
                op[x, y] = (0, 0, 0, 0)
                cleared += 1
            else:
                op[x, y] = (r, g, b, 255)

    out = clean_isolated(out, min_n=2)
    out.save(path, format="PNG", optimize=True)
    print(f"[OK] {os.path.basename(path)}  cleared {cleared*100//(w*h)}%  warm_halo={apply_warm_halo}")


def main():
    for p in TARGETS:
        # 020 心 单独需要清暖色光晕
        warm = p.endswith("020.png")
        process(p, apply_warm_halo=warm)


if __name__ == "__main__":
    main()
