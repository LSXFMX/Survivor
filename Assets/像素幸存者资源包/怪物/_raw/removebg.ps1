Add-Type -AssemblyName System.Drawing
$code = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public static class BgRemover
{
    public static string Process(string path, int tol, int minBright, int maxBright)
    {
        Bitmap bmp;
        using (var fs = System.IO.File.OpenRead(path))
        using (var tmp = Image.FromStream(fs))
            bmp = new Bitmap(tmp);

        int w = bmp.Width, h = bmp.Height;
        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        int len = stride * h;
        byte[] bytes = new byte[len];
        Marshal.Copy(data.Scan0, bytes, 0, len);

        bool[] visited = new bool[w * h];
        var stack = new Stack<int>();
        for (int x = 0; x < w; x++) { stack.Push(x); stack.Push((h - 1) * w + x); }
        for (int y = 0; y < h; y++) { stack.Push(y * w); stack.Push(y * w + (w - 1)); }

        int removed = 0;
        while (stack.Count > 0)
        {
            int idx = stack.Pop();
            if (idx < 0 || idx >= w * h) continue;
            if (visited[idx]) continue;
            visited[idx] = true;
            int px = idx % w, py = idx / w;
            int o = py * stride + px * 4;
            byte B = bytes[o], G = bytes[o + 1], R = bytes[o + 2];
            int mx = Math.Max(R, Math.Max(G, B));
            int mn = Math.Min(R, Math.Min(G, B));
            bool isBg = (mx - mn) <= tol && mx >= minBright && mx <= maxBright;
            if (!isBg) continue;
            bytes[o + 3] = 0;
            removed++;
            if (px > 0)     stack.Push(idx - 1);
            if (px < w - 1) stack.Push(idx + 1);
            if (py > 0)     stack.Push(idx - w);
            if (py < h - 1) stack.Push(idx + w);
        }
        Marshal.Copy(bytes, 0, data.Scan0, len);
        bmp.UnlockBits(data);
        bmp.Save(path, ImageFormat.Png);
        bmp.Dispose();
        return path + " removed=" + removed;
    }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
foreach ($p in $args) {
    Write-Output ([BgRemover]::Process($p, 24, 170, 252))
}
