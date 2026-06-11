"""采样背景棋盘格的实际 RGB 值"""
from PIL import Image
img = Image.open(r"d:\Survivor\Survivor\_tools\preview\A_pixel_art_sprite_sheet_of_a__2026-06-09T03-30-32.png").convert("RGBA")
print("Size:", img.size)
# 采几个肯定是背景的位置：4 个角 + 中间空白
samples = [(5,5), (1250,5), (5,840), (1250,840), (600,420), (10,500), (1000,500)]
for x, y in samples:
    print(f"  ({x:4d},{y:3d}) → {img.getpixel((x,y))}")
