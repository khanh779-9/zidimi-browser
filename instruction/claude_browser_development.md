# Tài liệu Nghiên Cứu & Đặc Tả Thiết Kế Trình Duyệt Web
### (Tham khảo Chrome / Edge / Firefox / Brave / Arc / Safari — Áp dụng cho dự án CEFSharp + C# WPF)

> **Mục đích tài liệu:** Tổng hợp kiến trúc giao diện (UI/UX), thành phần chức năng, hành vi, kích thước, và các lưu ý kỹ thuật/pháp lý của các trình duyệt phổ biến hiện nay, làm nền tảng để phát triển một trình duyệt nhúng chuyên nghiệp bằng **CefSharp (Chromium Embedded Framework) trên nền C# WPF**.
>
> **Ngày soạn:** 02/08/2026
> **Phạm vi:** Desktop Windows (WPF). Không đi sâu vào phiên bản mobile.

---

## Mục lục

1. [Tổng quan các trình duyệt tham khảo](#1-tổng-quan-các-trình-duyệt-tham-khảo)
2. [Kiến trúc khung giao diện tổng thể (Window Chrome)](#2-kiến-trúc-khung-giao-diện-tổng-thể-window-chrome)
3. [Thanh Tab (Tab Strip)](#3-thanh-tab-tab-strip)
4. [Thanh công cụ (Toolbar) & các nút điều hướng](#4-thanh-công-cụ-toolbar--các-nút-điều-hướng)
5. [Thanh địa chỉ / Omnibox](#5-thanh-địa-chỉ--omnibox)
6. [Menu trình duyệt & các Popup/Panel](#6-menu-trình-duyệt--các-popuppanel)
7. [Trang Cài đặt (Settings)](#7-trang-cài-đặt-settings)
8. [Quản lý Profile / Đa người dùng](#8-quản-lý-profile--đa-người-dùng)
9. [Lưu trữ dữ liệu (Storage Architecture)](#9-lưu-trữ-dữ-liệu-storage-architecture)
10. [Bảng màu, Typography & Design Tokens](#10-bảng-màu-typography--design-tokens)
11. [Ánh xạ sang kiến trúc CefSharp + WPF](#11-ánh-xạ-sang-kiến-trúc-cefsharp--wpf)
12. [Bảng đánh giá độ khó triển khai theo tính năng](#12-bảng-đánh-giá-độ-khó-triển-khai-theo-tính-năng)
13. [Rủi ro Sở hữu trí tuệ & Bản quyền](#13-rủi-ro-sở-hữu-trí-tuệ--bản-quyền)
14. [Lộ trình phát triển đề xuất (Roadmap)](#14-lộ-trình-phát-triển-đề-xuất-roadmap)
15. [Tài liệu tham khảo](#15-tài-liệu-tham-khảo)

---

## 1. Tổng quan các trình duyệt tham khảo

| Trình duyệt | Engine | Đặc trưng thiết kế | Ghi chú tham khảo |
|---|---|---|---|
| **Google Chrome** | Blink (Chromium) | Tab bo góc trên đỉnh cửa sổ ("tab thả nổi" từ bản thiết kế Material You ~2023), Omnibox bo tròn, nút Profile hình tròn ở góc phải, thanh công cụ có thể tuỳ biến (từ Chrome 132/2025) | Đây là "chuẩn thị trường" — phần lớn trình duyệt Chromium khác (Edge, Brave, Opera, Cốc Cốc) đều bám theo layout này |
| **Microsoft Edge** | Blink (Chromium) | Tương tự Chrome nhưng có thêm **Sidebar dọc bên phải** (Copilot, Collections, ứng dụng ghim), nút "Split screen", Workspaces (nhóm tab theo màu) | Rất đáng tham khảo vì gần nhất với hướng "trình duyệt doanh nghiệp/năng suất" |
| **Mozilla Firefox** | Gecko | Tab hình thang bo góc đặc trưng, thanh địa chỉ có icon "Reader View", hỗ trợ Container Tabs (tab theo màu/ngữ cảnh cô lập cookie) | Firefox có kiến trúc **Proton UI** — hệ thống thiết kế mở, tài liệu công khai chi tiết nhất |
| **Brave** | Blink (Chromium) | Thêm icon "Shields" (chặn quảng cáo) ngay trong address bar, panel Rewards, Wallet tích hợp | Tham khảo tốt cho việc thêm icon trạng thái tuỳ chỉnh vào address bar |
| **Arc (The Browser Company)** | Blink (Chromium) | Phá vỡ layout truyền thống: tab dọc bên trái thay vì ngang trên đỉnh, "Spaces" để nhóm workflow, không có thanh địa chỉ cố định (ẩn khi không dùng) | Tham khảo cho hướng đi khác biệt/sáng tạo, độ khó triển khai cao hơn nhiều |
| **Safari** (macOS, tham khảo chéo) | WebKit | Thanh công cụ tối giản, tab có thể thu gọn thành preview ảnh thumbnail | Không áp dụng trực tiếp cho Windows nhưng hữu ích cho ý tưởng tối giản hoá |

**Nhận định chung:** Vì bạn dùng CefSharp (dựa trên Chromium/CEF), nên **thiết kế theo phong cách Chrome/Edge** là lựa chọn hợp lý nhất — vừa quen thuộc với người dùng, vừa tương thích tự nhiên với các API mà CEF cung cấp (favicon, loading state, popup, download, v.v.).

---

## 2. Kiến trúc khung giao diện tổng thể (Window Chrome)

Cấu trúc phân lớp từ trên xuống dưới của một cửa sổ trình duyệt hiện đại (theo tài liệu UX chính thức của Chromium):

```
┌─────────────────────────────────────────────────────────┐
│ [Title/Tab Strip]                    [- □ ×] (window ctl)│  ← Tầng 1: Tab strip
├─────────────────────────────────────────────────────────┤
│ [←][→][⟳] [🏠] [   Address Bar / Omnibox   ] [⭐][⋮][👤] │  ← Tầng 2: Toolbar
├─────────────────────────────────────────────────────────┤
│ [Bookmarks Bar - tuỳ chọn, có thể ẩn/hiện]                │  ← Tầng 3 (tuỳ chọn)
├─────────────────────────────────────────────────────────┤
│                                                           │
│                 [Vùng nội dung Web/ WebView]              │  ← Tầng 4: Content area
│                                                           │
├─────────────────────────────────────────────────────────┤
│ [Status bar - hiện khi hover link / đang tải]             │  ← Tầng 5 (ẩn theo mặc định)
└─────────────────────────────────────────────────────────┘
```

Theo tài liệu UX của dự án Chromium, cấu trúc UI được mô tả gồm 4 khối chính: **Window Frame | Tabs | Throbber | Toolbar | Omnibox**, trong đó <cite index="6-1">tab được xem như tương đương với title bar của một ứng dụng desktop truyền thống, còn khung cửa sổ chứa các tab là cơ chế để quản lý nhóm các "ứng dụng" đó</cite>.

### 2.1. Kích thước tham chiếu (chuẩn desktop, DPI 96 = 100%)

| Thành phần | Chiều cao (px) | Ghi chú |
|---|---|---|
| Tab strip (bao gồm cả khoảng trống kéo cửa sổ) | 36–40px | Tab riêng lẻ cao khoảng 32–34px |
| Toolbar chính (nav + address bar) | 44–48px | |
| Bookmarks bar (nếu bật) | 28–32px | |
| Status bar (hiện khi cần) | 20–24px | Overlay ở góc dưới trái, không chiếm layout cố định |
| Nút điều khiển cửa sổ (Min/Max/Close) | 32×28px mỗi nút (chuẩn Fluent/Win11) | |

> ⚠️ *Các số liệu trên là ước lượng tổng hợp phổ biến từ quan sát thực tế và các UI kit cộng đồng (không phải số liệu chính thức 1:1 từ Google/Microsoft), dùng để tham khảo tỉ lệ — bạn nên tự đo & tinh chỉnh theo DPI/theme thực tế khi build.*

---

## 3. Thanh Tab (Tab Strip)

### 3.1. Thành phần trong một Tab

| Thành phần | Mô tả | Độ phức tạp |
|---|---|---|
| Favicon | Icon 16×16px của trang, load bất đồng bộ, có fallback icon mặc định khi trang không có favicon | Dễ |
| Loading spinner (Throbber) | Thay thế favicon khi trang đang tải, animation xoay liên tục | Dễ–Trung bình |
| Tiêu đề trang (Title) | Text co giãn, bị cắt bằng dấu "…" khi tab thu nhỏ | Dễ |
| Nút đóng tab (×) | Chỉ hiện khi hover vào tab hoặc khi tab đang active | Dễ |
| Chỉ báo âm thanh (🔊/🔇) | Hiện icon loa khi tab đang phát audio, click để mute nhanh | Trung bình |
| Chỉ báo tab được ghim (Pinned) | Tab thu nhỏ về kích thước icon-only, luôn nằm đầu danh sách | Trung bình |
| Kéo-thả sắp xếp lại | Cho phép kéo tab đổi vị trí, kéo ra khỏi cửa sổ để tách cửa sổ mới | Khó |
| Tab Groups (nhóm tab có màu) | Chrome/Edge cho phép bọc nhiều tab vào 1 nhóm có tên + màu, thu gọn được | Khó |
| Nút "+" thêm tab mới | Luôn nằm cuối tab strip | Dễ |
| Nút overflow (▾) khi quá nhiều tab | Hiện dropdown danh sách tab khi tab strip tràn | Trung bình |

### 3.2. Hành vi co giãn

- Khi số tab tăng, mỗi tab **co lại đều nhau** đến một ngưỡng tối thiểu (~52–72px), sau đó xuất hiện nút cuộn ngang hoặc menu tràn.
- Tab đang active luôn có **z-index cao hơn**, nền sáng/khác biệt rõ so với tab không active và so với vùng toolbar phía dưới, tạo ảo giác "tab liền mạch" với nội dung trang.

---

## 4. Thanh công cụ (Toolbar) & các nút điều hướng

Theo tài liệu hướng dẫn tuỳ biến toolbar của Chrome, cấu trúc toolbar được chia thành các cụm rõ ràng: <cite index="3-1">Address Bar (Omnibox) đóng vai trò vừa là ô nhập URL vừa là ô tìm kiếm tận dụng thuật toán gợi ý của Google, và các nút điều hướng Back/Forward/Reload dùng để duyệt lịch sử trang</cite>.

### 4.1. Cụm nút điều hướng (bên trái)

| Nút | Icon gợi ý | Hành vi | Trạng thái disable |
|---|---|---|---|
| Back (←) | Mũi tên trái | Điều hướng lùi trong lịch sử tab hiện tại; giữ chuột hiện dropdown lịch sử | Disable khi không có trang trước |
| Forward (→) | Mũi tên phải | Điều hướng tiến; tương tự có dropdown lịch sử | Disable khi không có trang sau |
| Reload/Stop (⟳ / ×) | Icon đổi động | Reload khi trang đã load xong; đổi thành nút "Stop" (dừng tải) khi đang loading | Không disable, luôn khả dụng |
| Home (🏠) | Icon nhà | Điều hướng về trang chủ đã cấu hình (tuỳ chọn ẩn/hiện trong Settings) | Có thể ẩn hoàn toàn |

### 4.2. Cụm bên phải (Actions)

| Nút | Chức năng | Độ phức tạp |
|---|---|---|
| Bookmark star (⭐) | Toggle lưu/bỏ trang hiện tại vào Bookmarks, click giữ mở popup sửa tên/thư mục | Trung bình |
| Extensions puzzle icon | Danh sách extension đã cài, ẩn theo mặc định để tiết kiệm không gian | Khó (cần hỗ trợ extension) |
| Downloads (⬇) | Hiện badge số lượng khi có download mới, click mở panel lịch sử tải | Trung bình |
| Profile avatar (👤) | Ảnh đại diện tròn, click mở panel chuyển đổi Profile | Trung bình–Khó |
| Menu 3 chấm (⋮) | Menu chính chứa: New tab/window, History, Downloads, Bookmarks, Zoom, Print, Find, More tools, Settings, Help, Exit | Dễ–Trung bình |

Theo tài liệu chính thức về tính năng tuỳ biến toolbar mà Google giới thiệu năm 2025, người dùng có thể tự chọn hiển thị các icon như <cite index="2-1">Trình quản lý mật khẩu Google, Phương thức thanh toán, Địa chỉ, Bookmarks, Reading List, Lịch sử, Xoá dữ liệu duyệt web, cũng như các công cụ như In, Tìm kiếm bằng Google Lens, Dịch, Tạo mã QR, Reading Mode, Sao chép liên kết, Gửi đến thiết bị khác, Task Manager và Developer Tools — và có thể kéo-thả để sắp xếp lại vị trí các icon này</cite>. Đây là một tính năng ở mức độ **khó**, phù hợp triển khai ở giai đoạn sau khi đã có toolbar cơ bản ổn định.

---

## 5. Thanh địa chỉ / Omnibox

Đây là thành phần **quan trọng nhất và phức tạp nhất** cần đầu tư kỹ.

### 5.1. Các trạng thái hiển thị

| Trạng thái | Hiển thị |
|---|---|
| Trang HTTPS an toàn | Icon ổ khoá (🔒) bên trái, đôi khi kèm tên miền tổ chức (EV cert - hiện ít dùng) |
| Trang HTTP không an toàn | Icon cảnh báo "Not secure" |
| Đang gõ | Dropdown gợi ý hiện realtime: lịch sử khớp, trang hay truy cập, gợi ý tìm kiếm từ search engine, bookmark khớp |
| Trang nội bộ (chrome://, about:) | Có thể ẩn icon khoá, hiện icon riêng |
| Đang tải | Progress bar mảnh (2–3px) chạy dọc theo cạnh dưới address bar hoặc dưới tab |

### 5.2. Cấu trúc icon trong Omnibox (từ trái sang phải)

1. **Security/Site info icon** — click mở popup thông tin bảo mật, cookies, permissions (camera/mic/location...) của trang.
2. **Ô nhập URL/Text** — co giãn chiếm phần lớn không gian.
3. **Icon động theo ngữ cảnh** — ví dụ icon "cài đặt PWA", icon "dịch trang", icon "Reader mode", extension icon đã ghim.
4. **Nút Star (Bookmark)** — có thể đặt trong hoặc ngoài omnibox tuỳ trình duyệt.

### 5.3. Logic xử lý input (quan trọng khi code)

```
Người dùng nhập text vào address bar
  → Kiểm tra: có phải URL hợp lệ? (có scheme, có dấu chấm hợp lệ dạng domain, IP...)
      → Nếu ĐÚNG: tự thêm "https://" nếu thiếu, điều hướng trực tiếp
      → Nếu SAI (không giống URL): coi là từ khoá tìm kiếm
          → Ghép với URL search engine mặc định (vd: https://www.google.com/search?q=...)
  → Đồng thời hiển thị dropdown gợi ý (autocomplete) dựa trên lịch sử/bookmark local
```

Repo mã nguồn mở tham khảo (WPF + CefSharp) mô tả đúng logic này: <cite index="17-1">thanh địa chỉ thông minh tự động thêm giao thức https:// khi cần và tìm kiếm trên Google với các truy vấn không phải URL, đồng thời hiển thị URL hiện tại kèm icon ổ khoá</cite>. Đây là baseline tối thiểu bạn nên có.

---

## 6. Menu trình duyệt & các Popup/Panel

| Panel | Mở từ đâu | Nội dung chính | Độ phức tạp |
|---|---|---|---|
| **Main menu (⋮)** | Toolbar phải | New Tab/Window/Incognito, History, Downloads, Bookmarks, Zoom (−/100%/+), Print, Find in page, More tools ▸, Settings, Help, Exit | Trung bình |
| **History panel** | Menu hoặc `Ctrl+H` | Danh sách trang đã truy cập theo nhóm ngày, ô tìm kiếm lịch sử, nút "Xoá dữ liệu duyệt web" | Trung bình |
| **Downloads panel** | Menu hoặc `Ctrl+J` | Danh sách file đã tải, progress bar, nút Open/Show in folder/Retry/Cancel | Trung bình |
| **Bookmarks manager** | Menu ▸ Bookmarks | Cây thư mục kéo-thả, tìm kiếm, import/export HTML | Khó |
| **Find in page** | `Ctrl+F` | Thanh nổi góc trên phải nội dung trang, đếm số kết quả, next/prev | Trung bình (CEF hỗ trợ sẵn `Find` API) |
| **Zoom control** | Menu | Nút −/+, hiện % hiện tại, reset về 100% | Dễ (CEF hỗ trợ `SetZoomLevel`) |
| **Site info popup** | Click icon khoá | Quyền truy cập Camera/Mic/Location/Notification, Cookie đang dùng | Khó |
| **Context menu (chuột phải)** | Chuột phải trên trang | Back/Forward/Reload, Save as, Print, View page source, Inspect, Copy link/image | Trung bình (CEF có `IContextMenuHandler`) |

---

## 7. Trang Cài đặt (Settings)

Trang Settings hiện đại thường được thiết kế như **một trang web nội bộ** (chrome://settings) render ngay trong chính WebView, chia làm 2 cột: menu điều hướng bên trái (sticky) + nội dung cuộn bên phải.

### 7.1. Cấu trúc nhóm mục cài đặt tiêu chuẩn

| Nhóm | Các mục con | Ghi chú kỹ thuật khi tự làm |
|---|---|---|
| **Bạn và Google/Account** | Đăng nhập, đồng bộ, Profile | Có thể bỏ qua nếu không làm hệ thống tài khoản cloud |
| **Công cụ tìm kiếm mặc định** | Danh sách search engine, thêm/sửa/xoá | Lưu vào config local (JSON/SQLite) |
| **Trình duyệt mặc định** | Đặt làm trình duyệt mặc định của Windows | Cần thao tác Registry (`HKEY_CLASSES_ROOT`, App Registration) |
| **Khi khởi động** | Mở trang mới / Tiếp tục tab cũ / Mở tập trang cụ thể | Lưu session state |
| **Giao diện (Appearance)** | Theme (Sáng/Tối/Hệ thống), hiện/ẩn Bookmarks bar, Font size, Zoom mặc định | Trung bình |
| **Quyền riêng tư & bảo mật** | Xoá dữ liệu duyệt web (Cookies/Cache/History/Passwords), Cookie settings (chặn 3rd-party), Safe Browsing, Site permissions (Camera/Mic/Location/Notification/Popup/JavaScript) | **Khó** — cần map với CEF request context |
| **Tự động điền (Autofill)** | Mật khẩu đã lưu, Phương thức thanh toán, Địa chỉ | Khó — cần mã hoá dữ liệu nhạy cảm (DPAPI trên Windows) |
| **Ngôn ngữ** | Ngôn ngữ hiển thị UI, dịch trang tự động | Trung bình |
| **Tải xuống (Downloads)** | Thư mục lưu mặc định, hỏi vị trí lưu mỗi lần | Dễ |
| **Trợ năng (Accessibility)** | Phóng to text, Focus highlight | Trung bình |
| **Hệ thống** | Dùng GPU tăng tốc, chạy nền khi đóng cửa sổ, Proxy | Khó |
| **Giới thiệu (About)** | Phiên bản, kiểm tra cập nhật | Dễ |

### 7.2. Nguyên tắc UX cho trang Settings

- Dùng **thanh tìm kiếm ở đầu trang** để lọc nhanh các mục cài đặt (rất được đánh giá cao về UX, nên ưu tiên làm sớm).
- Mỗi mục thay đổi áp dụng **ngay lập tức** (không cần nút "Lưu"), trừ các thay đổi cần khởi động lại (hiện banner "Khởi động lại để áp dụng").
- Settings nên là **một cửa sổ/tab riêng** dùng chung layout với trình duyệt (không phải dialog riêng biệt) để nhất quán trải nghiệm.

---

## 8. Quản lý Profile / Đa người dùng

### 8.1. Khái niệm

Mỗi **Profile** là một "hồ sơ" độc lập hoàn toàn về:
- Cookies, Local Storage, IndexedDB
- Lịch sử duyệt web, Bookmarks
- Mật khẩu đã lưu, Autofill data
- Extension đã cài & cấu hình riêng
- Cache riêng

### 8.2. Luồng UX quản lý Profile (kiểu Chrome/Edge)

```
Click Avatar (góc phải toolbar)
  → Popup hiện:
      - Danh sách các Profile đã tạo (avatar + tên)
      - Nút "Thêm Profile mới"
      - Nút "Quản lý Profile" (mở trang cài đặt riêng)
      - Toggle "Chế độ khách / Guest mode" (không lưu bất kỳ dữ liệu nào sau khi đóng)
  → Chọn Profile khác → Mở CỬA SỔ MỚI hoàn toàn (không chuyển đổi trong cùng cửa sổ)
```

### 8.3. Ánh xạ kỹ thuật với CefSharp

Trong CEF, mỗi Profile tương ứng với một **`RequestContext`** riêng biệt, trỏ đến một thư mục `CachePath` riêng trên đĩa. Đây là điểm mấu chốt kỹ thuật:

| Khái niệm trình duyệt | Khái niệm CEF/CefSharp tương ứng |
|---|---|
| Profile | `IRequestContext` (khởi tạo qua `RequestContext` với `RequestContextSettings.CachePath` riêng) |
| Chế độ ẩn danh (Incognito/InPrivate) | `RequestContext` với `CachePath` rỗng (in-memory, không ghi đĩa) hoặc thư mục temp bị xoá khi đóng |
| Cửa sổ theo Profile | Mỗi `Window` WPF gắn với 1 `ChromiumWebBrowser` (hoặc nhiều tab) dùng chung 1 `RequestContext` |

> ⚠️ **Lưu ý độ khó cao:** Quản lý nhiều `RequestContext` đồng thời trong CefSharp khá nặng về RAM (mỗi context là một tiến trình con Chromium riêng theo kiến trúc multi-process của CEF). Nên cân nhắc **giới hạn số Profile mở đồng thời** hoặc dùng kiến trúc lazy-load.

---

## 9. Lưu trữ dữ liệu (Storage Architecture)

### 9.1. Các loại dữ liệu cần lưu trữ local

| Loại dữ liệu | Định dạng gợi ý | Vị trí gợi ý (Windows) |
|---|---|---|
| Lịch sử duyệt web (History) | SQLite (bảng `urls`, `visits`) | `%AppData%\<TênApp>\Profile\History.db` |
| Bookmarks | SQLite hoặc JSON cây thư mục | `...\Profile\Bookmarks.json` |
| Cookies / Cache / IndexedDB / LocalStorage | Được **CEF tự quản lý** qua `CachePath` | `...\Profile\Cache\` (không tự parse tay) |
| Mật khẩu đã lưu | SQLite + mã hoá **DPAPI** (Windows Data Protection API) | `...\Profile\Logins.db` |
| Session hiện tại (tabs đang mở) | JSON snapshot, ghi định kỳ + khi thoát | `...\Profile\Session.json` |
| Downloads history | SQLite hoặc JSON | `...\Profile\Downloads.db` |
| Cấu hình/Settings | JSON hoặc `appsettings.json` | `...\Profile\Preferences.json` |
| Extension data (nếu hỗ trợ) | Thư mục riêng theo extension ID | `...\Profile\Extensions\` |

### 9.2. Nguyên tắc thiết kế

- **Không tự ý can thiệp/parse thư mục Cache do CEF quản lý** — chỉ set `CachePath` và để CEF tự xử lý vòng đời file cache; việc tự ý sửa đổi dễ gây lỗi hoặc crash.
- Dữ liệu nhạy cảm (mật khẩu, thẻ thanh toán) **bắt buộc mã hoá** trước khi ghi đĩa, tuyệt đối không lưu plaintext.
- Nên tách biệt rõ 2 tầng: **(1) Dữ liệu do CEF quản lý** (cookies/cache/localStorage — qua `RequestContext`) và **(2) Dữ liệu do ứng dụng WPF tự quản lý** (bookmarks, history hiển thị UI, settings) — vì bạn cần build UI riêng (History page, Bookmark manager) nên phải tự lưu song song một bản ghi lịch sử ở tầng ứng dụng.

---

## 10. Bảng màu, Typography & Design Tokens

### 10.1. Bảng màu tham khảo (Light / Dark) — phong cách Chromium/Fluent hiện đại

| Token | Light mode | Dark mode | Dùng cho |
|---|---|---|---|
| `--bg-toolbar` | `#F1F3F4` | `#2D2E30` | Nền toolbar & tab strip |
| `--bg-tab-active` | `#FFFFFF` | `#3C3D40` | Nền tab đang active |
| `--bg-tab-inactive` | Trong suốt/nhạt hơn nền toolbar | Trong suốt/nhạt hơn nền toolbar | Tab không active |
| `--bg-content` | `#FFFFFF` | `#202124` | Vùng nội dung web |
| `--text-primary` | `#1F1F1F` | `#E8EAED` | Chữ chính |
| `--text-secondary` | `#5F6368` | `#9AA0A6` | Chữ phụ, placeholder |
| `--accent` | `#1A73E8` (xanh dương) | `#8AB4F8` | Nút chính, link, icon active |
| `--border` | `#DADCE0` | `#3C4043` | Viền phân cách nhẹ |
| `--danger` | `#D93025` | `#F28B82` | Cảnh báo, nút đóng/xoá |

### 10.2. Typography

| Cấp | Font gợi ý (Windows) | Kích thước |
|---|---|---|
| Tiêu đề tab | Segoe UI Variable / Segoe UI | 12–13px |
| Text address bar | Segoe UI | 13–14px |
| Menu item | Segoe UI | 13px |
| Settings heading | Segoe UI Semibold | 20–22px |

### 10.3. Bo góc & Elevation

- Tab: bo góc trên 8–10px (kiểu "viên thuốc ngược" của Chrome hiện đại) hoặc bo đều 6–8px toàn bộ (kiểu Edge/Firefox cổ điển hơn).
- Address bar: bo tròn toàn phần (pill shape, radius = 50% chiều cao) là xu hướng phổ biến nhất hiện nay (Chrome 2023+).
- Dropdown/Popup: bo góc 8px, có `box-shadow` nhẹ + có thể dùng hiệu ứng **Mica/Acrylic** của Windows 11 (qua `DwmSetWindowAttribute` hoặc thư viện `WPFUI`/`ModernWpf`) để tăng cảm giác "native".

---

## 11. Ánh xạ sang kiến trúc CefSharp + WPF

### 11.1. Kiến trúc project đề xuất (MVVM)

```
BrowserApp/
├── App.xaml / App.xaml.cs          → Cef.Initialize(), xử lý shutdown Cef.Shutdown()
├── Models/
│   ├── TabItemModel.cs             → Title, Url, Favicon, IsLoading, CanGoBack/Forward
│   ├── ProfileModel.cs
│   ├── HistoryEntry.cs / Bookmark.cs / DownloadItem.cs
├── ViewModels/
│   ├── MainWindowViewModel.cs      → ObservableCollection<TabItemModel>
│   ├── AddressBarViewModel.cs      → Logic parse URL vs Search query
│   ├── SettingsViewModel.cs
├── Views/
│   ├── MainWindow.xaml             → Tab strip + Toolbar + ContentPresenter
│   ├── TabControlView.xaml         → Custom TabControl (không dùng TabControl mặc định của WPF — cần custom hoàn toàn để giống Chrome)
│   ├── AddressBarView.xaml
│   ├── SettingsWindow.xaml (hoặc render bằng HTML/CEF nội bộ)
├── Services/
│   ├── BrowserTabService.cs        → Quản lý danh sách ChromiumWebBrowser instances
│   ├── HistoryService.cs           → SQLite CRUD
│   ├── BookmarkService.cs
│   ├── DownloadHandler.cs          → implement IDownloadHandler của CEF
│   ├── ContextMenuHandler.cs       → implement IContextMenuHandler
│   ├── RequestContextFactory.cs    → Tạo RequestContext theo Profile
├── Resources/
│   ├── Themes/LightTheme.xaml, DarkTheme.xaml
│   ├── Icons/ (SVG/PNG - tự vẽ, không lấy icon gốc của Chrome/Edge)
```

### 11.2. Các Handler/Interface quan trọng của CefSharp cần implement

| Interface CEF | Mục đích | Độ ưu tiên |
|---|---|---|
| `ILifeSpanHandler` | Bắt sự kiện mở tab/popup mới (window.open) để tự tạo Tab trong UI thay vì mở cửa sổ Chromium mặc định | **Cao** |
| `IRequestHandler` | Can thiệp request (chặn quảng cáo, redirect, xử lý lỗi SSL) | Cao |
| `IDownloadHandler` | Custom UI hiển thị tiến trình tải file thay vì UI mặc định của CEF | Cao |
| `IContextMenuHandler` | Custom menu chuột phải theo phong cách app | Trung bình |
| `IKeyboardHandler` | Bắt phím tắt (Ctrl+T, Ctrl+W, Ctrl+Tab...) | Trung bình |
| `IDialogHandler` | Custom dialog chọn file (upload) | Trung bình |
| `IJsDialogHandler` | Custom alert/confirm/prompt JS thay vì dialog mặc định Windows | Trung bình |
| `IFindHandler` | Hỗ trợ tính năng Find in page | Thấp–Trung bình |
| `DisplayHandler` (`OnTitleChanged`, `OnFaviconUrlChange`, `OnLoadingStateChange`) | Cập nhật Title/Favicon/trạng thái nút Back-Forward-Reload lên UI Tab | **Cao — bắt buộc có sớm** |

### 11.3. Vòng đời khởi tạo CEF (quan trọng, hay bị lỗi)

```
1. App.xaml.cs (OnStartup)
   → Cấu hình CefSettings { CachePath, MultiThreadedMessageLoop = true, ... }
   → Cef.Initialize(settings) — PHẢI gọi trước khi tạo bất kỳ ChromiumWebBrowser nào
2. Tạo MainWindow → mỗi Tab = 1 instance ChromiumWebBrowser (hoặc dùng chung 1 browser + swap qua Multi-tab bằng CefSharp cách khác nếu tối ưu RAM)
3. Khi đóng App → Cef.Shutdown() ở OnExit, PHẢI gọi cuối cùng sau khi tất cả browser đã Dispose()
```

> ⚠️ Lỗi thường gặp nhất khi mới làm: quên set `Platform Target = x64` (không dùng AnyCPU), và quên Dispose từng `ChromiumWebBrowser` trước khi gọi `Cef.Shutdown()` gây crash khi thoát app.

---

## 12. Bảng đánh giá độ khó triển khai theo tính năng

| Tính năng | Độ khó | Ước lượng công sức | Ghi chú |
|---|---|---|---|
| Toolbar cơ bản (Back/Forward/Reload/Home + Address bar) | **Dễ** | 1–2 ngày | Baseline, gần giống repo tham khảo mục 15 |
| Tab đa năng (mở/đóng/kéo-thả sắp xếp) | **Trung bình** | 3–5 ngày | Kéo-thả (Drag reorder) là phần khó nhất |
| Favicon + Loading throbber | Dễ | 0.5–1 ngày | Dùng `OnFaviconUrlChange` |
| Autocomplete gợi ý địa chỉ (dropdown) | **Trung bình–Khó** | 3–5 ngày | Cần tự xây thuật toán ranking (tần suất + gần đây) |
| Bookmarks (lưu + quản lý cây thư mục) | Trung bình | 3–4 ngày | |
| History page + tìm kiếm | Trung bình | 2–3 ngày | |
| Download manager UI | Trung bình | 2–3 ngày | Cần implement `IDownloadHandler` |
| Settings page đầy đủ | **Khó** | 5–10 ngày | Tuỳ số lượng mục cài đặt |
| Multi-Profile (đa hồ sơ) | **Khó** | 5–7 ngày | Vấn đề RAM & RequestContext |
| Incognito/Chế độ ẩn danh | Trung bình | 1–2 ngày | RequestContext không ghi đĩa |
| Extension support | **Rất khó** | Nhiều tuần–không khuyến khích | CEF hỗ trợ hạn chế Chrome Extension API, rất phức tạp |
| Đặt làm trình duyệt mặc định Windows | **Khó** | 2–3 ngày | Thao tác Registry + App Registration, cần test kỹ trên Win 10/11 |
| Đồng bộ đa thiết bị (Cloud Sync) | **Rất khó** | Cần backend riêng | Ngoài phạm vi CEF, cần server riêng |
| Tab Groups (nhóm tab màu) | Khó | 3–5 ngày | UI + logic lưu trạng thái nhóm |
| Sidebar (kiểu Edge) | Khó | 4–6 ngày | Thêm panel WPF riêng cạnh WebView |
| Theme Sáng/Tối + Mica effect | Trung bình | 2–3 ngày | Dùng thư viện `WPF-UI` hoặc `ModernWpfUI` hỗ trợ sẵn |

---

## 13. Rủi ro Sở hữu trí tuệ & Bản quyền

Đây là phần **bạn cần đặc biệt lưu ý** trước khi phát hành sản phẩm thương mại/công khai:

### 13.1. Những gì **AN TOÀN** để tham khảo/sử dụng

| Hạng mục | Lý do an toàn |
|---|---|
| **Chromium Embedded Framework (CEF) / CefSharp** | Mã nguồn mở theo giấy phép **BSD-style** (cả CEF lẫn CefSharp) — được phép sử dụng, kể cả mục đích thương mại, miễn tuân thủ điều khoản giấy phép (giữ thông báo bản quyền gốc trong file license đi kèm) |
| **Bố cục/khái niệm UI tổng quát** (tab trên đỉnh, address bar, nút back/forward) | Đây là các **mẫu hình chức năng (UX pattern)** đã trở thành chuẩn ngành, không phải đối tượng bảo hộ bản quyền — tương tự như không ai độc quyền "menu có nút Save" |
| **Tự vẽ icon theo phong cách tương tự** (không sao chép pixel-by-pixel) | Ý tưởng thiết kế (mũi tên back, ổ khoá bảo mật...) là biểu tượng phổ quát (generic symbol), không phải tác phẩm độc quyền |
| **Tên gọi chức năng chung** (Bookmarks, History, Downloads, Settings...) | Từ ngữ mô tả chức năng thông thường, không phải nhãn hiệu độc quyền |

### 13.2. Những gì **CẦN TRÁNH** hoặc **RỦI RO CAO**

| Hạng mục | Rủi ro | Khuyến nghị |
|---|---|---|
| **Sao chép logo/icon chính thức của Chrome, Edge, Firefox** (bộ icon tam giác 3 màu, logo cáo lửa, logo sóng Edge...) | Vi phạm **bản quyền hình ảnh** và **nhãn hiệu (trademark)** — các logo này được đăng ký bảo hộ bởi Google/Microsoft/Mozilla | Tự thiết kế logo/icon riêng hoàn toàn cho sản phẩm của bạn |
| **Đặt tên sản phẩm gây nhầm lẫn** (vd: "Chrome Pro", "Edge X", "Chromium Turbo"...) | Vi phạm **nhãn hiệu (trademark infringement)**, có thể bị yêu cầu gỡ/kiện | Đặt tên thương hiệu hoàn toàn độc lập, không chứa tên các trình duyệt lớn |
| **Sao chép nguyên văn CSS/asset của trang `chrome://settings` hoặc `about:preferences`** (nếu lấy trực tiếp source từ Chromium/Firefox source tree cho phần UI riêng, không phải phần engine) | Chromium source phần lớn là BSD nhưng **một số asset (icon, font đặc thù, wordmark)** không nằm trong phạm vi giấy phép mở — cần kiểm tra từng file | Tự build UI Settings bằng XAML/HTML riêng, chỉ tham khảo *cấu trúc chức năng*, không copy asset |
| **Sử dụng "CEF"/"Chromium Embedded Framework" trong tên sản phẩm để ngụ ý được Google chứng thực** | Gây hiểu lầm về mối liên hệ chính thức | Có thể ghi "Powered by Chromium/CEF" ở phần About/Credits (thông lệ phổ biến, minh bạch) nhưng không dùng làm thương hiệu chính |
| **Bộ font hệ thống độc quyền** (Segoe UI là font độc quyền của Microsoft, chỉ được dùng hợp pháp khi chạy trên Windows có bản quyền, không được đóng gói font này phân phối lại) | Vi phạm giấy phép font | Dùng font hệ thống mặc định của máy người dùng (không nhúng file font Segoe UI vào bộ cài) hoặc dùng font mã nguồn mở (Inter, Roboto, Noto Sans...) |
| **Search Engine mặc định trỏ vào Google/Bing mà không qua API/thoả thuận chính thức** | Vi phạm điều khoản dịch vụ (Terms of Service) của công cụ tìm kiếm nếu tự động hoá truy vấn trái phép quy mô lớn | Với mục đích cá nhân/nội bộ ở mức người dùng thông thường thường không vấn đề gì (giống hành vi gõ URL bình thường), nhưng nếu phân phối rộng/thương mại nên đọc kỹ ToS của Google Search / Bing API và cân nhắc dùng API tìm kiếm chính thức có trả phí nếu cần |
| **Extension Chrome Web Store** | CEF không hỗ trợ đầy đủ Chrome Extension Manifest V3, việc "giả lập" hỗ trợ extension để tải extension từ Chrome Web Store có thể vi phạm điều khoản phân phối của Google | Tránh, hoặc chỉ hỗ trợ extension dạng riêng do bạn tự định nghĩa |

### 13.3. Khuyến nghị pháp lý tổng quát

> 📌 Tài liệu này **không phải là tư vấn pháp lý chính thức**. Nếu sản phẩm hướng tới thương mại hoá hoặc phân phối rộng rãi (public release), bạn nên:
> 1. Đọc kỹ giấy phép **BSD** của CEF/CefSharp và **Chromium** (LICENSE file trong repo gốc).
> 2. Tham vấn luật sư sở hữu trí tuệ trước khi đặt tên thương hiệu/logo chính thức.
> 3. Rà soát danh sách "Third-party notices" mà CEF/Chromium yêu cầu đính kèm khi phân phối bản build.

---

## 14. Lộ trình phát triển đề xuất (Roadmap)

```
Giai đoạn 1 — Nền tảng (đã có sẵn theo bạn mô tả)
  ✅ Khởi tạo CEF, hiển thị 1 WebView, nút Back/Forward/Reload, Address bar cơ bản

Giai đoạn 2 — Đa Tab (ưu tiên tiếp theo)
  ☐ Custom Tab Strip (không dùng TabControl mặc định)
  ☐ Mỗi tab = 1 ChromiumWebBrowser, cập nhật Title/Favicon/Loading realtime
  ☐ Ctrl+T / Ctrl+W / Ctrl+Tab shortcuts
  ☐ Kéo-thả sắp xếp tab

Giai đoạn 3 — Dữ liệu người dùng
  ☐ SQLite: History, Bookmarks, Downloads
  ☐ UI trang History / Bookmark Manager / Downloads panel
  ☐ Autocomplete address bar dựa trên History + Bookmark

Giai đoạn 4 — Cài đặt & Cá nhân hoá
  ☐ Settings page (Search engine, Startup, Appearance, Privacy)
  ☐ Theme Sáng/Tối
  ☐ Đặt làm trình duyệt mặc định (Registry)

Giai đoạn 5 — Nâng cao
  ☐ Multi-Profile / Incognito
  ☐ Tab Groups
  ☐ Sidebar tuỳ chỉnh
  ☐ Đóng gói cài đặt (Installer - MSIX/Inno Setup) + Auto-update
```

---

## 15. Tài liệu tham khảo

| Nguồn | Nội dung liên quan |
|---|---|
| [Chromium UX Documentation](https://www.chromium.org/user-experience/) | Tài liệu chính thức về triết lý thiết kế Window Frame, Tab, Throbber, Toolbar, Omnibox của Chromium |
| [CefSharp Official Site](https://cefsharp.github.io/) | Trang chủ CefSharp, hướng dẫn cài đặt, kiến trúc, API reference |
| [CefSharp GitHub - ChromiumWebBrowser.cs (WPF)](https://github.com/cefsharp/CefSharp/blob/master/CefSharp.Wpf/ChromiumWebBrowser.cs) | Source code control WPF chính, các DependencyProperty (Address, IsLoading...) |
| [CefSharp ChromiumWebBrowser Class API Docs](https://cefsharp.github.io/api/57.0.0/html/T_CefSharp_Wpf_ChromiumWebBrowser.htm) | Tài liệu API chi tiết class chính |
| [How to customize the Google Chrome toolbar (9to5Google, 2025)](https://9to5google.com/2025/01/17/google-chrome-toolbar-customize/) | Chi tiết tính năng tuỳ biến toolbar Chrome 132/2025 |
| [Chrome Toolbar Structure Guide](https://www.clrn.org/how-to-customize-toolbar-in-chrome/) | Phân tích cấu trúc Omnibox, nút điều hướng |
| [CefSharp WPF Tutorial - technical-recipes.com](https://www.technical-recipes.com/2016/using-the-cefsharp-chromium-web-browser-in-wpf-xaml/) | Hướng dẫn nhúng ChromiumWebBrowser cơ bản vào XAML |
| [CefSharp Tutorials - One-Tabbed Browser WPF](http://www.cefsharptutorials.com/One-Tabbed-Browser-with-URL-Navigation-in-WPF-Application-Using-CefSharp/) | Ví dụ nút Back/Forward/Navigate cơ bản |
| [praisecaleb/csharp-wpf-browser (GitHub, mã nguồn mở tham khảo)](https://github.com/praisecaleb/csharp-wpf-browser) | Repo mã nguồn mở minh hoạ trình duyệt WPF+CefSharp với address bar thông minh, status bar, navigation state |
| [FoxLearn - Chromium Browser with Tabs using CefSharp](https://foxlearn.com/windows-forms/chromium-browser-with-tabs-using-cefsharp-in-csharp-147.html) | Ví dụ triển khai TabControl kết hợp CefSharp |
| [Google Chrome Browser UI Kit 2025 (Figma Community)](https://www.figma.com/community/file/1265084211900612593/google-chrome-browser-ui-kit-2025) | UI Kit cộng đồng minh hoạ trực quan các thành phần Chrome mới nhất — **chỉ dùng để tham khảo bố cục, không copy asset gốc thuộc bản quyền Google** |

---

## Phụ lục: Danh sách phím tắt tiêu chuẩn nên hỗ trợ

| Phím tắt | Chức năng |
|---|---|
| `Ctrl+T` | Mở tab mới |
| `Ctrl+W` / `Ctrl+F4` | Đóng tab hiện tại |
| `Ctrl+Shift+T` | Mở lại tab vừa đóng |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Chuyển tab kế/trước |
| `Ctrl+1..8` | Nhảy đến tab thứ N |
| `Ctrl+L` / `Alt+D` | Focus vào Address bar |
| `Ctrl+R` / `F5` | Reload trang |
| `Alt+←` / `Alt+→` | Back / Forward |
| `Ctrl+D` | Bookmark trang hiện tại |
| `Ctrl+H` | Mở History |
| `Ctrl+J` | Mở Downloads |
| `Ctrl+Shift+N` | Mở cửa sổ ẩn danh mới |
| `Ctrl++` / `Ctrl+-` / `Ctrl+0` | Zoom in/out/reset |
| `F11` | Toàn màn hình |
| `F12` | Developer Tools (CEF hỗ trợ sẵn `ShowDevTools()`) |

---

*Hết tài liệu. Bạn nên coi đây là tài liệu "sống" — cập nhật thêm khi có quyết định thiết kế cụ thể trong quá trình build thực tế.*
