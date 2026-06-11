"""
重绘门挑战怪 sprite sheet（2DHD · 直立人形毒液体 v3）。

设计
----
- 直立人形（双足行走），正侧面（朝右）
- 纯白色人造体，毒液质感：肌肉张力 + 流体延展（手臂/尾巴顶端拉丝）
- **无面部特征**：光滑头部，无眼无嘴
- **长尾巴**：从腰后伸出，呈 S 形随步态摆动（左右平衡感）
- 4 帧连续步态：接触→过渡→推蹬→过渡

布局
----
- 192x192 / 4x4 / 每格 48x48（沿用既有 .meta 切片）
- PIL 第 0 行（_0.._3）= 右朝向行走 4 帧（idle.anim 也复用这 4 帧）
- 其余 12 格 = 静态站立填充

朝向
----
- 右朝向（侧面，头/胸/尾/前手都在右半部）
- 左朝向由 enemy.cs 的 transform.localScale.x 自动镜像实现
"""

from PIL import Image
import os

# ── 全局参数 ──────────────────────────────────────
SHEET_W = 192
SHEET_H = 192
CELL = 48

TRANSPARENT = (0, 0, 0, 0)
OUTLINE     = (40, 40, 40, 255)        # 深灰描边
WHITE       = (255, 255, 255, 255)
SHADOW      = (210, 215, 222, 255)     # 极浅冷灰（肌肉阴影）
HIGHLIGHT   = (255, 255, 255, 255)     # 高光（与底色同，靠位置区分）

OUT_PNG = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Assets", "怪物prefab", "gate", "351_爱给网_aigei_com.png"
)


# ── 像素工具 ──────────────────────────────────────
def P(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)


def fill_pixels(img, ox, oy, pts, c):
    for (x, y) in pts:
        P(img, ox + x, oy + y, c)


def line_pixels(x0, y0, x1, y1, thick=1):
    """Bresenham 风格直线像素集合，可选粗细（沿 x 方向膨胀）。"""
    out = set()
    dx = abs(x1 - x0)
    dy = abs(y1 - y0)
    sx = 1 if x0 < x1 else -1
    sy = 1 if y0 < y1 else -1
    err = dx - dy
    x, y = x0, y0
    while True:
        for t in range(thick):
            out.add((x + t, y))
        if x == x1 and y == y1:
            break
        e2 = 2 * err
        if e2 > -dy:
            err -= dy
            x += sx
        if e2 < dx:
            err += dx
            y += sy
    return out


def bezier_pixels(p0, p1, p2, samples=40):
    """二次贝塞尔曲线采样像素集合（用于尾巴/手臂的流体曲线）。"""
    out = set()
    for i in range(samples + 1):
        t = i / samples
        x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0]
        y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]
        out.add((round(x), round(y)))
    return out


def outline_of(body_set):
    """从填充像素集合反算外缘像素（8 邻域）。"""
    edge = set()
    for (x, y) in body_set:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if dx == 0 and dy == 0:
                    continue
                if (x + dx, y + dy) not in body_set:
                    edge.add((x + dx, y + dy))
    return edge


# ── 角色绘制 ──────────────────────────────────────
def draw_humanoid(canvas, ox, oy, frame):
    """
    在 cell (ox, oy) 处画一个直立人形（朝右），frame ∈ {0,1,2,3}。

    步态分解（侧面行走）：
      frame 0 — 右脚刚落地（前脚），左脚抬起准备前迈；右臂在后，左臂在前
      frame 1 — 重心过渡到右脚，左脚到达顶点（身体微抬）
      frame 2 — 左脚落地（前脚），右脚在后蹬地；左臂在后，右臂在前
      frame 3 — 重心过渡到左脚，右脚到达顶点（身体微抬）

    尾巴 S 形随步态反向摆动（与对侧腿同相位，物理平衡）。
    """

    # ── 参数：基线/比例（cell 48x48 内）──
    # 修长比例：头小、躯干长、四肢细，更显流体延展感
    bob = 0 if frame in (0, 2) else -1      # 步态中点身体上抬 1px
    pelvis_y = 32 + bob                      # 骨盆 y（再上移给腿留空间）
    chest_y  = 22 + bob                      # 胸腔 y
    neck_y   = 15 + bob                      # 颈 y
    head_cy  = 10 + bob                      # 头中心 y（顶部留空 4px）

    cx = 22                                   # 身体竖直中线 x

    # ── 1) 躯干（梯形：肩宽 8、腰宽 6）──
    torso = set()
    for y in range(neck_y + 2, pelvis_y + 1):
        # 从 chest_y 向下肩宽 8 渐变到腰宽 6
        t = (y - (neck_y + 2)) / max(1, pelvis_y - (neck_y + 2))
        half_w = round(4 - 1 * t)            # 4 → 3
        for x in range(cx - half_w, cx + half_w + 1):
            torso.add((x, y))

    # ── 2) 头部（圆形，无面部特征，小一圈）──
    head = set()
    head_r = 3
    for y in range(head_cy - head_r, head_cy + head_r + 1):
        for x in range(cx - head_r, cx + head_r + 1):
            if (x - cx) ** 2 + (y - head_cy) ** 2 <= head_r * head_r + 1:
                head.add((x, y))
    # 头顶微尖（毒液质感）
    head.add((cx, head_cy - head_r - 1))

    # 颈部连接
    neck = set()
    for y in range(head_cy + head_r, neck_y + 2):
        for x in range(cx - 1, cx + 2):
            neck.add((x, y))

    # ── 3) 手臂（曲线贝塞尔，体现流体延展）──
    # 肩位置：远侧（身后）和近侧（身前）
    shoulder_back  = (cx - 3, neck_y + 3)
    shoulder_front = (cx + 3, neck_y + 3)

    # 手臂前后摆动（与对侧腿同步反向，物理平衡）
    # frame 0: A 腿在前 → 近手在后 + 远手在前
    # frame 2: 镜像
    arm_swings = [
        # (近手末端, 远手末端) —— 绝对坐标
        # 近手（前侧）的 x 偏移要明显在躯干右缘 (cx+3) 外
        ((cx + 6, neck_y + 13), (cx - 8, neck_y + 11)),  # f0：近手前下、远手后下
        ((cx + 4, neck_y + 14), (cx - 7, neck_y + 13)),  # f1：双手向下、中位
        ((cx + 8, neck_y + 12), (cx - 6, neck_y + 13)),  # f2：近手猛甩向前、远手收回
        ((cx + 4, neck_y + 14), (cx - 7, neck_y + 13)),  # f3：双手中位
    ]
    near_hand, far_hand = arm_swings[frame]

    # 远臂（先画，做远景）
    back_arm_curve = bezier_pixels(
        shoulder_back,
        ((shoulder_back[0] + far_hand[0]) // 2 - 1, neck_y + 7),
        far_hand,
    )

    # 近臂（最后画，覆盖躯干）
    front_arm_curve = bezier_pixels(
        shoulder_front,
        ((shoulder_front[0] + near_hand[0]) // 2 + 1, neck_y + 7),
        near_hand,
    )

    # 加粗到 2 像素宽
    def thicken(curve, thickness=2):
        out = set(curve)
        for (x, y) in curve:
            for k in range(1, thickness):
                out.add((x + k, y))
        return out

    back_arm  = thicken(back_arm_curve, 2)
    front_arm = thicken(front_arm_curve, 2)

    # 手部末端：2x2 小拳头（不再做"大球"），保持流体细瘦感
    def hand_blob(cxh, cyh):
        return {
            (cxh, cyh), (cxh + 1, cyh),
            (cxh, cyh + 1), (cxh + 1, cyh + 1),
        }

    back_arm  |= hand_blob(*far_hand)
    front_arm |= hand_blob(*near_hand)

    # ── 4) 腿部（侧面行走步态，4 帧标准循环）──
    hip_y = pelvis_y
    foot_y_base = 44       # 脚底基线（站立时）

    # 4 帧腿部姿态：标准侧面行走循环
    # 命名：A 腿 / B 腿（避免前/后混淆，按相位错开）
    # frame 0：A 在前落地、B 在后蹬
    # frame 1：A 支撑直立、B 抬起经过身体下方
    # frame 2：B 在前落地、A 在后蹬（与 0 镜像相位）
    # frame 3：B 支撑直立、A 抬起经过
    leg_poses = [
        # 每帧：[(腿A 膝, 腿A 脚), (腿B 膝, 腿B 脚)]
        # frame 0：A 在前
        [((cx + 2, hip_y + 5), (cx + 5, foot_y_base)),
         ((cx - 1, hip_y + 5), (cx - 4, foot_y_base))],
        # frame 1：B 抬腿经过
        [((cx + 1, hip_y + 5), (cx + 2, foot_y_base)),
         ((cx + 0, hip_y + 4), (cx - 1, foot_y_base - 4))],   # B 抬高
        # frame 2：B 在前
        [((cx - 1, hip_y + 5), (cx - 4, foot_y_base)),
         ((cx + 2, hip_y + 5), (cx + 5, foot_y_base))],
        # frame 3：A 抬腿经过
        [((cx + 0, hip_y + 4), (cx + 1, foot_y_base - 4)),    # A 抬高
         ((cx + 1, hip_y + 5), (cx + 2, foot_y_base))],
    ]
    pose = leg_poses[frame]
    # pose[0] = 远侧腿（画身后，浅灰描边）
    # pose[1] = 近侧腿（画身前）
    far_knee, far_foot = pose[0]
    near_knee, near_foot = pose[1]

    def build_leg(knee, foot, thicken_x=False):
        """两段曲线（髋→膝→脚）+ 细脚掌"""
        hip = (cx, hip_y)
        thigh = bezier_pixels(hip, ((hip[0] + knee[0]) // 2, hip_y + 2), knee, samples=20)
        shin  = bezier_pixels(knee, ((knee[0] + foot[0]) // 2, (knee[1] + foot[1]) // 2 + 1), foot, samples=20)
        leg = thigh | shin
        # 腿宽 2 像素（瘦长）
        thick = set(leg)
        for (x, y) in leg:
            thick.add((x + 1, y))
        # 脚掌：朝右伸出 3 像素
        if foot[1] >= foot_y_base - 1:
            # 落地时画完整脚掌
            for fx in range(foot[0], foot[0] + 4):
                thick.add((fx, foot[1]))
        else:
            # 抬起时只画脚尖 2 像素
            thick.add((foot[0], foot[1]))
            thick.add((foot[0] + 1, foot[1]))
        return thick

    near_leg = build_leg(near_knee, near_foot)
    far_leg  = build_leg(far_knee,  far_foot)

    # ── 5) 尾巴（S 形长尾，从腰背下方伸出，避开躯干前景）──
    tail_root = (cx - 4, pelvis_y + 2)
    # 尾巴 4 帧姿态：尾尖在身后摆动（高低交替），整体呈反向 S
    tail_tips = [
        (cx - 14, pelvis_y - 5),    # f0：尾向后上扬
        (cx - 15, pelvis_y - 1),    # f1：尾水平后伸
        (cx - 14, pelvis_y + 3),    # f2：尾向后下
        (cx - 15, pelvis_y - 1),    # f3：回到水平
    ]
    tail_ctrl = [
        (cx - 9, pelvis_y - 7),
        (cx - 9, pelvis_y - 3),
        (cx - 9, pelvis_y + 0),
        (cx - 9, pelvis_y - 3),
    ]
    tail_curve = bezier_pixels(tail_root, tail_ctrl[frame], tail_tips[frame], samples=50)
    # 尾巴粗细随长度由粗到细：根部 3 像素、中段 2、尖端 1
    tail = set()
    tail_list = list(tail_curve)
    # 按距离根部排序
    tail_sorted = sorted(tail_list, key=lambda p: (p[0] - tail_root[0]) ** 2 + (p[1] - tail_root[1]) ** 2)
    n = len(tail_sorted)
    for i, (x, y) in enumerate(tail_sorted):
        if i < n * 0.4:
            tail.add((x, y))
            tail.add((x, y + 1))
            tail.add((x, y - 1))
        elif i < n * 0.8:
            tail.add((x, y))
            tail.add((x, y + 1))
        else:
            tail.add((x, y))

    # ── 6) 合成顺序（z-order）：身后层（尾+远臂+远侧腿）→ 身前层（躯干+头+颈+近侧腿）→ 近臂（最顶层，单独描边）──
    back_layer = tail | back_arm | far_leg
    front_layer = torso | head | neck | near_leg

    # 1) 画背景层填充
    fill_pixels(canvas, ox, oy, back_layer, WHITE)
    # 2) 画背景层描边
    back_edge = outline_of(back_layer)
    for (x, y) in back_edge:
        if (x, y) not in front_layer and (x, y) not in front_arm:
            P(canvas, ox + x, oy + y, OUTLINE)
    # 3) 画前景层填充（直接覆盖）
    fill_pixels(canvas, ox, oy, front_layer, WHITE)
    # 4) 画前景层描边
    front_edge = outline_of(front_layer)
    for (x, y) in front_edge:
        if (x, y) not in front_layer and (x, y) not in front_arm:
            P(canvas, ox + x, oy + y, OUTLINE)
    # 5) 近臂单独叠在最顶层（在躯干之上），独立填白 + 独立描边
    #    这样近臂会清晰地"浮"在躯干前，不会与躯干溶为一体
    fill_pixels(canvas, ox, oy, front_arm, WHITE)
    arm_edge = outline_of(front_arm)
    for (x, y) in arm_edge:
        if (x, y) not in front_arm:
            P(canvas, ox + x, oy + y, OUTLINE)

    # ── 7) 肌肉阴影（仅躯干 / 大腿，强化张力）──
    # 胸腹中线
    for y in range(neck_y + 3, pelvis_y - 1):
        cur = canvas.getpixel((ox + cx, oy + y))
        if cur == WHITE:
            P(canvas, ox + cx, oy + y, SHADOW)
    # 大腿外侧暗一格（跳过，避免破坏轮廓）
    _ = pose  # 保留引用

    # ── 8) 头顶毒液拉丝（流体感）──
    # 从头顶向上拉出 2 像素细丝
    head_top = (cx, head_cy - head_r - 1)
    for i in range(2):
        cur = canvas.getpixel((ox + head_top[0], oy + head_top[1] - i - 1))
        if cur[3] == 0:
            P(canvas, ox + head_top[0], oy + head_top[1] - i - 1, OUTLINE)

    # ── 9) 故意 **不画** 任何面部特征（无眼无嘴）──
    # 头部保持光滑纯白 + 黑色轮廓。


# ── 主流程 ──────────────────────────────────────
def main():
    sheet = Image.new("RGBA", (SHEET_W, SHEET_H), TRANSPARENT)

    # PIL 第 0 行：4 帧行走（_0,_1,_2,_3）
    for col in range(4):
        draw_humanoid(sheet, col * CELL, 0, col)

    # 其余 3 行：静态站立（frame=0 一致）
    for row in range(1, 4):
        for col in range(4):
            draw_humanoid(sheet, col * CELL, row * CELL, 0)

    sheet.save(OUT_PNG, "PNG")
    print(f"[OK] wrote {OUT_PNG}  ({SHEET_W}x{SHEET_H})")


if __name__ == "__main__":
    main()
