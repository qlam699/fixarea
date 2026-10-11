# FixArea

Ứng dụng Windows giữ **một vùng màn hình** luôn trống: cửa sổ khác đụng vào vùng này sẽ bị resize/đẩy ra; bạn có thể kéo hoặc hotkey để snap một app vào vùng đó.

## Tính năng

- Tạo **một** vùng khóa bằng kéo chuột (overlay chọn vùng)
- Cửa sổ khác chồng lên vùng khóa → tự **resize** để tránh
- Kéo thả cửa sổ vào vùng (kiểu FancyZones) → **ghim** và khớp kích thước vùng
- Snap cửa sổ đang focus bằng hotkey
- Overlay vùng: chỉ viền đứt nét, opacity 50%, không nền / không tên
- Chạy nền (tray), tùy chọn **Start with Windows**
- Hỗ trợ nhiều màn hình (tọa độ theo monitor)

## Hotkey

| Phím | Hành động |
|------|-----------|
| `Ctrl + Alt + R` | Vẽ / tạo vùng khóa (vùng mới thay vùng cũ) |
| `Ctrl + Alt + Enter` | Snap cửa sổ đang focus vào vùng khóa |
| `Ctrl + Alt + X` | Xóa vùng khóa |

## Cách dùng

1. Chạy `FixArea.App.exe`
2. `Ctrl + Alt + R` → kéo chọn hình chữ nhật trên màn hình
3. Kéo app khác vào vùng và thả (khi chuột / mép cửa sổ chồng vùng), hoặc focus app rồi `Ctrl + Alt + Enter`
4. App khác maximize / đụng vùng sẽ bị resize ra ngoài vùng khóa
5. Đóng cửa sổ chính → app ẩn xuống tray (Exit trong tray mới thoát)

## Yêu cầu

- Windows 10 (17763+) / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) để build

## Build & chạy

```bash
dotnet build FixArea.sln -c Debug
```

Exe:

```text
src/FixArea.App/bin/x64/Debug/net8.0-windows10.0.19041.0/FixArea.App.exe
```

## Bản portable (ZIP)

**Đừng** upload file `.exe` nhỏ (~268 KB) trong `bin/` lên GitHub Releases — bản đó cần nhiều DLL cùng thư mục, chạy một mình sẽ lỗi.

`PublishSingleFile` của WinUI 3 hiện bị crash khi mở app. Hãy ship **thư mục self-contained dạng ZIP**:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1
```

Upload:

```text
publish/FixArea-portable-win-x64.zip
```

Người dùng giải nén rồi chạy `FixArea.App.exe` (không cần cài .NET). Không bật `PublishTrimmed` — sẽ làm WinUI lỗi.

Cấu hình lưu tại:

```text
%AppData%\FixArea\config.json
```

## Cấu trúc dự án

```text
FixArea.sln
src/
  FixArea.Core/   # P/Invoke, collision, hook cửa sổ, overlay, pin, config
  FixArea.App/    # WinUI 3 (unpackaged), tray, MVVM UI
```

Công nghệ chính:

- C# / .NET 8, WinUI 3, Windows App SDK (unpackaged)
- `SetWinEventHook` theo dõi cửa sổ
- `SetWindowPos` / `GetWindowRect` / multi-monitor APIs
- `SHAppBarMessage` cho vùng sát cạnh (khi đủ điều kiện edge)

## Giới hạn

- Game / video **exclusive fullscreen** có thể bỏ qua xử lý thường
- Cửa sổ chạy **Administrator** có thể không bị điều khiển nếu FixArea không elevated
- Electron / Chromium đôi khi tạo nhiều HWND phụ
- Vùng giữa màn hình không vào work area chuẩn của Windows; FixArea tự theo dõi và sửa từng cửa sổ

## License

Dự án cá nhân / dùng nội bộ — chưa gắn license công khai.

Bản tiếng Anh: [README.md](README.md)
