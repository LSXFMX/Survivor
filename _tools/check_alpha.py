"""检查 ai_nobg.png 是否真的有透明像素"""
from PIL import Image
img = Image.open(r"d:\Survivor\Survivor\_tools\preview\ai_nobg.png").convert("RGBA")
print("Size:", img.size, "mode:", img.mode)
samples = [(5,5), (1250,5), (5,840), (600,420), (10,500)]
for x, y in samples:
    p = img.getpixel((x,y))
    print(f"  ({x:4d},{y:3d}) → {p}  alpha={p[3]}")

# 统计 alpha=0 的像素数
w, h = img.size
zero = sum(1 for x in range(0,w,4) for y in range(0,h,4) if img.getpixel((x,y))[3] == 0)
total = (w//4)*(h//4)
print(f"\n采样 alpha=0 像素: {zero}/{total} = {zero*100/total:.1f}%")
