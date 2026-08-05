# Tài liệu Nghiên Cứu & Đặc Tả Thiết Kế Trình Duyệt Web
### (Tham khảo Chrome / Edge / Firefox / Brave / Arc / Safari — Áp dụng cho dự án CEFSharp + C# WPF)

> **Mục đích tài liệu:** Tổng hợp kiến trúc giao diện (UI/UX), thành phần chức năng, hành vi, kích thước, và các lưu ý kỹ thuật/pháp lý của các trình duyệt phổ biến hiện nay, làm nền tảng để phát triển một trình duyệt nhúng chuyên nghiệp bằng **CefSharp (Chromium Embedded Framework) trên nền C# WPF**.
>
> **Ngày soạn:** 02/08/2026 — **Bản tổng hợp cập nhật:** 03/08/2026 (đã gộp phần chi tiết Trang History & Trang Settings)
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

### 6.1. Trang/Panel History (Lịch sử) — Mô tả đầy đủ chi tiết

Trang History có thể mở dưới dạng **panel nổi nhỏ** (dropdown ngắn gọn, kiểu Firefox) hoặc **trang toàn màn hình riêng** (kiểu `chrome://history` — khuyến nghị dùng cho trình duyệt của bạn vì dễ mở rộng và thao tác hàng loạt).

#### 6.1.1. Điểm truy cập (Entry points)

| Cách mở | Ghi chú |
|---|---|
| Phím tắt `Ctrl+H` | Chuẩn phổ biến nhất |
| Menu (⋮) ▸ History ▸ History | Mục cha "History" thường có submenu con hiện **8–10 trang gần nhất** để click nhanh, kèm mục "Show full history" ở cuối |
| Gõ `history` hoặc URL nội bộ (vd `app://history`) vào address bar | Tương tự `chrome://history` |
| Nút Back/Forward giữ chuột (long-press) | Hiện dropdown lịch sử **chỉ trong phạm vi tab hiện tại** (khác với trang History toàn cục) |

#### 6.1.2. Bố cục tổng thể trang History

```
┌───────────────────────────────────────────────────────────┐
│  ☰  Lịch sử                          [ 🔍 Tìm trong lịch sử ]│  ← Header
├───────────┬───────────────────────────────────────────────┤
│ Sidebar   │  ☑ Chọn tất cả     [Xoá mục đã chọn] [Xuất ▾]  │  ← Toolbar hành động
│ - Lịch sử │  ─── Hôm nay ──────────────────────────────────│
│ - Tab TB  │  ☐ 14:32  [favicon] Tiêu đề trang — url.com    │
│   khác    │  ☐ 13:05  [favicon] Tiêu đề trang — url2.com   │
│           │  ─── Hôm qua ──────────────────────────────────│
│           │  ☐ 20:11  [favicon] Tiêu đề trang — url3.com   │
│           │  ...                                            │
│           │  [Tải thêm ▾] (hoặc infinite scroll)            │
└───────────┴───────────────────────────────────────────────┘
```

#### 6.1.3. Chi tiết từng thành phần

| Thành phần | Mô tả chi tiết | Độ phức tạp |
|---|---|---|
| **Sidebar trái** | Gồm mục "History" (đang chọn), "Tabs from other devices" (đồng bộ đa thiết bị — có thể bỏ qua nếu không làm cloud sync), "Clear browsing data" (mở dialog riêng) | Dễ |
| **Ô tìm kiếm** (góc trên phải) | Tìm full-text theo cả **tiêu đề trang** lẫn **URL**, lọc realtime khi gõ (debounce ~200–300ms để tránh query liên tục), highlight từ khoá khớp trong kết quả | Trung bình |
| **Nhóm theo ngày** | Chia thành các nhóm: *Hôm nay*, *Hôm qua*, *Thứ [tên] tuần trước…*, sau đó theo *Ngày/Tháng/Năm* cụ thể — sắp xếp mới nhất lên trên | Trung bình |
| **Mỗi dòng lịch sử (History Entry)** | Gồm: (1) Checkbox chọn dòng, (2) Giờ truy cập (HH:mm), (3) Favicon 16×16, (4) Tiêu đề trang (bold, có thể click để mở lại), (5) URL rút gọn hiển thị màu xám nhạt bên cạnh tiêu đề, (6) Nút "×" ẩn/hiện khi hover để xoá riêng dòng đó | Trung bình |
| **Gộp nhiều lượt truy cập cùng 1 trang trong thời gian ngắn** | Nếu người dùng vào lại cùng URL nhiều lần liên tiếp, có thể gộp hiển thị kèm số đếm "(x3)" thay vì liệt kê lặp lại — tối ưu UX (tuỳ chọn nâng cao) | Khó |
| **Checkbox "Chọn tất cả" theo nhóm ngày** | Hiện khi hover vào tiêu đề nhóm ngày, cho phép chọn nhanh cả nhóm để xoá hàng loạt | Trung bình |
| **Thanh hành động khi có mục được chọn** | Hiện nổi lên (sticky) khi có ≥1 checkbox được tick: nút "Xoá (n) mục đã chọn", nút "Mở tất cả trong tab mới" | Trung bình |
| **Nút "Xoá dữ liệu duyệt web" (Clear browsing data)** | Mở **dialog riêng** (xem chi tiết bên dưới, mục 6.1.4) | — |
| **Phân trang / Infinite scroll** | Chrome dùng infinite scroll (tự tải thêm khi cuộn gần cuối danh sách), nên giới hạn **load theo batch ~50–100 dòng/lần** để tránh giật khi danh sách quá dài (hàng chục nghìn record) | Trung bình–Khó (cần virtualization, vd `VirtualizingStackPanel` trong WPF) |
| **Trạng thái rỗng (Empty state)** | Khi chưa có lịch sử hoặc sau khi tìm kiếm không ra kết quả → hiện icon + text "Không tìm thấy trang nào phù hợp" | Dễ |
| **Context menu chuột phải trên 1 dòng** | "Mở trong tab mới", "Mở trong cửa sổ mới", "Sao chép liên kết", "Xoá khỏi lịch sử" | Trung bình |

#### 6.1.4. Dialog "Xoá dữ liệu duyệt web" (Clear Browsing Data)

Đây là dialog **quan trọng nhất liên quan đến quyền riêng tư**, cần làm cẩn thận:

| Vùng | Nội dung |
|---|---|
| **Tab "Cơ bản" (Basic)** | 3 checkbox chính: ☑ Lịch sử duyệt web, ☑ Cookie và dữ liệu trang web khác, ☑ Hình ảnh và tệp đã lưu trong bộ nhớ đệm (Cache) |
| **Tab "Nâng cao" (Advanced)** | Thêm: ☐ Mật khẩu đã lưu, ☐ Dữ liệu tự động điền (Autofill), ☐ Cài đặt trang web (Site settings/permissions), ☐ Dữ liệu ứng dụng đã cài (nếu hỗ trợ PWA) |
| **Dropdown khoảng thời gian** | *Giờ vừa qua* / *24 giờ qua* / *7 ngày qua* / *4 tuần qua* / *Toàn bộ thời gian* — filter theo mốc thời gian trước khi xoá |
| **Nút hành động** | "Xoá dữ liệu" (màu accent/primary) + "Huỷ" — nên có `ConfirmationDialog` phụ nếu chọn "Toàn bộ thời gian" + tất cả checkbox (hành động không thể hoàn tác) |

> ⚠️ **Lưu ý kỹ thuật CefSharp:** Xoá History ở tầng ứng dụng (SQLite tự quản lý) là việc của bạn, nhưng xoá **Cookies/Cache/Site data** phải gọi qua API của CEF: `IRequestContext.ClearCertificateExceptionsAsync()`, và với cookie dùng `ICookieManager.DeleteCookiesAsync(url, name)`; cache thường nên xoá bằng cách **xoá thư mục `CachePath`** khi browser đã đóng hoàn toàn (không xoá khi đang chạy vì file đang bị khoá).

#### 6.1.5. Gợi ý schema dữ liệu (SQLite)

```sql
-- Bảng lưu URL duy nhất (tránh trùng lặp dữ liệu)
CREATE TABLE urls (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    url TEXT NOT NULL UNIQUE,
    title TEXT,
    visit_count INTEGER DEFAULT 0,
    last_visit_time INTEGER,     -- Unix timestamp
    favicon_url TEXT
);

-- Bảng lưu từng lượt truy cập cụ thể (1 URL có thể có nhiều lượt)
CREATE TABLE visits (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    url_id INTEGER NOT NULL REFERENCES urls(id) ON DELETE CASCADE,
    visit_time INTEGER NOT NULL, -- Unix timestamp
    transition_type TEXT         -- 'typed' | 'link' | 'reload' | 'redirect'...
);

CREATE INDEX idx_visits_time ON visits(visit_time DESC);
CREATE INDEX idx_urls_title  ON urls(title);
```

> 💡 Tách 2 bảng `urls` và `visits` (giống kiến trúc thật của Chromium `History` service) giúp việc **tính tần suất truy cập** (phục vụ autocomplete ở mục 5.3) chính xác và nhanh hơn nhiều so với việc chỉ lưu 1 bảng phẳng.

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

### 7.2. Bố cục tổng thể trang Settings

```
┌─────────────────────────────────────────────────────────────┐
│  ←  Cài đặt                    [ 🔍 Tìm kiếm trong cài đặt ]  │  ← Header + Search
├───────────────┬───────────────────────────────────────────┤
│ SIDEBAR (trái, sticky, ~240px) │  NỘI DUNG (phải, cuộn dọc)   │
│ ● Bạn và Google                │  ┌─────────────────────┐   │
│   Bảo mật & quyền riêng tư     │  │ [Card nhóm cài đặt]  │   │
│   Giao diện                    │  │  - Toggle/Dropdown   │   │
│   Công cụ tìm kiếm             │  │  - Toggle/Dropdown   │   │
│   Trình duyệt mặc định         │  └─────────────────────┘   │
│   Khi khởi động                │  ┌─────────────────────┐   │
│   Ngôn ngữ                     │  │ [Card nhóm cài đặt]  │   │
│   Tải xuống                    │  └─────────────────────┘   │
│   Trợ năng                     │                             │
│   Hệ thống                     │                             │
│   Giới thiệu                   │                             │
└───────────────┴───────────────────────────────────────────┘
```

- Sidebar dùng **danh sách mục có icon**, mục đang active có nền highlight nhẹ (`--accent` nhạt) + thanh dọc màu accent bên trái.
- Nội dung bên phải chia thành từng **"Card"** (khối bo góc, có shadow nhẹ hoặc viền `--border`), mỗi Card = 1 nhóm chức năng liên quan, có tiêu đề nhóm + mô tả phụ (subtitle) nhỏ màu xám bên dưới mỗi control.
- Khi click 1 mục sidebar → **cuộn mượt (smooth scroll)** đến Card tương ứng nếu dùng layout 1 trang dài, hoặc **chuyển view** nếu dùng layout multi-page (khuyến nghị multi-page vì dễ quản lý code hơn với XAML `Frame`/`Page` navigation).

### 7.3. Chi tiết từng trang con (control cụ thể, dùng để thiết kế UI)

#### 7.3.1. Trang "Bạn và tài khoản" (You and Account)

| Control | Loại | Mô tả |
|---|---|---|
| Card đăng nhập | Avatar + Tên + nút "Đăng nhập" | Nếu không làm hệ thống tài khoản cloud, có thể thay bằng "Thông tin Profile local" |
| Danh sách Profile | List item có avatar | Click → mở panel Quản lý Profile (xem mục 8) |
| Toggle "Đồng bộ" | Switch bật/tắt | Chỉ hiển thị nếu có backend sync riêng — nếu không, **ẩn hẳn mục này** |

#### 7.3.2. Trang "Công cụ tìm kiếm" (Search Engine)

| Control | Loại | Mô tả |
|---|---|---|
| Dropdown "Công cụ tìm kiếm dùng trong address bar" | Combobox | Danh sách: Google, Bing, DuckDuckGo, Cốc Cốc, Tuỳ chỉnh... |
| Bảng "Quản lý công cụ tìm kiếm" | DataGrid/List | Cột: Tên, Từ khoá gợi nhớ (keyword, vd gõ "yt" + Tab để search trực tiếp trên YouTube), URL truy vấn (chứa `%s` thay cho từ khoá) |
| Nút "Thêm" | Button | Mở form nhập: Tên / Keyword / URL mẫu (vd `https://www.google.com/search?q=%s`) |
| Nút xoá/sửa mỗi dòng | Icon button | Hover hiện |

#### 7.3.3. Trang "Trình duyệt mặc định" (Default Browser)

| Control | Loại | Mô tả |
|---|---|---|
| Trạng thái hiện tại | Text + icon | "✅ Đây là trình duyệt mặc định" hoặc "⚠️ Chưa phải trình duyệt mặc định" |
| Nút "Đặt làm mặc định" | Button primary | Gọi API Windows: mở trực tiếp `ms-settings:defaultapps` **hoặc** dùng `IApplicationAssociationRegistrationUI` (Windows API) để hiện dialog chọn nhanh — CHÚ Ý: từ Windows 10 1803+ trở đi, Microsoft **giới hạn quyền set default programmatically**, ứng dụng thường chỉ có thể *mở* trang Settings hệ thống để người dùng tự chọn, không thể tự set ngầm |

#### 7.3.4. Trang "Khi khởi động" (On Startup)

| Control | Loại | Mô tả |
|---|---|---|
| Radio "Mở trang Tab mới" | Radio button | Mặc định |
| Radio "Tiếp tục nơi đã dừng lại" | Radio button | Khôi phục toàn bộ session tab đã mở lần cuối (đọc từ `Session.json`) |
| Radio "Mở một trang cụ thể hoặc nhiều trang" | Radio button | Hiện danh sách URL bên dưới khi chọn, có nút "Thêm trang", "Dùng trang hiện tại", "Dùng tất cả tab đang mở" |

#### 7.3.5. Trang "Giao diện" (Appearance)

| Control | Loại | Mô tả |
|---|---|---|
| Chọn Theme | 3 ô lựa chọn hình ảnh preview (Sáng / Tối / Theo hệ thống) | Click đổi ngay lập tức toàn bộ UI (dùng `ResourceDictionary` swap trong WPF) |
| Toggle "Hiện thanh Bookmarks" | Switch | Kèm dropdown con: Luôn hiện / Chỉ hiện ở Tab mới / Luôn ẩn |
| Slider "Kích thước chữ trang web" | Slider hoặc Dropdown (Nhỏ/Vừa/Lớn/Rất lớn) | Map sang `zoom text only` của CEF nếu hỗ trợ, hoặc CSS injection |
| Dropdown "Mức zoom mặc định trang" | Combobox % | 50%–200% |
| Toggle "Hiện nút Home" | Switch | Kèm ô nhập URL trang chủ tuỳ chỉnh khi bật |
| Chọn màu Accent (tuỳ chọn nâng cao) | Bảng màu preset | Đổi `--accent` token toàn UI |

#### 7.3.6. Trang "Quyền riêng tư & bảo mật" (Privacy & Security)

| Control | Loại | Mô tả |
|---|---|---|
| Nút "Xoá dữ liệu duyệt web" | Button | Mở dialog đã mô tả ở mục 6.1.4 |
| Dropdown "Cookie của bên thứ ba" | Combobox | Cho phép tất cả / Chặn trong chế độ ẩn danh / Chặn tất cả |
| Toggle "Gửi yêu cầu Do Not Track" | Switch | Chỉ gửi header, không có tác dụng bắt buộc phía server |
| Toggle "Safe Browsing" (nếu tích hợp Google Safe Browsing API hoặc tương đương) | Switch + Radio (Bảo vệ nâng cao/Tiêu chuẩn/Tắt) | Cảnh báo trang lừa đảo/malware — cần gọi API bên thứ 3, độ khó cao |
| **Bảng "Quyền của trang web" (Site Permissions)** | List các quyền: Vị trí, Camera, Micro, Thông báo, Popup & chuyển hướng, JavaScript, Hình ảnh | Mỗi quyền có dropdown mặc định (Hỏi trước/Cho phép/Chặn) + danh sách ngoại lệ theo từng domain cụ thể |
| Toggle "HTTPS-Only Mode" | Switch | Tự động nâng cấp mọi kết nối HTTP lên HTTPS khi có thể |

#### 7.3.7. Trang "Tự động điền" (Autofill and Passwords)

| Control | Loại | Mô tả |
|---|---|---|
| Toggle "Lưu mật khẩu" | Switch | Bật/tắt tính năng hỏi lưu mật khẩu khi đăng nhập |
| Bảng "Mật khẩu đã lưu" | DataGrid | Cột: Website, Username, Password (ẩn dạng `••••••`, click icon 👁 để hiện — **yêu cầu xác thực lại bằng mật khẩu Windows** trước khi hiện, dùng Windows Hello/`CredentialPicker` nếu có) |
| Bảng "Địa chỉ" | DataGrid | Tên, Địa chỉ, SĐT — dùng cho autofill form |
| Bảng "Phương thức thanh toán" | DataGrid | Số thẻ (ẩn trừ 4 số cuối), Ngày hết hạn — **mã hoá bắt buộc**, cân nhắc kỹ trước khi làm tính năng này vì rủi ro bảo mật cao nếu làm sai |

#### 7.3.8. Trang "Ngôn ngữ" (Languages)

| Control | Loại | Mô tả |
|---|---|---|
| Danh sách ngôn ngữ ưu tiên | List kéo-thả sắp xếp thứ tự | Ngôn ngữ đầu tiên = ngôn ngữ hiển thị UI |
| Toggle "Đề nghị dịch trang không cùng ngôn ngữ" | Switch | Cần tích hợp API dịch (Google Translate API hoặc tương đương — có phí) |

#### 7.3.9. Trang "Tải xuống" (Downloads)

| Control | Loại | Mô tả |
|---|---|---|
| Đường dẫn thư mục lưu mặc định | Text field (readonly) + nút "Thay đổi" | Mở `FolderBrowserDialog`/`OpenFolderDialog` của WPF |
| Toggle "Hỏi vị trí lưu cho mỗi file" | Switch | Nếu bật → mỗi lần tải hiện `SaveFileDialog` thay vì tự lưu vào thư mục mặc định |
| Toggle "Mở file PDF trong trình duyệt" | Switch | Cần render PDF viewer (CEF hỗ trợ sẵn PDF plugin cơ bản) |

#### 7.3.10. Trang "Trợ năng" (Accessibility)

| Control | Loại | Mô tả |
|---|---|---|
| Slider "Phóng to văn bản mặc định" | Slider % | |
| Toggle "Tô sáng vùng đang Focus" | Switch | Hỗ trợ điều hướng bằng bàn phím |
| Toggle "Chuyển động rút gọn (Reduce motion)" | Switch | Tắt animation UI cho người nhạy cảm chuyển động |

#### 7.3.11. Trang "Hệ thống" (System)

| Control | Loại | Mô tả |
|---|---|---|
| Toggle "Dùng tăng tốc phần cứng (GPU)" | Switch | Map sang `CefSettings.WindowlessRenderingEnabled` / cờ GPU của CEF, cần khởi động lại khi đổi |
| Toggle "Tiếp tục chạy nền khi đóng cửa sổ cuối" | Switch | Cho phép ứng dụng minimize xuống system tray thay vì thoát hẳn |
| Cấu hình Proxy | Nút "Mở cài đặt Proxy hệ thống" hoặc form nhập Proxy thủ công (host/port/username/password) | Độ khó cao nếu tự làm proxy riêng thay vì dùng proxy hệ thống Windows |

#### 7.3.12. Trang "Giới thiệu" (About)

| Control | Loại | Mô tả |
|---|---|---|
| Logo + Tên ứng dụng + Số phiên bản | Static | vd "MyBrowser — Phiên bản 1.0.0 (build 20260803)" |
| Nút "Kiểm tra cập nhật" | Button | Gọi API check version từ server riêng của bạn (hoặc GitHub Releases API nếu mã nguồn mở) |
| Thông tin engine | Static text | "Powered by Chromium/CEF — [phiên bản CEF đang dùng]" (nên có, minh bạch & đúng thông lệ, xem mục 13.2) |
| Link "Giấy phép mã nguồn mở bên thứ ba" | Link | Trỏ tới trang liệt kê license CEF/CefSharp/các thư viện NuGet khác đã dùng |

### 7.4. Nguyên tắc UX cho trang Settings

- Dùng **thanh tìm kiếm ở đầu trang** để lọc nhanh các mục cài đặt (rất được đánh giá cao về UX, nên ưu tiên làm sớm) — khi gõ, highlight + tự cuộn đến control khớp từ khoá trên toàn bộ các trang con, không chỉ lọc theo tên nhóm.
- Mỗi mục thay đổi áp dụng **ngay lập tức** (không cần nút "Lưu"), trừ các thay đổi cần khởi động lại (hiện banner nhỏ dưới cùng: "Khởi động lại để áp dụng" kèm nút "Khởi động lại ngay").
- Settings nên là **một cửa sổ/tab riêng** dùng chung layout với trình duyệt (không phải dialog riêng biệt) để nhất quán trải nghiệm — mở bằng URL nội bộ dạng `app://settings/...` giống cách Chrome dùng `chrome://settings/...` cho từng trang con (dễ deep-link, dễ lưu lịch sử điều hướng trong Settings).
- Mỗi control nên có **mô tả phụ (subtitle)** giải thích ngắn gọn tác dụng — tránh để người dùng phải đoán ý nghĩa của toggle.
- Với các control ảnh hưởng bảo mật/quyền riêng tư quan trọng (xoá dữ liệu, hiện mật khẩu), luôn có **bước xác nhận phụ** trước khi thực thi.

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
