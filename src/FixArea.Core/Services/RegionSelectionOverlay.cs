using System.Drawing;
using System.Drawing.Drawing2D;
using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public sealed class RegionSelectionOverlay : IDisposable
{
    NativeWindow? _window;
    ScreenRect _virtual;
    bool _dragging;
    POINT _start;
    POINT _current;
    bool _visible;

    public event Action<ScreenRect>? Completed;
    public event Action? Cancelled;

    public bool IsVisible => _visible;
    public IntPtr Handle => _window?.Handle ?? IntPtr.Zero;

    public void Show()
    {
        Hide();
        _virtual = MonitorService.GetVirtualScreen();
        _dragging = false;
        _visible = true;

        var ex = NativeConstants.WS_EX_TOPMOST | NativeConstants.WS_EX_TOOLWINDOW | NativeConstants.WS_EX_LAYERED;
        var style = NativeConstants.WS_POPUP | NativeConstants.WS_VISIBLE;
        _window = new NativeWindow(
            "FixAreaSelect",
            ex,
            style,
            _virtual,
            "FixArea Selection",
            NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_CROSS));

        _window.Message += OnMessage;
        Paint();
    }

    public void Hide()
    {
        _visible = false;
        _dragging = false;
        if (_window is not null)
        {
            _window.Message -= OnMessage;
            _window.Dispose();
            _window = null;
        }
    }

    IntPtr? OnMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch ((int)msg)
        {
            case NativeConstants.WM_LBUTTONDOWN:
                _dragging = true;
                _start = Unpack(lParam);
                _current = _start;
                Paint();
                return IntPtr.Zero;
            case NativeConstants.WM_MOUSEMOVE:
                if (_dragging)
                {
                    _current = Unpack(lParam);
                    Paint();
                }
                return IntPtr.Zero;
            case NativeConstants.WM_LBUTTONUP:
                if (_dragging)
                {
                    _dragging = false;
                    _current = Unpack(lParam);
                    var rect = ToScreenRect();
                    Hide();
                    if (rect.Width >= 32 && rect.Height >= 32)
                        Completed?.Invoke(rect);
                    else
                        Cancelled?.Invoke();
                }
                return IntPtr.Zero;
            case NativeConstants.WM_RBUTTONUP:
            case NativeConstants.WM_KEYDOWN when wParam.ToInt32() == NativeConstants.VK_ESCAPE:
                Hide();
                Cancelled?.Invoke();
                return IntPtr.Zero;
        }

        return null;
    }

    void Paint()
    {
        if (_window is null)
            return;

        using var bmp = LayeredSurface.Create(_virtual.Width, _virtual.Height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Color.FromArgb(140, 8, 12, 20));

        using var hintFont = new Font("Segoe UI", 18, FontStyle.Bold);
        using var hintBrush = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
        g.DrawString("Drag to lock a region  •  Esc to cancel", hintFont, hintBrush, 40, 40);

        if (_dragging)
        {
            var r = ToClientRect();
            using var fill = new SolidBrush(Color.FromArgb(70, 40, 140, 255));
            using var pen = new Pen(Color.FromArgb(240, 80, 180, 255), 2);
            g.FillRectangle(fill, r);
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

            using var labelFont = new Font("Segoe UI", 12, FontStyle.Regular);
            var screen = ToScreenRect();
            g.DrawString($"{screen.Width} × {screen.Height}", labelFont, hintBrush, r.X + 8, r.Y + 8);
        }

        LayeredSurface.Present(_window.Handle, _virtual.X, _virtual.Y, bmp);
    }

    Rectangle ToClientRect()
    {
        var x1 = Math.Min(_start.X, _current.X);
        var y1 = Math.Min(_start.Y, _current.Y);
        var x2 = Math.Max(_start.X, _current.X);
        var y2 = Math.Max(_start.Y, _current.Y);
        return Rectangle.FromLTRB(x1, y1, x2, y2);
    }

    ScreenRect ToScreenRect()
    {
        var c = ToClientRect();
        return new ScreenRect(c.X + _virtual.X, c.Y + _virtual.Y, c.Width, c.Height);
    }

    static POINT Unpack(IntPtr lParam)
    {
        var v = lParam.ToInt32();
        return new POINT((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF));
    }

    public void Dispose() => Hide();
}
