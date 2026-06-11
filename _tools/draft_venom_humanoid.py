"""
直立人形毒液怪 - 高细节版 v3
朝向：右（行走方向 +x）
策略：清晰的图层分离 + 单帧细节先做好

图层（从后到前）：
  L0 尾巴
  L1 远侧臂 + 远侧腿（压暗）
  L2 躯干 + 头 + 颈
  L3 近侧腿
  L4 近侧臂 (最前)
  L5 滴液 / 拉丝（细节贴片）

每帧 96x96，4 帧横排
"""

import os
import math
from PIL import Image

OUT_DIR = os.path.join(os.path.dirname(__file__), "preview")
os.makedirs(OUT_DIR, exist_ok=True)

CELL = 96
FRAMES = 4
SHEET_W = CELL * FRAMES
SHEET_H = CELL
SCALE_PREVIEW = 4
FINAL_CELL = 48

# === 色板 ===
T          = (0, 0, 0, 0)
WHITE      = (255, 255, 255, 255)
HIGH       = (255, 255, 255, 255)
LIGHT      = (240, 245, 250, 255)   # 高光偏冷
MID        = (210, 218, 232, 255)   # 中调（蓝灰）
SHADOW     = (160, 170, 192, 255)   # 暗
DEEP       = (95, 105, 130, 255)    # 深
OUTLINE    = (25, 28, 40, 255)
SLIME      = (170, 200, 215, 255)   # 黏液主色
SLIME_EDGE = (50, 70, 95, 255)


def P(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)


def fill_set(img, ox, pixels, c):
    for (x, y) in pixels:
        P(img, x + ox, y, c)


def outline(s):
    e = set()
    for (x, y) in s:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in s:
                e.add((x + dx, y + dy))
    return e


# === 几何 ===
def bezier_ord(p0, p1, p2, samples=120):
    pts = []
    for i in range(samples + 1):
        t = i / samples
        x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0]
        y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]
        pts.append((int(round(x)), int(round(y))))
    dedup = [pts[0]]
    for p in pts[1:]:
        if p != dedup[-1]:
            dedup.append(p)
    return dedup


def bezier3_ord(p0, p1, p2, p3, samples=180):
    pts = []
    for i in range(samples + 1):
        t = i / samples
        u = 1 - t
        x = u**3 * p0[0] + 3 * u**2 * t * p1[0] + 3 * u * t**2 * p2[0] + t**3 * p3[0]
        y = u**3 * p0[1] + 3 * u**2 * t * p1[1] + 3 * u * t**2 * p2[1] + t**3 * p3[1]
        pts.append((int(round(x)), int(round(y))))
    dedup = [pts[0]]
    for p in pts[1:]:
        if p != dedup[-1]:
            dedup.append(p)
    return dedup


def disc(cx, cy, r):
    s = set()
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                s.add((x, y))
    return s


def tapered(pts_ord, r_start, r_end):
    out = set()
    n = max(1, len(pts_ord) - 1)
    for i, (x, y) in enumerate(pts_ord):
        t = i / n
        r = r_start * (1 - t) + r_end * t
        ri = int(math.ceil(r))
        r2 = r * r
        for dy in range(-ri, ri + 1):
            for dx in range(-ri, ri + 1):
                if dx * dx + dy * dy <= r2:
                    out.add((x + dx, y + dy))
    return out


def filled_polygon_curves(left_curve, right_curve):
    """两条曲线（分别是左右边界），扫描线填充内部"""
    lx, rx = {}, {}
    for (x, y) in left_curve:
        lx[y] = min(lx.get(y, x), x)
    for (x, y) in right_curve:
        rx[y] = max(rx.get(y, x), x)
    s = set()
    for y in sorted(set(lx) & set(rx)):
        for x in range(lx[y], rx[y] + 1):
            s.add((x, y))
    return s


# === 单帧 ===
def draw_frame(canvas, ox, frame):
    cx = 46          # 身体中线（略偏左，给"朝右走"的甩臂/尾巴留空间）
    ground = 90

    # 步态相位定义：phase=0..3
    # 角色朝右走（+x 方向）
    # bob：抬腿瞬间身体微抬
    bob = -2 if frame in (1, 3) else 0

    # ====== 关键解剖参数 ======
    head_cy   = 16
    head_r    = 8
    neck_top  = head_cy + head_r - 1
    neck_bot  = 28
    shoulder_y = 30
    chest_top  = 30
    waist_y    = 52
    pelvis_y   = 62
    hip_y      = 66
    knee_y     = 78
    foot_y     = ground

    # ============ 1. 躯干 ============
    torso_L = bezier_ord((cx - 10, shoulder_y + bob),
                         (cx - 13, 46 + bob),    # 腰侧凸出
                         (cx - 7, pelvis_y + bob), samples=120)
    torso_R = bezier_ord((cx + 10, shoulder_y + bob),
                         (cx + 12, 46 + bob),
                         (cx + 7, pelvis_y + bob), samples=120)
    torso = filled_polygon_curves(torso_L, torso_R)
    # 肩膀圆顶
    torso |= disc(cx - 8, shoulder_y + bob, 5)
    torso |= disc(cx + 8, shoulder_y + bob, 5)
    # 骨盆圆
    torso |= disc(cx - 4, pelvis_y + bob, 5)
    torso |= disc(cx + 4, pelvis_y + bob, 5)
    torso |= disc(cx, pelvis_y + 2 + bob, 5)

    # ============ 2. 颈 + 头 ============
    neck = set()
    for y in range(neck_top + bob, neck_bot + 2 + bob):
        w = 4 - (y - neck_top - bob) // 4
        for x in range(cx - w, cx + w + 1):
            neck.add((x, y))

    head = disc(cx + 1, head_cy + bob, head_r)
    # 略微前倾的椭圆头形（不要鸟喙突起）
    head |= disc(cx + 2, head_cy + 1 + bob, head_r - 1)

    # 头顶毒液拉丝（3 根，粗 2 像素，向后倾）
    head_strands = set()
    strand_specs = [
        ((cx - 2, head_cy - head_r + bob), (cx - 6, head_cy - head_r - 7 + bob)),
        ((cx + 1, head_cy - head_r + bob), (cx - 3, head_cy - head_r - 10 + bob)),
        ((cx + 4, head_cy - head_r + bob), (cx + 1, head_cy - head_r - 6 + bob)),
    ]
    for p0, p1 in strand_specs:
        # 直线插值
        steps = max(abs(p1[0] - p0[0]), abs(p1[1] - p0[1])) + 1
        for k in range(steps):
            t = k / max(1, steps - 1)
            x = int(round(p0[0] * (1 - t) + p1[0] * t))
            y = int(round(p0[1] * (1 - t) + p1[1] * t))
            head_strands.add((x, y))
            head_strands.add((x + 1, y))  # 2 像素粗

    # ============ 3. 腿（朝右走的 4 帧步态）============
    # 朝右走的 4 帧步态：
    #   摄像机视角下，近侧腿（A）始终在身体右半侧、远侧腿（B）始终在身体左半侧
    #   行走通过两腿"前后偏移"产生
    #   f0：A 向后蹬（x 大但弯）、B 向前迈（x 小但伸）
    #   f1：B 抬起经过身下
    #   f2：A 向前迈（x 大且伸）、B 向后蹬（x 小但弯）
    #   f3：A 抬起经过身下
    # 注意：朝右走 "前 = +x 方向"
    leg_phases = [
        # (near_knee, near_foot, far_knee, far_foot)
        # f0: A 蹬地（脚在身后偏左）、B 落地（脚在身前偏右）—— 等等，这反了
        # 正确：朝右走，"在前"= +x
        # 让 A=右腿（近）、B=左腿（远），他们的脚都围绕 hip 摆
        # f0: 近脚在前 (+8)，远脚在后 (-2)
        ((cx + 4, knee_y - 1 + bob), (cx + 9, foot_y),
         (cx - 2, knee_y - 1 + bob), (cx - 4, foot_y)),
        # f1: 近脚支撑直立、远脚抬起经过身下
        ((cx + 2, knee_y + bob), (cx + 4, foot_y),
         (cx + 1, knee_y - 5 + bob), (cx + 2, foot_y - 10)),
        # f2: 近脚向后 (-2)、远脚向前 (-... 不对，远脚也要向前)
        # 实际上：两腿交替向前向后
        # 近腿在后：脚在 cx-2 ~ cx 这一段（朝右走，所以"后"是 x 小的位置）
        # 远腿在前：脚在 cx+6 左右（前=+x），但远腿位于身体左侧仅是"远观察者"概念
        # 修正：远腿其实视觉上和近腿可以交叉，但远腿应该总是被部分遮挡 + 灰色
        ((cx - 1, knee_y - 1 + bob), (cx - 4, foot_y),
         (cx + 4, knee_y - 1 + bob), (cx + 8, foot_y)),
        # f3: 远脚支撑、近脚抬起经过
        ((cx + 1, knee_y - 5 + bob), (cx + 2, foot_y - 10),
         (cx + 2, knee_y + bob), (cx + 4, foot_y)),
    ]
    near_knee, near_foot, far_knee, far_foot = leg_phases[frame]

    def build_leg(hip, knee, foot, lifted=False):
        thigh = bezier_ord(hip, ((hip[0] + knee[0]) // 2, hip[1] + 4), knee, samples=80)
        shin  = bezier_ord(knee, ((knee[0] + foot[0]) // 2, (knee[1] + foot[1]) // 2 + 2), foot, samples=80)
        leg = tapered(thigh, r_start=4, r_end=3) | tapered(shin, r_start=3, r_end=2)
        # 脚掌
        if not lifted:
            for fx in range(foot[0] - 2, foot[0] + 6):
                leg.add((fx, foot[1]))
                leg.add((fx, foot[1] + 1))
            leg.add((foot[0] - 3, foot[1] + 1))   # 脚跟
            leg.add((foot[0] + 6, foot[1] + 1))   # 脚尖
        else:
            # 抬起的脚（朝下指）
            for fx in range(foot[0] - 1, foot[0] + 4):
                leg.add((fx, foot[1]))
            leg.add((foot[0] + 1, foot[1] + 1))
            leg.add((foot[0] + 2, foot[1] + 1))
        return leg

    near_lifted = (frame == 3)
    far_lifted = (frame == 1)
    near_leg = build_leg((cx + 3, hip_y + bob), near_knee, near_foot, lifted=near_lifted)
    far_leg  = build_leg((cx - 3, hip_y + bob), far_knee, far_foot, lifted=far_lifted)

    # ============ 4. 手臂 ============
    # 与对侧腿同步反向摆动（物理平衡）
    # f0：近腿前 → 近臂向后下垂，远臂向前上甩
    # f2：近腿后 → 近臂向前上甩，远臂向后下垂
    near_shoulder = (cx + 8, shoulder_y + 2 + bob)
    far_shoulder  = (cx - 8, shoulder_y + 2 + bob)

    arm_phases = [
        # (near_elbow, near_hand, far_elbow, far_hand)
        # 朝右走的手臂摆动（与对侧腿反向），手腕统一在大腿外侧 +x
        # f0：近腿在前 → 近臂向后下、远臂向前上
        ((cx + 12, waist_y - 2 + bob), (cx + 14, waist_y + 14 + bob),
         (cx - 10, waist_y - 6 + bob), (cx + 2,  waist_y - 6 + bob)),
        # f1：中位（双臂垂直下垂，手向外撑开避免与大腿重叠）
        ((cx + 14, waist_y + 2 + bob), (cx + 16, waist_y + 14 + bob),
         (cx - 14, waist_y + 2 + bob), (cx - 16, waist_y + 14 + bob)),
        # f2：近腿在后 → 近臂猛甩向前上
        ((cx + 12, waist_y - 6 + bob), (cx + 22, waist_y - 4 + bob),
         (cx - 12, waist_y - 2 + bob), (cx - 14, waist_y + 14 + bob)),
        # f3：中位
        ((cx + 14, waist_y + 2 + bob), (cx + 16, waist_y + 14 + bob),
         (cx - 14, waist_y + 2 + bob), (cx - 16, waist_y + 14 + bob)),
    ]
    n_elbow, n_hand, f_elbow, f_hand = arm_phases[frame]

    near_arm_curve = bezier3_ord(near_shoulder, n_elbow,
                                 ((n_elbow[0] + n_hand[0]) // 2, (n_elbow[1] + n_hand[1]) // 2),
                                 n_hand, samples=150)
    far_arm_curve  = bezier3_ord(far_shoulder, f_elbow,
                                 ((f_elbow[0] + f_hand[0]) // 2, (f_elbow[1] + f_hand[1]) // 2),
                                 f_hand, samples=150)
    near_arm = tapered(near_arm_curve, r_start=5, r_end=3)
    far_arm  = tapered(far_arm_curve,  r_start=5, r_end=3)

    # 手部：3 指爪（毒液质感）
    def claw(hx, hy, facing=1):
        s = disc(hx, hy, 3)
        # 3 根指
        for k, (off_x, off_y, ln) in enumerate([(facing * 0, 3, 4), (facing * 2, 3, 5), (facing * 3, 2, 4)]):
            for t in range(ln):
                s.add((hx + off_x, hy + off_y + t))
                if abs(off_x) >= 1:
                    s.add((hx + off_x + (1 if facing > 0 else -1) * (t // 3), hy + off_y + t))
            s.add((hx + off_x, hy + off_y + ln))   # 指尖
        return s

    near_arm |= claw(*n_hand, facing=1)
    far_arm  |= claw(*f_hand, facing=1)

    # ============ 5. 尾巴 ============
    tail_root = (cx - 7, pelvis_y + 4 + bob)
    tail_phases = [
        # [ctrl1, ctrl2, tip] - 三次贝塞尔
        # f0: 高扬
        [(cx - 16, pelvis_y - 6 + bob), (cx - 28, pelvis_y - 12 + bob), (cx - 38, pelvis_y - 6 + bob)],
        # f1: 中
        [(cx - 18, pelvis_y + bob), (cx - 28, pelvis_y + 2 + bob), (cx - 38, pelvis_y - 2 + bob)],
        # f2: 下垂
        [(cx - 16, pelvis_y + 10 + bob), (cx - 28, pelvis_y + 14 + bob), (cx - 38, pelvis_y + 6 + bob)],
        # f3: 中
        [(cx - 18, pelvis_y + bob), (cx - 28, pelvis_y + 2 + bob), (cx - 38, pelvis_y - 2 + bob)],
    ]
    tc1, tc2, ttip = tail_phases[frame]
    tail_curve = bezier3_ord(tail_root, tc1, tc2, ttip, samples=250)
    tail = tapered(tail_curve, r_start=5, r_end=1)
    # 尾尖拉丝
    if len(tail_curve) >= 4:
        a = tail_curve[-1]
        b = tail_curve[-4]
        dx = a[0] - b[0]
        dy = a[1] - b[1]
        n = math.hypot(dx, dy) or 1
        for k in range(1, 6):
            tail.add((int(a[0] + dx / n * k), int(a[1] + dy / n * k)))

    # ============ 渲染 ============
    # ----- L0：尾巴（最远）-----
    fill_set(canvas, ox, tail, MID)
    for (x, y) in outline(tail):
        if (x, y) not in tail:
            P(canvas, x + ox, y, OUTLINE)
    # 尾巴内侧暗
    tail_inner_dark = set()
    for (x, y) in tail:
        # 顶面（y 较小那侧）画 LIGHT，底面画 SHADOW
        # 找该 x 列的 y 范围
        pass
    # 简化：对尾巴所有像素的下半部分加 SHADOW
    ys_in_tail = {}
    for (x, y) in tail:
        ys_in_tail.setdefault(x, []).append(y)
    for x, ys in ys_in_tail.items():
        if not ys:
            continue
        ymin, ymax = min(ys), max(ys)
        if ymax > ymin:
            # 底像素 = SHADOW，顶像素 = LIGHT
            P(canvas, x + ox, ymax, SHADOW)
            P(canvas, x + ox, ymin, LIGHT)

    # ----- L1：远侧腿 + 远侧臂（整体灰调 + 深灰描边）-----
    far_layer = far_leg | far_arm
    # 整体 SHADOW 底色（比 MID 深，与近层白色形成强对比）
    fill_set(canvas, ox, far_layer, SHADOW)
    # 描边用 DEEP（仍是冷色，但比 OUTLINE 浅）
    middle_layer = torso | neck | head
    front_layer = near_leg | near_arm
    for (x, y) in outline(far_layer):
        if (x, y) not in far_layer and (x, y) not in middle_layer and (x, y) not in front_layer:
            P(canvas, x + ox, y, DEEP)
    # 远侧手指尖端更深
    for (x, y) in far_layer:
        ys_col = [yy for (xx, yy) in far_layer if xx == x]
        if ys_col and y == max(ys_col):
            P(canvas, x + ox, y, DEEP)

    # ----- L2：中层（躯干 + 颈 + 头）-----
    fill_set(canvas, ox, middle_layer, WHITE)
    for (x, y) in outline(middle_layer):
        if (x, y) not in middle_layer and (x, y) not in front_layer:
            P(canvas, x + ox, y, OUTLINE)

    # ----- L2.5：躯干肌肉/阴影分层 -----
    # 沿身体中线偏左 (cx-1) 画一条腹中线阴影
    for y in range(chest_top + 4 + bob, pelvis_y - 2 + bob):
        if (cx - 1, y) in torso:
            P(canvas, cx - 1 + ox, y, MID)
        if (cx, y) in torso:
            P(canvas, cx + ox, y, LIGHT)  # 中线高光
    # 胸大肌：左右两个弧形 MID
    for (x, y) in torso:
        # 左胸弧
        dl = (x - (cx - 4)) ** 2 + (y - (chest_top + 6 + bob)) ** 2
        if 16 <= dl <= 25:
            P(canvas, x + ox, y, MID)
        # 右胸弧
        dr = (x - (cx + 4)) ** 2 + (y - (chest_top + 6 + bob)) ** 2
        if 16 <= dr <= 25:
            P(canvas, x + ox, y, MID)
    # 腹肌：1 像素竖向连续暗线（替代斑点）
    for ay in range(chest_top + 10 + bob, pelvis_y - 4 + bob):
        if (cx - 1, ay) in torso:
            cur = canvas.getpixel((cx - 1 + ox, ay))
            if cur != OUTLINE:
                P(canvas, cx - 1 + ox, ay, MID)
    # 腰侧凹陷阴影
    for y in range(46 + bob, 54 + bob):
        for x_off in (-9, -8, 8, 9):
            xx = cx + x_off
            if (xx, y) in torso:
                P(canvas, xx + ox, y, MID)
    # 骨盆下缘 MID
    for (x, y) in torso:
        if pelvis_y - 1 + bob <= y <= pelvis_y + 2 + bob:
            if abs(x - cx) >= 3:
                cur = canvas.getpixel((x + ox, y))
                if cur == WHITE:
                    P(canvas, x + ox, y, MID)

    # 颈部下半阴影
    for (x, y) in neck:
        if y >= neck_top + 2 + bob:
            P(canvas, x + ox, y, MID)

    # 头部底部阴影（下颚）
    for (x, y) in head:
        if y >= head_cy + 2 + bob:
            d = (x - cx - 1) ** 2 + (y - head_cy - bob) ** 2
            if d >= 25:
                P(canvas, x + ox, y, MID)
    # 头顶高光
    for (x, y) in head:
        if y <= head_cy - head_r + 3 + bob and abs(x - cx) <= 2:
            P(canvas, x + ox, y, LIGHT)

    # 头顶拉丝
    for (x, y) in head_strands:
        P(canvas, x + ox, y, OUTLINE)
    # 拉丝中线高光
    for (x, y) in head_strands:
        if y < head_cy - head_r + bob:
            if (x + y) % 2 == 0:
                pass

    # ----- L3：近侧腿 -----
    fill_set(canvas, ox, near_leg, WHITE)
    for (x, y) in outline(near_leg):
        if (x, y) not in near_leg and (x, y) not in near_arm:
            P(canvas, x + ox, y, OUTLINE)
    # 近腿肌肉阴影：大腿后侧 + 小腿内侧
    for (x, y) in near_leg:
        # 该 y 行最左点 + 1 = 大腿后侧暗带
        xs = [px for (px, py) in near_leg if py == y]
        if xs:
            lx = min(xs)
            if x == lx + 1 and hip_y + bob <= y <= knee_y + bob:
                P(canvas, x + ox, y, MID)
            # 大腿前侧高光
            rx = max(xs)
            if x == rx - 1 and hip_y + bob <= y <= knee_y + bob:
                P(canvas, x + ox, y, LIGHT)

    # ----- L4：近侧臂（最顶）-----
    fill_set(canvas, ox, near_arm, WHITE)
    # 近臂外轮廓
    for (x, y) in outline(near_arm):
        if (x, y) not in near_arm:
            P(canvas, x + ox, y, OUTLINE)
    # 近臂"贴身侧"内描边（关键：让臂与躯干分离）
    # 找近臂每行最左点 → 如果该点在 torso/middle_layer 内，把它本身画成 OUTLINE
    for (x, y) in near_arm:
        xs = [px for (px, py) in near_arm if py == y]
        if not xs:
            continue
        lx = min(xs)
        # 仅在手臂"靠身体一侧"位于躯干范围内时画内描边
        if x == lx and (x, y) in middle_layer:
            P(canvas, x + ox, y, OUTLINE)
        # 大臂内侧 + 1 像素阴影
        if x == lx + 1 and shoulder_y + bob <= y <= waist_y + 14 + bob:
            cur = canvas.getpixel((x + ox, y))
            if cur == WHITE:
                P(canvas, x + ox, y, MID)
        # 大臂外缘 - 1 像素高光
        rx = max(xs)
        if x == rx - 1 and shoulder_y + bob <= y <= waist_y + 14 + bob:
            cur = canvas.getpixel((x + ox, y))
            if cur == WHITE:
                P(canvas, x + ox, y, LIGHT)

    # ----- L5：滴液 / 黏液 -----
    drops_per_frame = [
        # f0
        [(cx + 10, shoulder_y + 9 + bob), (n_hand[0] - 1, n_hand[1] + 5),
         (ttip[0] + 1, ttip[1] + 2)],
        # f1
        [(cx - 10, shoulder_y + 11 + bob), (n_hand[0] + 1, n_hand[1] + 6),
         (ttip[0] - 1, ttip[1] + 4)],
        # f2
        [(cx + 10, shoulder_y + 9 + bob), (f_hand[0] - 1, f_hand[1] + 5),
         (ttip[0] + 1, ttip[1] + 2), (near_foot[0] + 4, near_foot[1] + 2)],
        # f3
        [(cx - 10, shoulder_y + 11 + bob), (n_hand[0] + 1, n_hand[1] + 6),
         (ttip[0] - 1, ttip[1] + 4)],
    ]
    for (dx, dy) in drops_per_frame[frame]:
        body = {
            (dx, dy), (dx + 1, dy), (dx + 2, dy),
            (dx, dy + 1), (dx + 1, dy + 1), (dx + 2, dy + 1),
            (dx + 1, dy + 2),
        }
        strand = {(dx + 1, dy - 1), (dx + 1, dy - 2)}
        for (px, py) in body:
            P(canvas, px + ox, py, SLIME)
        for (px, py) in strand:
            P(canvas, px + ox, py, SLIME)
        for (px, py) in outline(body):
            if (px, py) not in body and (px, py) not in strand:
                P(canvas, px + ox, py, SLIME_EDGE)
        # 顶部高光
        P(canvas, dx + 1 + ox, dy, LIGHT)


# === 主程序 ===
def main():
    sheet = Image.new("RGBA", (SHEET_W, SHEET_H), T)
    for f in range(FRAMES):
        draw_frame(sheet, f * CELL, f)

    sheet.save(os.path.join(OUT_DIR, "draft_venom_96.png"))
    big = sheet.resize((SHEET_W * SCALE_PREVIEW, SHEET_H * SCALE_PREVIEW), Image.NEAREST)
    big.save(os.path.join(OUT_DIR, "draft_venom_big.png"))

    # 48x48 下采样
    small = Image.new("RGBA", (FINAL_CELL * FRAMES, FINAL_CELL), T)
    for f in range(FRAMES):
        sub = sheet.crop((f * CELL, 0, (f + 1) * CELL, CELL)).resize((FINAL_CELL, FINAL_CELL), Image.LANCZOS)
        small.paste(sub, (f * FINAL_CELL, 0))
    small.save(os.path.join(OUT_DIR, "draft_venom_48.png"))
    small.resize((FINAL_CELL * FRAMES * 6, FINAL_CELL * 6), Image.NEAREST).save(os.path.join(OUT_DIR, "draft_venom_48_x6.png"))

    # GIF
    frames = []
    for f in range(FRAMES):
        sub = sheet.crop((f * CELL, 0, (f + 1) * CELL, CELL))
        bg = Image.new("RGBA", sub.size, (38, 42, 52, 255))
        bg.alpha_composite(sub)
        bg = bg.resize((sub.width * 4, sub.height * 4), Image.NEAREST)
        frames.append(bg.convert("P", palette=Image.ADAPTIVE))
    frames[0].save(os.path.join(OUT_DIR, "draft_venom.gif"), save_all=True, append_images=frames[1:],
                   duration=83, loop=0, disposal=2)
    print("OK")


if __name__ == "__main__":
    main()
