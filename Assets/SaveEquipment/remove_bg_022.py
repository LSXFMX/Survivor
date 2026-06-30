from PIL import Image
import os

png_dir = r"d:\Survivor\Survivor\Assets\Resources\像素幸存者资源包\存档装备图标\通关装备"
f = "022.png"
img_path = os.path.join(png_dir, f)
img = Image.open(img_path).convert("RGBA")
data = list(img.getdata())
new_data = []
for i, (r2, g2, b2, a) in enumerate(data):
    if r2 < 30 and g2 > 200 and b2 < 30:
        new_data.append((0, 0, 0, 0))
    else:
        new_data.append((r2, g2, b2, a))
img.putdata(new_data)
img.save(img_path)
print("022.png 去背完成")
