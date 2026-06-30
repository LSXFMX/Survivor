import os
import shutil

src = r"d:\Survivor\Survivor\Assets\Resources\像素幸存者资源包\存档装备图标\通关装备\Pixel_art_game_equipment_icon__2026-06-30T02-56-00.png"
dst = r"d:\Survivor\Survivor\Assets\Resources\像素幸存者资源包\存档装备图标\通关装备\022.png"

if os.path.exists(src):
    # 用 replace 直接替换（Windows 上需要先删除目标）
    if os.path.exists(dst):
        os.remove(dst)
    shutil.move(src, dst)
    print("022.png 已覆盖为皮毛之甲")
else:
    print("源文件不存在:", src)
