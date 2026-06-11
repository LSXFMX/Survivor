"""
基于 AI 生成的 4 帧侧面行走像素画，构建门挑战怪物的 192x192 sprite sheet。

流程：
1) 读取 AI 原图 (1536x1024，纯灰背景，4 帧侧面行走白色人造体)
2) 去除灰色背景 → 透明
3) 自动定位 4 个角色的连通域 bbox
4) 每个 frame 裁剪并居中到 48x48 像素方格
5) 拼成 192x192 的 4x4 sprite sheet：每一行都重复同样的 4 帧
   （现有 anim 的 walk_right / idle 都引用同一行，统一即可）
6) 直接覆盖写入 Assets/怪物prefab/gate/351_爱给网_aigei_com.png
   GUID 不变，prefab/anim 无需改动
"""

import os
from pathlib import Path
from PIL import Image
from collections import deque

ROOT = Path(__file__).parent.parent
SRC  = ROOT / "_tools" / "preview" / "Pixel_art_sprite_sheet_of_a_hu_2026-06-09T04-30-26.png"
DST  = ROOT / "Assets" / "怪物prefab" / "gate" / "351_爱给网_aigei_com.png"
PREVIEW_DIR = ROOT / "_tools" / "preview"

CELL = 48
SHEET_W = CELL * 4   # 192
SHEET_H = CELL * 4   # 192


# ───────── 背景去除 ─────────
def is_gray_bg(rgb):
    """AI 用纯灰背景：中性灰 ~ #808080，允许偏差。
    角色身体是白色 (R,G,B > 230)，描边接近黑 (<60)。
    中灰 (R≈G≈B≈120~150) 判为背景。"""
    r, g, b = rgb[:3]
    if abs(r - g) > 15 or abs(g - b) > 15 or abs(r - b) > 15:
        return False
    return 95 <= r <= 165


def remove_bg(img: Image.Image) -> Image.Image:
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()

    visited = [[False] * h for _ in range(w)]
    q = deque()

    for x in range(w):
        for y in (0, h - 1):
            if is_gray_bg(px[x, y]):
                q.append((x, y)); visited[x][y] = True
    for y in range(h):
        for x in (0, w - 1):
            if is_gray_bg(px[x, y]) and not visited[x][y]:
                q.append((x, y)); visited[x][y] = True

    while q:
        x, y = q.popleft()
        px[x, y] = (0, 0, 0, 0)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not visited[nx][ny]:
                if is_gray_bg(px[nx, ny]):
                    visited[nx][ny] = True
                    q.append((nx, ny))
    return img


# ───────── 连通域 bbox ─────────
def find_bboxes(img: Image.Image, min_pixels=2000):
    """返回 [(x0,y0,x1,y1,area), ...]，按 x0 升序。"""
    w, h = img.size
    px = img.load()
    visited = [[False] * h for _ in range(w)]
    bboxes = []
    for sy in range(h):
        for sx in range(w):
            if visited[sx][sy]:
                continue
            if px[sx, sy][3] < 30:
                visited[sx][sy] = True
                continue
            q = deque([(sx, sy)])
            visited[sx][sy] = True
            xs, ys = [], []
            while q:
                x, y = q.popleft()
                xs.append(x); ys.append(y)
                for dx, dy in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,-1),(1,-1),(-1,1)):
                    nx, ny = x+dx, y+dy
                    if 0 <= nx < w and 0 <= ny < h and not visited[nx][ny]:
                        if px[nx, ny][3] >= 30:
                            visited[nx][ny] = True
                            q.append((nx, ny))
            if len(xs) >= min_pixels:
                bboxes.append((min(xs), min(ys), max(xs), max(ys), len(xs)))
    bboxes.sort(key=lambda b: b[0])
    return bboxes


# ───────── 主流程 ─────────
def main():
    print(f"[源图] {SRC}")
    img = Image.open(SRC).convert("RGBA")
    print(f"[尺寸] {img.size}")

    print("[1/5] 去除灰色背景 (flood-fill)...")
    img = remove_bg(img)
    img.save(PREVIEW_DIR / "gate_v2_nobg.png")

    print("[2/5] 定位 4 个角色 bbox...")
    boxes = find_bboxes(img, min_pixels=3000)
    print(f"  找到 {len(boxes)} 个连通域")
    for i, b in enumerate(boxes):
        print(f"  bbox[{i}]: x={b[0]}..{b[2]} y={b[1]}..{b[3]} area={b[4]}")

    # 合并：可能描边脚踝处被棋盘分割；按 x 把过近的合并
    merged = []
    for b in boxes:
        if merged and b[0] - merged[-1][2] < 20:
            mb = merged[-1]
            merged[-1] = (
                min(mb[0], b[0]), min(mb[1], b[1]),
                max(mb[2], b[2]), max(mb[3], b[3]),
                mb[4] + b[4],
            )
        else:
            merged.append(b)
    print(f"  合并后 {len(merged)} 个")
    if len(merged) < 4:
        raise SystemExit("[错误] 检测到角色少于 4 个")

    # 取面积最大的 4 个，再按 x 排序
    merged.sort(key=lambda b: -b[4])
    frames = merged[:4]
    frames.sort(key=lambda b: b[0])

    print("[3/5] 裁剪 + 居中归一化到 48x48...")
    cells = []
    for i, b in enumerate(frames):
        x0, y0, x1, y1, _ = b
        crop = img.crop((x0, y0, x1 + 1, y1 + 1))
        # 紧致包围盒
        bb = crop.getbbox()
        if bb:
            crop = crop.crop(bb)
        w, h = crop.size
        # 加 8px 边距后放进方形画布
        side = max(w, h) + 8
        sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        sq.paste(crop, ((side - w) // 2, (side - h) // 2), crop)
        # 缩到 48x48（NEAREST 保留像素感）
        cell = sq.resize((CELL, CELL), Image.NEAREST)
        cells.append(cell)
        print(f"  frame {i}: 原 {w}x{h} → 48x48")

    print("[4/5] 组装 192x192 sprite sheet (4 行重复)...")
    sheet = Image.new("RGBA", (SHEET_W, SHEET_H), (0, 0, 0, 0))
    for row in range(4):
        for col in range(4):
            sheet.paste(cells[col], (col * CELL, row * CELL), cells[col])

    # 备份旧文件
    if DST.exists():
        bak = DST.with_suffix(".png.bak")
        if not bak.exists():
            try:
                bak.write_bytes(DST.read_bytes())
                print(f"  备份旧文件 → {bak.name}")
            except Exception as e:
                print(f"  备份失败（忽略）: {e}")

    sheet.save(DST, "PNG")
    print(f"[5/5] 写入 {DST}  ({SHEET_W}x{SHEET_H})")

    # 预览
    big = sheet.resize((SHEET_W * 6, SHEET_H * 6), Image.NEAREST)
    bg = Image.new("RGBA", big.size, (40, 40, 50, 255))
    bg.paste(big, (0, 0), big)
    (PREVIEW_DIR / "gate_v2_sheet_big.png").parent.mkdir(exist_ok=True)
    bg.save(PREVIEW_DIR / "gate_v2_sheet_big.png")

    # GIF（走路 4 帧循环）
    gif_frames = []
    for c in cells:
        canvas = Image.new("RGBA", (CELL * 6, CELL * 6), (40, 40, 50, 255))
        scaled = c.resize((CELL * 6, CELL * 6), Image.NEAREST)
        canvas.paste(scaled, (0, 0), scaled)
        gif_frames.append(canvas.convert("P", palette=Image.ADAPTIVE))
    gif_frames[0].save(
        PREVIEW_DIR / "gate_v2_walk.gif",
        save_all=True, append_images=gif_frames[1:],
        duration=120, loop=0, disposal=2,
    )
    print(f"[预览] {PREVIEW_DIR / 'gate_v2_sheet_big.png'}")
    print(f"[预览] {PREVIEW_DIR / 'gate_v2_walk.gif'}")
    print("\n[完成] sprite sheet 已替换，prefab 和 anim 引用的 GUID 未变，无需重新关联。")


if __name__ == "__main__":
    main()
