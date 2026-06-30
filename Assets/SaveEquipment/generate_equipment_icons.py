#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
生成 018-035 号通关装备图标（像素风，纯色背景方便去背）
用法: python generate_equipment_icons.py
需要在 Unity 项目目录的 Assets/SaveEquipment/ 下运行
"""

from PIL import Image, ImageDraw
import os

OUTPUT_DIR = r"d:\Survivor\Survivor\Assets\Resources\像素幸存者资源包\存档装备图标\通关装备"
SIZE = 1024  # 高分辨率，方便在 Unity 里缩放

# 装备定义: (id, 名称, 颜色, 图标类型)
EQUIPMENTS = [
    # N8 (id 18-20): 和平之剑/甲/心
    (18, "和平之剑", (200, 220, 255), "sword_peace"),
    (19, "和平之甲", (180, 200, 240), "armor_peace"),
    (20, "和平之心", (220, 240, 255), "heart_peace"),
    # N9 (id 21-23): 利爪之剑/皮毛之甲/野兽之心
    (21, "利爪之剑", (255, 200, 100), "sword_claw"),
    (22, "皮毛之甲", (200, 160, 100), "armor_fur"),
    (23, "野兽之心", (255, 180, 80), "heart_beast"),
    # N10 (id 24-26): 月牙之剑/月圆之甲/月球之心
    (24, "月牙之剑", (200, 220, 255), "sword_crescent"),
    (25, "月圆之甲", (220, 240, 255), "armor_moon"),
    (26, "月球之心", (255, 255, 200), "heart_moon"),
    # N11 (id 27-29): 粘液之剑/甲/心
    (27, "粘液之剑", (100, 255, 100), "sword_slime"),
    (28, "粘液之甲", (80, 200, 80), "armor_slime"),
    (29, "粘液之心", (150, 255, 150), "heart_slime"),
    # N12 (id 30-32): 暗影之剑/甲/心
    (30, "暗影之剑", (150, 100, 255), "sword_shadow"),
    (31, "暗影之甲", (100, 80, 200), "armor_shadow"),
    (32, "暗影之心", (200, 150, 255), "heart_shadow"),
    # N13 (id 33-35): 龙鳞之剑/甲/黄金睛
    (33, "龙鳞之剑", (255, 200, 50), "sword_dragon"),
    (34, "龙鳞之甲", (255, 180, 30), "armor_dragon"),
    (35, "黄金睛", (255, 220, 0), "eye_golden"),
]

def draw_sword(draw, cx, cy, color, variant="normal"):
    """画一把剑"""
    # 剑柄
    draw.rectangle([cx-20, cy+80, cx+20, cy+160], fill=color)
    draw.rectangle([cx-40, cy+120, cx+40, cy+140], fill=color)
    # 护手
    draw.rectangle([cx-60, cy+40, cx+60, cy+80], fill=color)
    # 剑身
    points = [(cx, cy-200), (cx-30, cy-40), (cx+30, cy-40)]
    draw.polygon(points, fill=color)
    # 高光
    highlight = tuple(min(255, c+50) for c in color)
    draw.rectangle([cx-10, cy-180, cx+10, cy-60], fill=highlight)

def draw_armor(draw, cx, cy, color, variant="normal"):
    """画一件胸甲"""
    # 主体
    draw.rectangle([cx-120, cy-80, cx+120, cy+160], fill=color)
    # 肩甲
    draw.ellipse([cx-180, cy-140, cx-60, cy-20], fill=color)
    draw.ellipse([cx+60, cy-140, cx+180, cy-20], fill=color)
    # 腰带
    belt_color = tuple(max(0, c-60) for c in color)
    draw.rectangle([cx-120, cy+80, cx+120, cy+120], fill=belt_color)
    # 高光
    highlight = tuple(min(255, c+40) for c in color)
    draw.rectangle([cx-80, cy-60, cx-40, cy+60], fill=highlight)

def draw_heart(draw, cx, cy, color, variant="normal"):
    """画一个心形宝石"""
    # 心形（两个圆 + 三角）
    r = 80
    draw.ellipse([cx-r, cy-r, cx, cy+r], fill=color)
    draw.ellipse([cx, cy-r, cx+r, cy+r], fill=color)
    draw.polygon([(cx-r, cy+20), (cx+r, cy+20), (cx, cy+120)], fill=color)
    # 内部符号
    symbol_color = tuple(min(255, c+80) for c in color)
    if variant == "peace":
        # 和平符号
        draw.line([cx, cy-40, cx, cy+40], fill=symbol_color, width=8)
        draw.arc([cx-30, cy-30, cx+30, cy+30], 0, 360, fill=symbol_color, width=8)
    elif variant == "beast":
        # 爪痕
        for i in range(3):
            draw.line([cx-30+i*20, cy-40, cx-20+i*20, cy+40], fill=symbol_color, width=6)
    elif variant == "moon":
        # 月牙
        draw.ellipse([cx-60, cy-60, cx+60, cy+60], fill=(0, 255, 0))  # 背景绿，挖空效果
        draw.ellipse([cx-40, cy-40, cx+80, cy+80], fill=color)
    elif variant == "slime":
        # 粘液滴
        draw.ellipse([cx-30, cy-20, cx+30, cy+40], fill=symbol_color)
    elif variant == "shadow":
        # 暗影漩涡
        for i in range(4):
            r2 = 50 - i*10
            draw.ellipse([cx-r2, cy-r2, cx+r2, cy+r2], outline=symbol_color, width=4)
    elif variant == "dragon":
        # 龙鳞纹
        for i in range(5):
            draw.ellipse([cx-60+i*20, cy-30, cx-40+i*20, cy+30], fill=symbol_color)
    # 发光效果
    glow = (*color, 80)
    # PIL 不支持 alpha，用更亮的颜色代替
    glow_color = tuple(min(255, c+100) for c in color)
    draw.ellipse([cx-100, cy-100, cx+100, cy+100], outline=glow_color, width=4)

def draw_eye(draw, cx, cy, color, variant="golden"):
    """画一只眼睛（黄金睛）"""
    # 眼眶
    draw.ellipse([cx-100, cy-60, cx+100, cy+60], fill=color)
    # 瞳孔
    pupil_color = (80, 60, 0)
    draw.ellipse([cx-40, cy-30, cx+40, cy+30], fill=pupil_color)
    # 高光
    draw.ellipse([cx-20, cy-40, cx+10, cy-10], fill=(255, 255, 200))
    # 鳞片纹路
    scale_color = tuple(max(0, c-40) for c in color)
    for i in range(6):
        draw.arc([cx-90+i*20, cy-50, cx-70+i*20, cy+50], 0, 180, fill=scale_color, width=3)

def generate_icon(equip_id, name, color, icon_type):
    """生成单个图标"""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 255, 0, 255))  # 纯绿背景
    draw = ImageDraw.Draw(img)
    
    cx, cy = SIZE // 2, SIZE // 2
    
    if "sword" in icon_type:
        variant = icon_type.replace("sword_", "")
        draw_sword(draw, cx, cy, color, variant)
    elif "armor" in icon_type:
        variant = icon_type.replace("armor_", "")
        draw_armor(draw, cx, cy, color, variant)
    elif "heart" in icon_type:
        variant = icon_type.replace("heart_", "")
        draw_heart(draw, cx, cy, color, variant)
    elif "eye" in icon_type:
        draw_eye(draw, cx, cy, color, icon_type)
    
    # 保存
    filename = f"{equip_id:03d}.png"
    filepath = os.path.join(OUTPUT_DIR, filename)
    img.save(filepath, "PNG")
    print(f"已生成: {filename} ({name})")

def main():
    if not os.path.exists(OUTPUT_DIR):
        os.makedirs(OUTPUT_DIR)
    
    for equip_id, name, color, icon_type in EQUIPMENTS:
        generate_icon(equip_id, name, color, icon_type)
    
    print(f"\n共生成 {len(EQUIPMENTS)} 张图标，保存在:\n{OUTPUT_DIR}")

if __name__ == "__main__":
    main()
