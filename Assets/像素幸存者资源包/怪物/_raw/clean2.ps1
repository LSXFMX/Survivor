# Args: <sheetPath> <outDir> <prefix>
# Robust background removal for AI-generated 4-frame side sheets:
#  1) slice into 4 frames
#  2) edge flood-fill removes connected NEUTRAL background (checker gray / white / shadow band, any brightness)
#  3) erode the outer non-solid rim (dispersed red glow haze / soft antialias fringe), stops at solid outline/pure colors
#  4) connected-component analysis: keep only large components (removes black cell divider lines & speckle noise)
#  5) bottom-align + horizontally center every frame onto a common canvas (no drift)
Add-Type -AssemblyName System.Drawing
$code = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public static class Clean2
{
    // background = neutral (low saturation) AND bright enough (excludes dark character outline)
    static bool IsBg(byte[] b, int o)
    {
        if (b[o+3]==0) return true;
        int B=b[o],G=b[o+1],R=b[o+2];
        int mx=Math.Max(R,Math.Max(G,B));
        int mn=Math.Min(R,Math.Min(G,B));
        return (mx-mn)<=70 && mx>=85;
    }

    // solid = part of the character we must never erode: dark outline/black fur OR pure saturated color (red/yellow)
    static bool IsSolid(byte[] b, int o)
    {
        int B=b[o],G=b[o+1],R=b[o+2];
        int mx=Math.Max(R,Math.Max(G,B));
        int mn=Math.Min(R,Math.Min(G,B));
        if (mx < 70) return true;        // black outline / black fur
        if (mx-mn > 110) return true;    // pure saturated color (red eye, red rim, yellow eye)
        return false;
    }

    public static void Run(string sheet, string outDir, string prefix)
    {
        Bitmap src;
        using (var fs = System.IO.File.OpenRead(sheet))
        using (var tmp = Image.FromStream(fs))
            src = new Bitmap(tmp);
        int SW = src.Width, SH = src.Height;
        int cw = SW / 4;
        int W = cw, H = SH;

        var frames = new byte[4][];
        int stride = 0;
        int[] minX={cw,cw,cw,cw}, minY={SH,SH,SH,SH}, maxX={0,0,0,0}, maxY={0,0,0,0};

        for (int i = 0; i < 4; i++)
        {
            var bmp = new Bitmap(cw, SH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.DrawImage(src, new Rectangle(0,0,cw,SH), new Rectangle(i*cw,0,cw,SH), GraphicsUnit.Pixel);
            }
            var data = bmp.LockBits(new Rectangle(0,0,cw,SH), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            stride = data.Stride;
            int len = stride*SH;
            var b = new byte[len];
            Marshal.Copy(data.Scan0, b, 0, len);

            // ---- (1) edge flood fill: clear connected neutral background ----
            var visited = new bool[W*H];
            var stack = new Stack<int>();
            for (int x=0;x<W;x++){ TrySeed(b,stride,visited,stack,x,0,W); TrySeed(b,stride,visited,stack,x,H-1,W); }
            for (int y=0;y<H;y++){ TrySeed(b,stride,visited,stack,0,y,W); TrySeed(b,stride,visited,stack,W-1,y,W); }
            while (stack.Count>0)
            {
                int idx=stack.Pop();
                int x=idx%W, y=idx/W;
                b[y*stride+x*4+3]=0;
                TryPush(b,stride,visited,stack,x+1,y,W,H);
                TryPush(b,stride,visited,stack,x-1,y,W,H);
                TryPush(b,stride,visited,stack,x,y+1,W,H);
                TryPush(b,stride,visited,stack,x,y-1,W,H);
            }

            // ---- (2) erode outer non-solid rim (red glow haze / soft fringe), up to N layers ----
            int ERODE = 14;
            for (int step=0; step<ERODE; step++)
            {
                var toClear = new List<int>();
                for (int y=0;y<H;y++)
                {
                    int row=y*stride;
                    for (int x=0;x<W;x++)
                    {
                        int o=row+x*4;
                        if (b[o+3]==0) continue;
                        // neighbor transparent?
                        bool edge=false;
                        if (x+1>=W || b[row+(x+1)*4+3]==0) edge=true;
                        else if (x-1<0 || b[row+(x-1)*4+3]==0) edge=true;
                        else if (y+1>=H || b[(y+1)*stride+x*4+3]==0) edge=true;
                        else if (y-1<0 || b[(y-1)*stride+x*4+3]==0) edge=true;
                        if (!edge) continue;
                        if (!IsSolid(b,o)) toClear.Add(o);
                    }
                }
                if (toClear.Count==0) break;
                foreach(int o in toClear) b[o+3]=0;
            }

            // ---- (2b) clear bright neutral pixels left inside interior holes (leg/arm gaps) ----
            // Flood fill only removes background connected to the image border; the checker/white
            // trapped between the legs is enclosed by the character, so clear any bright neutral pixel.
            for (int y=0;y<H;y++)
            {
                int row=y*stride;
                for (int x=0;x<W;x++)
                {
                    int o=row+x*4;
                    if (b[o+3]==0) continue;
                    int BB=b[o],GG=b[o+1],RR=b[o+2];
                    int mx=Math.Max(RR,Math.Max(GG,BB));
                    int mn=Math.Min(RR,Math.Min(GG,BB));
                    if (mx>=95 && (mx-mn)<=42) b[o+3]=0; // gray/white hole background (checker light+dark), character dark parts have mx<85
                }
            }

            // ---- (2b2) clear DARK neutral leftovers (very dark checker ~mx72, same value as black outline) ----
            // Cannot separate by color (outline == dark checker). Use SPACE: the character's black outline is
            // always adjacent to its COLORED parts (red rim / yellow eye / dark-red). Build a protected zone by
            // dilating colored pixels; then clear dark-neutral pixels that fall OUTSIDE that zone (floating checker).
            {
                var prot = new bool[W*H];
                int RAD = 15;
                for (int y=0;y<H;y++)
                {
                    int row=y*stride;
                    for (int x=0;x<W;x++)
                    {
                        int o=row+x*4;
                        if (b[o+3]==0) continue;
                        int BB=b[o],GG=b[o+1],RR=b[o+2];
                        int mx=Math.Max(RR,Math.Max(GG,BB));
                        int mn=Math.Min(RR,Math.Min(GG,BB));
                        if ((mx-mn)>=18 && mx>=45) // a saturated (colored) character pixel
                        {
                            int x0=Math.Max(0,x-RAD), x1=Math.Min(W-1,x+RAD);
                            int y0=Math.Max(0,y-RAD), y1=Math.Min(H-1,y+RAD);
                            for (int yy=y0;yy<=y1;yy++){ int r2=yy*W; for(int xx=x0;xx<=x1;xx++) prot[r2+xx]=true; }
                        }
                    }
                }
                for (int y=0;y<H;y++)
                {
                    int row=y*stride;
                    for (int x=0;x<W;x++)
                    {
                        int o=row+x*4;
                        if (b[o+3]==0) continue;
                        int BB=b[o],GG=b[o+1],RR=b[o+2];
                        int mx=Math.Max(RR,Math.Max(GG,BB));
                        int mn=Math.Min(RR,Math.Min(GG,BB));
                        if (mx>=50 && mx<=100 && (mx-mn)<=8 && !prot[y*W+x]) b[o+3]=0; // floating dark checker
                    }
                }
            }

            // ---- (2c) remove isolated vertical divider lines (tall dark column with empty sides) ----
            {
                int[] darkCol=new int[W]; int[] anyCol=new int[W];
                for (int x=0;x<W;x++)
                {
                    int dc=0,ac=0;
                    for (int y=0;y<H;y++)
                    {
                        int o=y*stride+x*4;
                        if (b[o+3]!=0)
                        {
                            ac++;
                            int BB=b[o],GG=b[o+1],RR=b[o+2];
                            int mx=Math.Max(RR,Math.Max(GG,BB));
                            if (mx<70) dc++;
                        }
                    }
                    darkCol[x]=dc; anyCol[x]=ac;
                }
                for (int x=0;x<W;x++)
                {
                    if (darkCol[x] < (int)(H*0.35)) continue;   // not a tall dark column
                    int lx=x-4, rx=x+4;
                    bool leftEmpty  = (lx<0)  || anyCol[lx] < (int)(H*0.10);
                    bool rightEmpty = (rx>=W) || anyCol[rx] < (int)(H*0.10);
                    if (leftEmpty && rightEmpty) { for(int y=0;y<H;y++) b[y*stride+x*4+3]=0; } // isolated divider line
                }
            }

            // ---- (3) connected components; keep only large ones (kills cell lines & speckle) ----
            var label = new int[W*H];
            for (int k=0;k<label.Length;k++) label[k]=-1;
            var sizes = new List<int>();
            var q = new Queue<int>();
            for (int p=0;p<W*H;p++)
            {
                int px=p%W, py=p/W;
                if (b[py*stride+px*4+3]==0 || label[p]!=-1) continue;
                int lab=sizes.Count; int area=0;
                label[p]=lab; q.Enqueue(p);
                while(q.Count>0)
                {
                    int c=q.Dequeue(); area++;
                    int cx=c%W, cy=c/W;
                    EnqNb(b,stride,label,q,cx+1,cy,W,H,lab);
                    EnqNb(b,stride,label,q,cx-1,cy,W,H,lab);
                    EnqNb(b,stride,label,q,cx,cy+1,W,H,lab);
                    EnqNb(b,stride,label,q,cx,cy-1,W,H,lab);
                }
                sizes.Add(area);
            }
            int maxArea=0; for(int s=0;s<sizes.Count;s++) if(sizes[s]>maxArea) maxArea=sizes[s];
            int keepThresh=(int)(maxArea*0.05);
            for (int p=0;p<W*H;p++)
            {
                int lab=label[p];
                if (lab>=0 && sizes[lab]<keepThresh)
                {
                    int px=p%W, py=p/W;
                    b[py*stride+px*4+3]=0;
                }
            }

            // ---- bbox of remaining content ----
            for (int y=0;y<H;y++){ int row=y*stride; for(int x=0;x<W;x++){ if(b[row+x*4+3]>20){ if(x<minX[i])minX[i]=x; if(x>maxX[i])maxX[i]=x; if(y<minY[i])minY[i]=y; if(y>maxY[i])maxY[i]=y; } } }

            Marshal.Copy(b, 0, data.Scan0, len);
            bmp.UnlockBits(data);
            frames[i]=b;
        }

        int contentW=0, contentH=0;
        for(int i=0;i<4;i++){ if(maxX[i]>=minX[i]){ contentW=Math.Max(contentW, maxX[i]-minX[i]+1); contentH=Math.Max(contentH, maxY[i]-minY[i]+1); } }
        int margin=12;
        int outW=contentW+margin*2;
        int outH=contentH+margin*2;

        if(!System.IO.Directory.Exists(outDir)) System.IO.Directory.CreateDirectory(outDir);

        for (int i=0;i<4;i++)
        {
            var outBmp=new Bitmap(outW,outH,PixelFormat.Format32bppArgb);
            var od=outBmp.LockBits(new Rectangle(0,0,outW,outH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int olen=od.Stride*outH;
            var ob=new byte[olen];
            int fw=maxX[i]-minX[i]+1;
            int fh=maxY[i]-minY[i]+1;
            int dstX0=(outW-fw)/2;
            int dstY0=(outH-margin)-fh;
            var sb=frames[i];
            for (int yy=0; yy<fh; yy++)
            {
                int sy=minY[i]+yy; int dy=dstY0+yy;
                if (dy<0||dy>=outH) continue;
                for (int xx=0; xx<fw; xx++)
                {
                    int sx=minX[i]+xx; int dx=dstX0+xx;
                    if (dx<0||dx>=outW) continue;
                    int so=sy*stride+sx*4;
                    int doo=dy*od.Stride+dx*4;
                    ob[doo]=sb[so]; ob[doo+1]=sb[so+1]; ob[doo+2]=sb[so+2]; ob[doo+3]=sb[so+3];
                }
            }
            Marshal.Copy(ob,0,od.Scan0,olen);
            outBmp.UnlockBits(od);
            outBmp.Save(System.IO.Path.Combine(outDir, prefix+(i+1)+".png"), ImageFormat.Png);
            outBmp.Dispose();
        }
        src.Dispose();
        Console.WriteLine("done "+prefix+" -> "+outW+"x"+outH);
    }

    static void TrySeed(byte[] b,int stride,bool[] vis,Stack<int> st,int x,int y,int W)
    {
        int idx=y*W+x;
        if (vis[idx]) return;
        vis[idx]=true;
        if (IsBg(b, y*stride+x*4)) st.Push(idx);
    }
    static void TryPush(byte[] b,int stride,bool[] vis,Stack<int> st,int x,int y,int W,int H)
    {
        if (x<0||x>=W||y<0||y>=H) return;
        int idx=y*W+x;
        if (vis[idx]) return;
        vis[idx]=true;
        if (IsBg(b, y*stride+x*4)) st.Push(idx);
    }
    static void EnqNb(byte[] b,int stride,int[] label,Queue<int> q,int x,int y,int W,int H,int lab)
    {
        if (x<0||x>=W||y<0||y>=H) return;
        int p=y*W+x;
        if (label[p]!=-1) return;
        if (b[y*stride+x*4+3]==0) return;
        label[p]=lab; q.Enqueue(p);
    }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
for ($i=0; $i -lt $args.Count; $i+=3) { [Clean2]::Run($args[$i], $args[$i+1], $args[$i+2]) }
