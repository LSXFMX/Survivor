"""
处理 AI 生成的姿势库图片，提取 4 帧走路循环并拼成雪碧图。

输入：_tools/preview/A_pixel_art_sprite_sheet_of_a__*.png  (1536x1024, 5 个姿势散布)
输出：
  _tools/preview/ai_frames_raw.png       — 4 帧并排（原始大小，已去背景）
  _tools/preview/ai_frames_48.png        — 4 帧 48x48 雪碧图（游戏可用）
  _tools/preview/ai_frames_big.png       — 放大 6x 预览
  _tools/preview/ai_frames.gif           — 动画预览（12fps）

去背景策略：
  AI 输出的"透明背景"实际是棋盘格灰（白灰相间方块）。我们检测：
   - 像素是接近 (200~210, 200~210, 200~210) 的两种灰之一 → 透明
   - 同时排除明显是角色"白色身体" (R>235 且 G>235 且 B>235)
   - 棋盘格颜色样本：浅灰 ~(204,204,204)、深灰 ~(170,170,170)
  采用 flood-fill 从图像四角开始向内填充，只把"连通到边缘的棋盘格区域"判为背景，
  这样角色身体内部即便有相似灰度也不会被误删。
"""

import os
import sys
from pathlib import Path
from PIL import Image
from collections import deque

ROOT = Path(__file__).parent
PREVIEW = ROOT / "preview"
PREVIEW.mkdir(exist_ok=True)


def find_source():
    for p in PREVIEW.glob("A_pixel_art_sprite_sheet_of_a_*.png"):
        return p
    raise FileNotFoundError("未找到 AI 生成图")


# ============ 1) 去背景：flood-fill from edges ============

def is_checkerboard(rgb):
    """是否是棋盘格背景的灰色之一（实测：浅 ~145, 深 ~90）"""
    r, g, b = rgb[:3]
    # 必须是中性灰（三通道接近）
    if abs(r - g) > 12 or abs(g - b) > 12 or abs(r - b) > 12:
        return False
    # 浅灰 (140~155) 或 深灰 (80~100)
    return (80 <= r <= 100) or (138 <= r <= 158)


def remove_bg_floodfill(img: Image.Image) -> Image.Image:
    """从四边 flood-fill 把背景棋盘格变透明"""
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()

    visited = [[False] * h for _ in range(w)]
    q = deque()

    # 种子：四条边
    for x in range(w):
        for y in (0, h - 1):
            if is_checkerboard(px[x, y]):
                q.append((x, y))
                visited[x][y] = True
    for y in range(h):
        for x in (0, w - 1):
            if is_checkerboard(px[x, y]) and not visited[x][y]:
                q.append((x, y))
                visited[x][y] = True

    # BFS 扩散
    while q:
        x, y = q.popleft()
        px[x, y] = (0, 0, 0, 0)  # 透明
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not visited[nx][ny]:
                if is_checkerboard(px[nx, ny]):
                    visited[nx][ny] = True
                    q.append((nx, ny))

    return img


# ============ 2) 找到 5 个角色的包围盒 ============

def find_character_bboxes(img: Image.Image, min_area=8000):
    """对去背景后的图做连通域分析，返回每个角色的 bbox"""
    w, h = img.size
    px = img.load()

    visited = [[False] * h for _ in range(w)]
    bboxes = []

    for sy in range(h):
        for sx in range(w):
            if visited[sx][sy]:
                continue
            if px[sx, sy][3] < 10:  # 透明
                visited[sx][sy] = True
                continue
            # BFS 找连通域
            q = deque([(sx, sy)])
            visited[sx][sy] = True
            xs, ys = [], []
            while q:
                x, y = q.popleft()
                xs.append(x); ys.append(y)
                for dx, dy in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,-1),(1,-1),(-1,1)):
                    nx, ny = x+dx, y+dy
                    if 0 <= nx < w and 0 <= ny < h and not visited[nx][ny]:
                        if px[nx, ny][3] >= 10:
                            visited[nx][ny] = True
                            q.append((nx, ny))
            x0, x1 = min(xs), max(xs)
            y0, y1 = min(ys), max(ys)
            area = (x1 - x0) * (y1 - y0)
            if area >= min_area:
                bboxes.append((x0, y0, x1, y1, len(xs)))

    return bboxes


# ============ 3) 把 bbox 内的角色裁剪并归一化到方形 ============

def crop_to_square(img: Image.Image, bbox, pad=20):
    """裁剪 + 加边距 + 居中到方形画布"""
    x0, y0, x1, y1 = bbox[:4]
    x0 = max(0, x0 - pad); y0 = max(0, y0 - pad)
    x1 = min(img.size[0], x1 + pad); y1 = min(img.size[1], y1 + pad)
    crop = img.crop((x0, y0, x1, y1))
    w, h = crop.size
    side = max(w, h)
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(crop, ((side - w) // 2, (side - h) // 2), crop)
    return sq


# ============ 4) 主流程 ============

def main():
    src = find_source()
    print(f"[源图] {src}")
    img = Image.open(src)
    print(f"[尺寸] {img.size}")

    # 1) 去背景
    print("[1/5] 去背景 (flood-fill)...")
    img = remove_bg_floodfill(img)
    img.save(PREVIEW / "ai_nobg.png")

    # 2) 手动指定 4 个姿势 bbox（基于目测的 1264×848 画布）
    # 选差异最大的 4 帧组成走路循环
    print("[2/5] 使用手动 bbox 提取 4 帧...")
    manual_bboxes = [
        # (x0, y0, x1, y1)  分别对应：上1（带长尾接触帧）
        (95,   30, 360, 380),   # frame 0: 上排左 - contact，长尾后扬
        (430,  30, 660, 380),   # frame 1: 上排中 - pass，尾水平
        (750,  30, 990, 380),   # frame 2: 上排右 - contact，尾横展
        (820, 410, 1080, 770),  # frame 3: 下排右 - pass，尾下垂
    ]

    print("[3/5] 裁剪 + 居中归一化...")
    frames_sq = []
    for i, b in enumerate(manual_bboxes):
        x0, y0, x1, y1 = b
        # 直接裁剪，不再 outline 检测（已去背景，透明部分 alpha=0）
        crop = img.crop((x0, y0, x1, y1))
        # 再对裁剪结果做 alpha 紧致包围盒
        bbox = crop.getbbox()
        if bbox:
            crop = crop.crop(bbox)
        w, h = crop.size
        side = max(w, h) + 20  # 加边距
        sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        sq.paste(crop, ((side - w) // 2, (side - h) // 2), crop)
        sq = sq.resize((256, 256), Image.LANCZOS)
        frames_sq.append(sq)
        print(f"  frame {i}: crop {w}x{h} → 256x256")

    # 4) 拼接 raw 雪碧图
    raw = Image.new("RGBA", (256 * 4, 256), (0, 0, 0, 0))
    for i, f in enumerate(frames_sq):
        raw.paste(f, (i * 256, 0), f)
    raw.save(PREVIEW / "ai_frames_raw.png")
    print(f"[4/5] ai_frames_raw.png  {raw.size}")

    # 5) 缩到 48×48 雪碧图（游戏分辨率）
    frames_48 = [f.resize((48, 48), Image.LANCZOS) for f in frames_sq]
    sheet48 = Image.new("RGBA", (48 * 4, 48), (0, 0, 0, 0))
    for i, f in enumerate(frames_48):
        sheet48.paste(f, (i * 48, 0), f)
    sheet48.save(PREVIEW / "ai_frames_48.png")
    print(f"  ai_frames_48.png   {sheet48.size}")

    # 6) 放大预览 6x
    big = sheet48.resize((48 * 4 * 6, 48 * 6), Image.NEAREST)
    bg = Image.new("RGBA", big.size, (40, 40, 50, 255))
    bg.paste(big, (0, 0), big)
    bg.save(PREVIEW / "ai_frames_big.png")
    print(f"  ai_frames_big.png  {bg.size}")

    # 7) GIF 动画
    gif_frames = []
    for f in frames_48:
        canvas = Image.new("RGBA", (48 * 6, 48 * 6), (40, 40, 50, 255))
        scaled = f.resize((48 * 6, 48 * 6), Image.NEAREST)
        canvas.paste(scaled, (0, 0), scaled)
        gif_frames.append(canvas.convert("P", palette=Image.ADAPTIVE))
    gif_frames[0].save(
        PREVIEW / "ai_frames.gif",
        save_all=True, append_images=gif_frames[1:],
        duration=120, loop=0, disposal=2,
    )
    print(f"[5/5] ai_frames.gif      48x48 @ 12fps")

    print("\n[完成] 查看 _tools/preview/ai_frames_big.png 和 ai_frames.gif")


if __name__ == "__main__":
    main()
