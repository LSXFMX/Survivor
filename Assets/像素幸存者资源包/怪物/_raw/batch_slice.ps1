# Batch slice+clean. Args come in triples: <sheet> <outDir> <prefix> <sheet> <outDir> <prefix> ...
Add-Type -AssemblyName System.Drawing
$code = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class SliceClean
{
    static byte[] _bytes;
    static int _stride;

    static bool IsBgGray(int o, int tol, int minB, int maxB)
    {
        byte B=_bytes[o],G=_bytes[o+1],R=_bytes[o+2];
        int mx=Math.Max(R,Math.Max(G,B));
        int mn=Math.Min(R,Math.Min(G,B));
        return (mx-mn)<=tol && mx>=minB && mx<=maxB;
    }

    public static void Run(string sheet, string outDir, string prefix, int tol, int minBright, int maxBright)
    {
        Bitmap src;
        using (var fs = System.IO.File.OpenRead(sheet))
        using (var tmp = Image.FromStream(fs))
            src = new Bitmap(tmp);
        int SW = src.Width, SH = src.Height;
        int cw = SW / 4;

        var frames = new byte[4][];
        var strides = new int[4];
        int[] minX = {cw,cw,cw,cw}, minY = {SH,SH,SH,SH}, maxX = {0,0,0,0}, maxY = {0,0,0,0};

        for (int i = 0; i < 4; i++)
        {
            var bmp = new Bitmap(cw, SH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.DrawImage(src, new Rectangle(0,0,cw,SH), new Rectangle(i*cw,0,cw,SH), GraphicsUnit.Pixel);
            }
            var data = bmp.LockBits(new Rectangle(0,0,cw,SH), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            _stride = data.Stride;
            int len = _stride * SH;
            _bytes = new byte[len];
            Marshal.Copy(data.Scan0, _bytes, 0, len);

            // (2) remove neutral-gray checkerboard everywhere
            for (int y=0;y<SH;y++){ int row=y*_stride; for(int x=0;x<cw;x++){ int o=row+x*4; if(_bytes[o+3]!=0 && IsBgGray(o,tol,minBright,maxBright)) _bytes[o+3]=0; } }

            // (3) remove thin wide-solid ground baseline at bottom, with thickness guard (<=14 rows)
            int wideRun = 0;
            for (int y=SH-1; y>=0; y--)
            {
                int row=y*_stride; int lo=-1,hi=-1,cnt=0;
                for(int x=0;x<cw;x++){ if(_bytes[row+x*4+3]!=0){ if(lo<0)lo=x; hi=x; cnt++; } }
                if (lo<0) { if (wideRun>0) break; else continue; }
                int span=hi-lo+1;
                bool wideSolid = span >= (int)(cw*0.60) && cnt >= (int)(span*0.85);
                if (wideSolid) wideRun++; else break;
            }
            if (wideRun > 0 && wideRun <= 14)
            {
                int deleted = 0;
                for (int y=SH-1; y>=0 && deleted<wideRun; y--)
                {
                    int row=y*_stride; int lo=-1,hi=-1,cnt=0;
                    for(int x=0;x<cw;x++){ if(_bytes[row+x*4+3]!=0){ if(lo<0)lo=x; hi=x; cnt++; } }
                    if (lo<0) continue;
                    int span=hi-lo+1;
                    bool wideSolid = span >= (int)(cw*0.60) && cnt >= (int)(span*0.85);
                    if (wideSolid) { for(int x=0;x<cw;x++) _bytes[row+x*4+3]=0; deleted++; }
                    else break;
                }
            }

            for (int y=0;y<SH;y++){ int row=y*_stride; for(int x=0;x<cw;x++){ if(_bytes[row+x*4+3]>20){ if(x<minX[i])minX[i]=x; if(x>maxX[i])maxX[i]=x; if(y<minY[i])minY[i]=y; if(y>maxY[i])maxY[i]=y; } } }

            Marshal.Copy(_bytes, 0, data.Scan0, len);
            bmp.UnlockBits(data);
            frames[i]=_bytes; strides[i]=_stride;
        }

        int contentW=0, contentH=0;
        for(int i=0;i<4;i++){ if(maxX[i]>=minX[i]){ contentW=Math.Max(contentW, maxX[i]-minX[i]+1); contentH=Math.Max(contentH, maxY[i]-minY[i]+1); } }
        int margin = 12;
        int outW = contentW + margin*2;
        int outH = contentH + margin*2;

        if(!System.IO.Directory.Exists(outDir)) System.IO.Directory.CreateDirectory(outDir);

        for (int i=0;i<4;i++)
        {
            var outBmp = new Bitmap(outW, outH, PixelFormat.Format32bppArgb);
            var od = outBmp.LockBits(new Rectangle(0,0,outW,outH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int olen = od.Stride*outH;
            var ob = new byte[olen];
            Marshal.Copy(od.Scan0, ob, 0, olen);

            int fw = maxX[i]-minX[i]+1;
            int fh = maxY[i]-minY[i]+1;
            int dstX0 = (outW - fw)/2;
            int dstY0 = (outH - margin) - fh;

            int sstride = strides[i];
            var sb = frames[i];
            for (int yy=0; yy<fh; yy++)
            {
                int sy = minY[i]+yy;
                int dy = dstY0+yy;
                if (dy<0||dy>=outH) continue;
                for (int xx=0; xx<fw; xx++)
                {
                    int sx = minX[i]+xx;
                    int dx = dstX0+xx;
                    if (dx<0||dx>=outW) continue;
                    int so = sy*sstride + sx*4;
                    int doo = dy*od.Stride + dx*4;
                    ob[doo]=sb[so]; ob[doo+1]=sb[so+1]; ob[doo+2]=sb[so+2]; ob[doo+3]=sb[so+3];
                }
            }
            Marshal.Copy(ob, 0, od.Scan0, olen);
            outBmp.UnlockBits(od);
            string outPath = System.IO.Path.Combine(outDir, prefix + (i+1) + ".png");
            outBmp.Save(outPath, ImageFormat.Png);
            outBmp.Dispose();
        }
        src.Dispose();
        Console.WriteLine("done " + prefix + " -> " + outW + "x" + outH);
    }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
for ($i = 0; $i -lt $args.Count; $i += 3) {
    [SliceClean]::Run($args[$i], $args[$i+1], $args[$i+2], 24, 150, 252)
}
