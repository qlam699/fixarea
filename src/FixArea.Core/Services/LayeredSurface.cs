using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

internal static class LayeredSurface
{
    public static void Present(IntPtr hwnd, int x, int y, Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        var memDc = NativeMethods.CreateCompatibleDC(screenDc);

        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0
            }
        };

        var dib = NativeMethods.CreateDIBSection(memDc, ref bmi, 0, out var bits, IntPtr.Zero, 0);
        if (dib == IntPtr.Zero || bits == IntPtr.Zero)
        {
            NativeMethods.DeleteDC(memDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            return;
        }

        var old = NativeMethods.SelectObject(memDc, dib);
        CopyPremultiplied(bitmap, bits, width, height);

        var dst = new POINT(x, y);
        var size = new SIZE(width, height);
        var src = new POINT(0, 0);
        var blend = new BLENDFUNCTION
        {
            BlendOp = NativeConstants.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = NativeConstants.AC_SRC_ALPHA
        };

        NativeMethods.UpdateLayeredWindow(
            hwnd,
            screenDc,
            ref dst,
            ref size,
            memDc,
            ref src,
            0,
            ref blend,
            NativeConstants.ULW_ALPHA);

        NativeMethods.SelectObject(memDc, old);
        NativeMethods.DeleteObject(dib);
        NativeMethods.DeleteDC(memDc);
        NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
    }

    static unsafe void CopyPremultiplied(Bitmap bitmap, IntPtr dest, int width, int height)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var src = (byte*)data.Scan0;
            var dst = (byte*)dest;
            for (var y = 0; y < height; y++)
            {
                var s = src + y * data.Stride;
                var d = dst + y * width * 4;
                for (var x = 0; x < width; x++)
                {
                    var b = s[x * 4 + 0];
                    var g = s[x * 4 + 1];
                    var r = s[x * 4 + 2];
                    var a = s[x * 4 + 3];
                    d[x * 4 + 0] = (byte)(b * a / 255);
                    d[x * 4 + 1] = (byte)(g * a / 255);
                    d[x * 4 + 2] = (byte)(r * a / 255);
                    d[x * 4 + 3] = a;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public static Bitmap Create(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        return new Bitmap(width, height, PixelFormat.Format32bppArgb);
    }
}
