# -*- coding: utf-8 -*-
"""
红月分身形贴图预处理：
1. 抠除暗蓝近黑色背景（变透明）
2. 缩放到 256x256（游戏中合适的展示尺寸）
3. 原地覆盖回原路径与所有拷贝
"""
from PIL import Image
import os

src_path = r'd:\Survivor\Survivor\Assets\Resources\Wolf\RedMoonClonePet_sprite.png'
im = Image.open(src_path).convert('RGBA')
w, h = im.size
print('原图尺寸', w, h)

# 采样四角背景色
corners = [(2, 2), (w-3, 2), (2, h-3), (w-3, h-3)]
for c in corners:
    print('角', c, 'RGBA', im.getpixel(c))
print('中心', im.getpixel((w//2, h//2)))

# 取右上角作背景参考（最不与红月重叠的边角）
bg_r, bg_g, bg_b = im.getpixel(corners[1])[:3]
print('使用背景色 RGB=', (bg_r, bg_g, bg_b))

# 转数组
px = im.load()

# 抠背景：把每个像素与背景色做欧氏距离比较，远的保留，近的设为全透明
TOL = 60
TOL2 = TOL * TOL
removed = 0
for y in range(h):
    for x in range(w):
        r, g, b, a = px[x, y]
        dr = r - bg_r
        dg = g - bg_g
        db = b - bg_b
        d2 = dr*dr + dg*dg + db*db
        if d2 < TOL2:
            # 同时要求 RGB 都偏暗（暗蓝近黑），避免误伤红月边缘深色像素
            if r < 70 and g < 70 and b < 90:
                px[x, y] = (0, 0, 0, 0)
                removed += 1
print('扣除像素数', removed)

# 找到非透明内容包围盒
bbox = im.getbbox()
print('内容包围盒', bbox)
if bbox:
    # 按内容裁剪（去掉透明边距）
    im_cropped = im.crop(bbox)
    # 等比缩放到 256 长边
    target = 256
    w2, h2 = im_cropped.size
    scale = target / max(w2, h2)
    new_w = max(1, int(w2 * scale))
    new_h = max(1, int(h2 * scale))
    im_resized = im_cropped.resize((new_w, new_h), Image.NEAREST)
    print('缩放后尺寸', im_resized.size)
    # 保存到原路径
    im_resized.save(src_path, optimize=True)
    print('已覆盖原文件:', src_path)
else:
    print('包围盒为空，未保存')
