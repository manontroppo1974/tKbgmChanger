using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections;

using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.ComponentModel;
using System.Threading;

namespace tKbgmChanger
{
    class Program
    {
        const uint SPI_SETDESKWALLPAPER = 20;
        const uint SPIF_UPDATEINIFILE = 1;
        const uint SPIF_SENDWININICHANGE = 2;

        const string mFileCfg = "tKbgmChanger.cfg";
        const string mFileKbgm = "tKbgmChanger.jpg";
        const string mFileLog = "tKbgmChanger.log";

        string mPathBase;
        int mKido = 100;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, uint fWinIni);

        [STAThread]
        static void Main(string[] args)
        {
            Program p = new Program();
            p.Exec();
        }

        void Exec()
        {
            int ret;

            // 設定ファイルの読み込み
            ret = ReadCfgFile();
            if (ret < 0)
            {
                return;
            }

            // 画像の検索
            string[] fileStrs = Directory.GetFiles(mPathBase, "*.jpg", SearchOption.AllDirectories);
            Console.WriteLine("file count: " + fileStrs.Length);

            // 画面全体の大きさを取得
            int scrCnt = Screen.AllScreens.Length;
            Point dispMin = new Point(0, 0);
            Point dispMax = new Point(0, 0);
            for (int scrNo = 0; scrNo < scrCnt; scrNo++)
            {
                Rectangle rect = Screen.AllScreens[scrNo].Bounds;
                Console.WriteLine("display [" + scrNo + "]: X=" + rect.X + ", " + rect.Y + ", " + rect.Width + " x " + rect.Height);

                dispMin.X = (rect.X < dispMin.X) ? rect.X : dispMin.X;
                dispMin.Y = (rect.Y < dispMin.Y) ? rect.Y : dispMin.Y;
                dispMax.X = (rect.X + rect.Width > dispMax.X) ? (rect.X + rect.Width) : dispMax.X;
                dispMax.Y = (rect.Y + rect.Height > dispMax.Y) ? (rect.Y + rect.Height) : dispMax.Y;            
            }

            //壁紙画像の生成
            Bitmap dispFullBmp = new Bitmap(dispMax.X - dispMin.X, dispMax.Y - dispMin.Y);
            Graphics gDispFull = Graphics.FromImage(dispFullBmp);
            gDispFull.Clear(Color.Green);
            //gDispFull.TranslateTransform((float)-dispMin.X, (float)-dispMin.Y, MatrixOrder.Prepend);
            for (int scrNo = 0; scrNo < scrCnt; scrNo++)
            {
                Rectangle rect = Screen.AllScreens[scrNo].Bounds;

                Bitmap bmp = ResizeTrimBitmap(new Bitmap(SelectImgFile(fileStrs)), rect.Size);
                gDispFull.DrawImage(bmp, rect.X - dispMin.X, rect.Y - dispMin.Y, bmp.Width, bmp.Height);
                bmp.Dispose();
            }

            //作成した壁紙をディスプレイの配置に合わせて平行移動
            //Console.WriteLine("画像位置設定中... ");
            //Bitmap dispFullBmpMove = Move(dispFullBmp, -dispMin.X, -dispMin.Y);
            Bitmap dispFullBmpMove = dispFullBmp;

            //画像をちょっと暗く
            Console.WriteLine("画像輝度設定中 (" + mKido + "%) ... ");
            Bitmap dispFullBmpDark = DarkBmp(dispFullBmpMove, mKido);

            Console.Write("壁紙変更... ");

            //壁紙画像の保存
            dispFullBmpMove.Save(mFileKbgm, System.Drawing.Imaging.ImageFormat.Jpeg);

            //壁紙のパス
            StringBuilder sb = new StringBuilder(Path.Combine(System.Windows.Forms.Application.StartupPath, mFileKbgm));

            // 並べて表示の説定
            using (var regkeyDesktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
            {
                if (regkeyDesktop == null)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                // TileWallpaperとWallpaperStyleを設定
                regkeyDesktop.SetValue("TileWallpaper", "1");
                regkeyDesktop.SetValue("WallpaperStyle", "0");

                // 「並べて表示」、「拡大して表示」などの原点も変えたい場合は、この値を0以外に変更する
                regkeyDesktop.SetValue("WallpaperOriginX", "0");
                regkeyDesktop.SetValue("WallpaperOriginY", "0");
                //regkeyDesktop.SetValue("WallpaperOriginX", dispMin.X.ToString());
                //regkeyDesktop.SetValue("WallpaperOriginY", dispMin.Y.ToString());

                // Wallpaperの値をセットすることでも壁紙を設定できるが、
                // SystemParametersInfoを呼び出さないと、壁紙を設定しても即座には反映されない
                regkeyDesktop.SetValue("Wallpaper", mFileKbgm);
            }

            //壁紙を変える
            var flags = (Environment.OSVersion.Platform == PlatformID.Win32NT)
                        ? SPIF_SENDWININICHANGE
                        : SPIF_SENDWININICHANGE | SPIF_UPDATEINIFILE;

            //bool retSP = SystemParametersInfo(SPI_SETDESKWALLPAPER, (uint)sb.Length, sb, flags);
            bool retSP = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, sb, flags);
            if (retSP)
            {
                Console.WriteLine("OK");
            }
            else
            {
                Console.WriteLine("NG");
            }

            //リソース開放
            Console.WriteLine("リソース開放");
            dispFullBmp.Dispose();
            dispFullBmpMove.Dispose();
            dispFullBmpDark.Dispose();
        }

        int ReadCfgFile()
        {
            //ファイル一覧
            if (!File.Exists(mFileCfg))
            {
                Console.WriteLine("err: 設定ファイルが存在しません");
                return -1;
            }
            //string basePath = @"C:\共通DATA\壁紙";
            //string basePath = "";
            string line = "";
            //ArrayList al = new ArrayList(); 
            using (StreamReader sr = new StreamReader(mFileCfg, Encoding.GetEncoding("Shift_JIS")))
            {
                int lineNo = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    if (lineNo == 0)
                    {
                        mPathBase = line;
                    }
                    else if (lineNo == 1)
                    {
                        mKido = Int16.Parse(line);
                    }
                    lineNo++;
                    //al.Add(line);
                }
            }
            Console.WriteLine("dir: " + mPathBase);
            if (!Directory.Exists(mPathBase))
            {
                Console.WriteLine("dir err: " + mPathBase + " が存在しません");
                return -1;
            }

            return 0;
        }

        string SelectImgFile(string[] fileStrs)
        {
            /*
            string[] files = Directory.GetFiles(mPathBase, "*.jpg", SearchOption.AllDirectories);
            Console.WriteLine("file count: " + files.Length);
            */

            //ファイルの選択
            Random rnd = new Random();
            string selectedFile = fileStrs[rnd.Next(fileStrs.Length)];
            Console.WriteLine("select: " + selectedFile);

            return selectedFile;
        }

        Bitmap ResizeTrimBitmap(Bitmap bmp, Size size)
        {
            Console.WriteLine("image:   " + bmp.Width + " x " + bmp.Height);
            Bitmap resizeBmp;
            Size resizeSize;
            if (bmp.Height * size.Width / bmp.Width >= size.Height)
            {
                resizeSize = new Size(size.Width, bmp.Height * size.Width / bmp.Width);
                resizeBmp = new Bitmap(bmp, resizeSize);
            }
            else
            {
                resizeSize = new Size(bmp.Width * size.Height / bmp.Height, size.Height);
                resizeBmp = new Bitmap(bmp, resizeSize);
            }
            Console.WriteLine("resize:  " + resizeBmp.Width + " x " + resizeBmp.Height);

            //画像のトリミング
            RectangleF trimRect = new RectangleF((resizeBmp.Width - size.Width) / 2, (resizeBmp.Height - size.Height) / 2, size.Width, size.Height);
            Bitmap trimBmp = resizeBmp.Clone(trimRect, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            //trimBmp.Save(@"c:\toshi\tmp\tKbgmChange.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
            //trimBmp.Save(mFileKbgm, System.Drawing.Imaging.ImageFormat.Bmp);
            Console.WriteLine("trim:    " + trimBmp.Width + " x " + trimBmp.Height);

            resizeBmp.Dispose();

            return trimBmp;
        }

        Bitmap Move(Bitmap bmp, int x, int y)
        {
            Bitmap mvBmp = new Bitmap(bmp.Width, bmp.Height);
            Graphics gMvBmp = Graphics.FromImage(mvBmp);
            gMvBmp.Clear(Color.Green);

            // 左上 → 右下
            gMvBmp.DrawImage(bmp,
                new Rectangle(bmp.Width - x, bmp.Height - y, x, y),
                0, 0, x, y, GraphicsUnit.Pixel);

            // 右上 → 左下
            gMvBmp.DrawImage(bmp,
                new Rectangle(0, bmp.Height - y, bmp.Width - x, y),
                x, 0, bmp.Width - x, y, GraphicsUnit.Pixel);

            // 左下 → 右上
            gMvBmp.DrawImage(bmp,
                new Rectangle(bmp.Width - x, 0, x, bmp.Height - y),
                0, y, x, bmp.Height - y, GraphicsUnit.Pixel);

            // 右下 → 左上
            gMvBmp.DrawImage(bmp,
                new Rectangle(0, 0, bmp.Width-x, bmp.Height-y),
                x, y, bmp.Width - x, bmp.Height - y, GraphicsUnit.Pixel);

            return mvBmp;
        }

        Bitmap DarkBmp(Bitmap bmp, int val)
        {
            if (val < 0 || 100 < val)
            {
                val = 100;
            }

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            unsafe
            {
                byte* adr = (byte*)bmpData.Scan0;
                int pos;
                for (int y = 0; y < bmp.PhysicalDimension.Height; y++)
                {
                    for (int x = 0; x < bmp.PhysicalDimension.Width; x++)
                    {
                        pos = x * 3 + bmpData.Stride * y;
                        byte cb = adr[pos + 0];
                        byte cg = adr[pos + 1];
                        byte cr = adr[pos + 2];

                        adr[pos + 0] = (byte)(cb * val / 100.0);
                        adr[pos + 1] = (byte)(cg * val / 100.0);
                        adr[pos + 2] = (byte)(cr * val / 100.0);
                    }
                }

                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }

    }
}
