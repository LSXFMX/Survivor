"""
存档装备图标去背脚本
将 018-035.png 的绿色 chroma key 背景 (#00FF00) 扣除，转为透明背景
"""
import os
import glob
from PIL import Image

def remove_green_background(input_path, output_path=None, tolerance=60):
    """
    将图片中的绿色背景 (约 #00FF00) 替换为透明
    
    Args:
        input_path: 输入图片路径
        output_path: 输出图片路径（None 则覆盖原文件）
        tolerance: 颜色容差（0-255），越大越激进
    """
    if output_path is None:
        output_path = input_path

    img = Image.open(input_path).convert("RGBA")
    datas = img.getdata()
    newData = []

    for item in datas:
        # 检测绿色背景：G 通道高，R 和 B 通道低
        r, g, b, a = item
        # 目标颜色：#00FF00 或接近的绿色
        if g > 150 and r < 100 and b < 100:
            # 完全透明
            newData.append((0, 0, 0, 0))
        elif g > 180 and r < 80 and b < 80:
            newData.append((0, 0, 0, 0))
        else:
            newData.append(item)

    img.putdata(newData)
    img.save(output_path, "PNG")
    print(f"  [OK] {os.path.basename(input_path)} -> 去背完成")

def main():
    base_dir = r"d:\Survivor\Survivor\Assets\Resources\像素幸存者资源包\存档装备图标\通关装备"
    
    print("=" * 50)
    print("开始处理 018-035 去背...")
    print("=" * 50)
    
    processed = 0
    for i in range(18, 36):
        filename = f"{i:03d}.png"
        filepath = os.path.join(base_dir, filename)
        
        if os.path.exists(filepath):
            try:
                remove_green_background(filepath)
                processed += 1
            except Exception as e:
                print(f"  [ERR] {filename}: {e}")
        else:
            print(f"  [SKIP] {filename} 不存在")
    
    print("=" * 50)
    print(f"完成！共处理 {processed} 个文件")
    print("=" * 50)

if __name__ == "__main__":
    main()
