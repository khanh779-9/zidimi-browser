# UX/UI Design System — Quy Tắc Thiết Kế Toàn Diện

> Tài liệu này định nghĩa hệ thống quy tắc thiết kế (Design System) dùng chung cho nhiều loại phần mềm thuộc nhiều lĩnh vực: từ phần mềm doanh nghiệp (SaaS, ERP/CRM), thương mại (E-commerce, POS), tài chính (Fintech, Ví điện tử/Crypto, Bảo hiểm), y tế & sức khỏe, hệ thống (Trình duyệt, Antivirus), sáng tạo (Design Tools), nội dung & giải trí (Game, Streaming, Mạng xã hội), giáo dục & năng suất (E-learning, Ghi chú, Email, Calendar, Developer Tools), giao tiếp (Chat, Video Call), đời sống & dịch vụ (Giao đồ ăn, Du lịch, Bất động sản, Hẹn hò, Fitness), ngành dọc chuyên biệt (Logistics, HR/Tuyển dụng, Pháp lý), cho đến thiết bị đặc thù (IoT/Nhà thông minh, Wearable, Kiosk, Ô tô). Phần 1-4 là nền tảng dùng chung cho mọi lĩnh vực. Phần 5 là điều chỉnh riêng theo từng ngành.

---

## MỤC LỤC

1. [Nguyên tắc thiết kế chung](#1-nguyên-tắc-thiết-kế-chung)
2. [Hệ thống màu sắc](#2-hệ-thống-màu-sắc)
3. [Controls (Thành phần giao diện)](#3-controls-thành-phần-giao-diện)
4. [Bố cục & Layout](#4-bố-cục--layout)
5. [Quy tắc riêng theo lĩnh vực](#5-quy-tắc-riêng-theo-lĩnh-vực)
   - 5.1 SaaS Dashboard / Admin Panel · 5.2 E-commerce · 5.3 Fintech/Ngân hàng · 5.4 Y tế/Sức khỏe · 5.5 ERP/CRM/Quản lý dự án · 5.6 Trình duyệt · 5.7 Antivirus/Bảo mật · 5.8 Công cụ thiết kế chung
   - 5.9 Game UI/HUD · 5.10 Streaming/Media · 5.11 Mạng xã hội · 5.12 E-learning/LMS · 5.13 Ghi chú/Productivity · 5.14 Email Client · 5.15 Calendar/Lịch hẹn · 5.16 Developer Tools (IDE/API docs)
   - 5.17 Chat/Nhắn tin · 5.18 Video Call/Họp trực tuyến · 5.19 Giao đồ ăn/Giao hàng · 5.20 Du lịch/Đặt vé · 5.21 Bất động sản · 5.22 Hẹn hò · 5.23 Fitness/Sức khỏe cá nhân
   - 5.24 Bảo hiểm · 5.25 Logistics/Vận chuyển · 5.26 POS/Bán lẻ · 5.27 HR/Tuyển dụng · 5.28 Pháp lý (Legal Tech) · 5.29 Ví điện tử/Crypto
   - 5.30 IoT/Nhà thông minh · 5.31 Wearable/Smartwatch · 5.32 Kiosk/Màn hình tự phục vụ · 5.33 Ô tô/In-car UI
6. [Checklist kiểm tra trước khi ship](#6-checklist-kiểm-tra-trước-khi-ship)

---

## 1. NGUYÊN TẮC THIẾT KẾ CHUNG

### 1.1 Hệ thống khoảng cách (Spacing Scale)

Dùng thang đo bội số của **4px** (hoặc 8px cho layout lớn) để mọi khoảng cách luôn nhất quán, tránh dùng số tùy hứng (ví dụ 13px, 27px).

| Token | Giá trị | Dùng cho |
|-------|---------|----------|
| `space-1` | 4px | Khoảng cách icon-text sát nhau, padding trong badge nhỏ |
| `space-2` | 8px | Padding trong button nhỏ, khoảng cách giữa label và input |
| `space-3` | 12px | Padding input, khoảng cách giữa các control trong 1 nhóm |
| `space-4` | 16px | Padding card, khoảng cách giữa các field trong form |
| `space-5` | 24px | Khoảng cách giữa các section nhỏ |
| `space-6` | 32px | Khoảng cách giữa các block nội dung |
| `space-8` | 48px | Khoảng cách giữa các section lớn |
| `space-10` | 64px | Khoảng cách đầu/cuối trang, hero section |

**Quy tắc áp dụng:**
- Không tự chế giá trị ngoài thang đo (không dùng 10px, 15px, 22px...).
- Khoảng cách giữa 2 phần tử có liên quan chặt (label-input) dùng token nhỏ; giữa 2 nhóm không liên quan dùng token lớn hơn — tạo "khoảng trắng có ý nghĩa" (proximity principle của Gestalt).
- Padding trong component (button, card, input) luôn nhỏ hơn hoặc bằng margin ngoài component đó.

### 1.2 Hệ thống Typography

**Font chữ:**
- Chọn 1 font chính cho toàn bộ UI (khuyến nghị: Inter, SF Pro, Roboto, hoặc font hệ thống `-apple-system, Segoe UI, Roboto, sans-serif` để tối ưu hiệu năng và độ quen thuộc).
- Tối đa 2 font: 1 font UI (sans-serif) + 1 font mono (cho số liệu, code, mã giao dịch) nếu cần.
- Không dùng quá 2 kiểu font trong cùng 1 sản phẩm — gây rối mắt và thiếu chuyên nghiệp.

**Type Scale (thang cỡ chữ):**

| Token | Size | Line-height | Weight | Dùng cho |
|-------|------|-------------|--------|----------|
| `display` | 36-48px | 1.2 | 700 (Bold) | Trang chủ, hero, số liệu lớn (dashboard KPI) |
| `h1` | 28-32px | 1.25 | 700 | Tiêu đề trang |
| `h2` | 22-24px | 1.3 | 600 (Semibold) | Tiêu đề section |
| `h3` | 18-20px | 1.35 | 600 | Tiêu đề card, sub-section |
| `body-lg` | 16px | 1.5 | 400 (Regular) | Nội dung chính, mô tả quan trọng |
| `body` | 14px | 1.5 | 400 | Nội dung mặc định, text trong control |
| `body-sm` | 13px | 1.45 | 400 | Text phụ, helper text, caption |
| `label` | 12-13px | 1.4 | 500 (Medium) | Label form, tag, badge |
| `caption` | 11-12px | 1.4 | 400 | Timestamp, ghi chú nhỏ, footnote |

**Quy tắc:**
- Không dùng cỡ chữ dưới 11px cho bất kỳ nội dung nào người dùng cần đọc.
- Line-height luôn ≥ 1.2× font-size để đảm bảo dễ đọc; văn bản dài (paragraph) nên 1.5-1.6×.
- Độ dài dòng văn bản (line-length) lý tưởng: 50-75 ký tự/dòng cho nội dung đọc dài.
- Chỉ dùng tối đa 3 độ đậm (weight) trong 1 màn hình: Regular (400), Medium (500), Semibold/Bold (600-700).
- Tiêu đề và nội dung phải có khoảng cách thị giác rõ (dùng size + weight + màu, không chỉ dựa vào 1 yếu tố).

### 1.3 Kích thước Control (Sizing Scale)

Áp dụng nhất quán 3-4 mức kích thước cho mọi control tương tác (button, input, select...):

| Size | Chiều cao | Padding ngang | Font-size | Dùng cho |
|------|-----------|----------------|-----------|----------|
| `xs` | 24px | 8px | 12px | Bảng dữ liệu dày đặc, action icon trong table row |
| `sm` | 32px | 12px | 13px | Toolbar, filter, form phụ |
| `md` (mặc định) | 40px | 16px | 14px | Form chính, button chính, đa số control |
| `lg` | 48px | 20px | 16px | CTA chính, landing page, form đăng ký/thanh toán |

**Quy tắc:**
- Vùng chạm (touch target) tối thiểu **44×44px** trên mobile, **32×32px** trên desktop (theo WCAG 2.5.5 và Apple HIG).
- Icon-only button luôn có kích thước vuông bằng chiều cao control tương ứng (VD: button 40px cao → icon button 40×40px).
- Border-radius nhất quán theo 1 hệ thống: bo tròn nhẹ (4-8px) cho phong cách chuyên nghiệp/enterprise; bo tròn nhiều (12-16px+) cho phong cách thân thiện/consumer. Không trộn lẫn 2 phong cách trong cùng sản phẩm.
- Border-width chuẩn: 1px cho viền mặc định, 1.5-2px cho trạng thái focus/active.

### 1.4 Elevation & Shadow (Độ nổi)

Dùng shadow để thể hiện thứ bậc (hierarchy) — phần tử càng "nổi" (dropdown, modal, tooltip) càng cần shadow mạnh hơn.

| Level | Shadow | Dùng cho |
|-------|--------|----------|
| `elevation-0` | none / border 1px | Card nằm phẳng trên nền, input |
| `elevation-1` | `0 1px 2px rgba(0,0,0,0.05)` | Card hover nhẹ, button hover |
| `elevation-2` | `0 2px 8px rgba(0,0,0,0.08)` | Dropdown menu, popover |
| `elevation-3` | `0 8px 24px rgba(0,0,0,0.12)` | Modal, dialog |
| `elevation-4` | `0 16px 48px rgba(0,0,0,0.16)` | Toast thông báo nổi, command palette |

**Quy tắc:** Không dùng shadow quá đậm cho phần tử tĩnh (card thường) — dễ trông "nặng nề", lỗi thời. Ưu tiên border mỏng + shadow nhẹ hơn là shadow đậm.

### 1.5 Grid System

- Desktop: lưới **12 cột**, gutter (khoảng cách giữa cột) 16-24px, margin lề 24-32px (hoặc 48-64px cho layout rộng).
- Tablet: lưới **8 cột**, gutter 16px.
- Mobile: lưới **4 cột**, gutter 12-16px, margin lề 16px.
- Breakpoints chuẩn: `mobile < 640px`, `tablet 640-1024px`, `desktop 1024-1440px`, `wide > 1440px`.
- Nội dung văn bản dài không nên chiếm full-width màn hình lớn — giới hạn max-width 680-720px để giữ độ dài dòng dễ đọc.

### 1.6 Chuyển động (Motion)

- Thời lượng chuẩn: `100-150ms` cho vi tương tác (hover, toggle), `200-300ms` cho chuyển cảnh (modal mở, panel trượt), `300-400ms` cho page transition.
- Easing: dùng `ease-out` cho phần tử xuất hiện (vào nhanh, dừng êm), `ease-in` cho phần tử biến mất.
- Không animate quá 400ms — cảm giác chậm, ì.
- Tôn trọng `prefers-reduced-motion` cho người dùng nhạy cảm chuyển động.

---

## 2. HỆ THỐNG MÀU SẮC

### 2.1 Cấu trúc bảng màu (Color Palette Structure)

Mỗi màu trong hệ thống nên có **9-10 sắc độ (shade)** từ nhạt đến đậm, đặt tên theo số (50-900) hoặc (100-900):

```
primary-50   #EFF6FF   (nền nhạt nhất — dùng làm background vùng highlight)
primary-100  #DBEAFE   (nền hover nhẹ)
primary-200  #BFDBFE   (border, divider có màu)
primary-300  #93C5FD   (icon phụ, disabled state có màu)
primary-400  #60A5FA   (hover state của primary)
primary-500  #3B82F6   (màu chính — dùng cho button, link)
primary-600  #2563EB   (hover/active của button chính)
primary-700  #1D4ED8   (pressed state, text trên nền sáng cần đậm hơn)
primary-800  #1E40AF   (dùng cho text nhấn mạnh cần độ tương phản cao)
primary-900  #1E3A8A   (đậm nhất — hiếm dùng, dark mode accent)
```

**Nguyên tắc chọn màu chính (Primary):**
- 1 màu Primary duy nhất đại diện thương hiệu — dùng cho: nút hành động chính (CTA), link, phần tử đang active/selected, thanh tiến trình, checkbox/radio khi chọn.
- 1 màu Secondary/Accent (tùy chọn) — dùng cho hành động phụ, làm nổi bật thông tin đặc biệt (VD: badge "Mới", điểm nhấn khuyến mãi).

### 2.2 Màu ngữ nghĩa (Semantic Colors)

Bắt buộc phải có, tách biệt hoàn toàn khỏi Primary để không gây nhầm lẫn ý nghĩa:

| Vai trò | Tông màu gợi ý | Dùng cho |
|---------|----------------|----------|
| **Success** | Xanh lá `#16A34A` / `#22C55E` | Thao tác thành công, trạng thái hoàn tất, số dương (tăng trưởng) |
| **Warning** | Vàng/Cam `#F59E0B` / `#EAB308` | Cảnh báo cần chú ý, trạng thái chờ xử lý |
| **Error/Danger** | Đỏ `#DC2626` / `#EF4444` | Lỗi, thao tác nguy hiểm (xóa), số âm (giảm), validation lỗi |
| **Info** | Xanh dương nhạt `#0EA5E9` | Thông báo trung tính, tooltip hướng dẫn, số liệu trung lập |

**Quy tắc:**
- Mỗi màu ngữ nghĩa cũng cần 3 sắc độ tối thiểu: nền nhạt (background của alert/badge), màu chính (icon, border), màu đậm (text để đủ tương phản đọc được).
- KHÔNG dùng đỏ/xanh lá cho mục đích khác ngoài lỗi/thành công — vi phạm quy tắc này gây hiểu nhầm nghiêm trọng (VD: dùng đỏ làm màu thương hiệu chính sẽ khiến mọi CTA trông như "cảnh báo nguy hiểm").
- Cân nhắc người dùng mù màu đỏ-xanh (~8% nam giới): luôn kèm icon hoặc text, không chỉ dựa vào màu để truyền đạt trạng thái (VD: icon dấu tích cho success, icon chấm than cho error).

### 2.3 Màu trung tính (Neutral/Gray Scale)

Đây là bảng màu dùng nhiều nhất trong UI (nền, viền, text, chữ phụ) — cần 9-10 sắc độ xám mịn:

```
neutral-0    #FFFFFF   nền trắng tinh (card, modal trên nền xám)
neutral-50   #F9FAFB   nền trang (background chính của app)
neutral-100  #F3F4F6   nền section phụ, nền hover của item trong list
neutral-200  #E5E7EB   border mặc định, divider
neutral-300  #D1D5DB   border đậm hơn (input focus outline nhẹ), icon disabled
neutral-400  #9CA3AF   placeholder text, icon phụ
neutral-500  #6B7280   text phụ (secondary text)
neutral-600  #4B5563   text phụ đậm hơn
neutral-700  #374151   text nội dung chính (thay vì đen tuyệt đối)
neutral-800  #1F2937   heading, text nhấn mạnh
neutral-900  #111827   text đậm nhất, dùng hạn chế
```

**Quy tắc quan trọng:** KHÔNG dùng đen tuyệt đối `#000000` cho text trên nền trắng — gây tương phản quá gắt, mỏi mắt. Dùng `neutral-800/900` (xám rất đậm) thay thế. Tương tự, nền không nên dùng trắng tinh `#FFFFFF` cho toàn bộ app nếu có nhiều card — dùng `neutral-50` làm nền trang, `#FFFFFF` cho card nổi lên trên để tạo phân lớp.

### 2.4 Phân cấp màu Text (Text Color Hierarchy)

| Cấp | Màu (light mode) | Dùng cho | Tỷ lệ tương phản tối thiểu |
|-----|-------------------|----------|------------------------------|
| Primary text | `neutral-900` (#111827) | Tiêu đề, nội dung quan trọng nhất | 7:1 (AAA) |
| Secondary text | `neutral-700` (#374151) | Nội dung thường | 4.5:1 (AA) |
| Tertiary text | `neutral-500` (#6B7280) | Caption, helper text, timestamp | 4.5:1 (AA) — chỉ dùng cho text không quan trọng |
| Disabled text | `neutral-400` (#9CA3AF) | Text/control bị vô hiệu hóa | Không cần đạt AA (không phải nội dung tương tác) |
| Placeholder | `neutral-400` (#9CA3AF) | Placeholder trong input | Không cần đạt AA |
| Link/Interactive text | `primary-600` | Link có thể click | 4.5:1 (AA) |
| Text trên nền màu (Primary button) | Trắng `#FFFFFF` hoặc `neutral-900` tùy độ sáng nền | Text trong button, badge có màu nền | 4.5:1 (AA) |

**Quy tắc bắt buộc — Độ tương phản (Contrast — WCAG 2.1):**
- Text thường (< 18px hoặc < 14px bold): tối thiểu **4.5:1** so với nền (AA).
- Text lớn (≥ 18px hoặc ≥ 14px bold): tối thiểu **3:1** (AA).
- Text quan trọng/heading nên hướng tới **7:1** (AAA) khi có thể.
- Icon mang thông tin chức năng (không phải trang trí): tối thiểu **3:1**.
- Luôn kiểm tra contrast bằng công cụ (VD: WebAIM Contrast Checker) trước khi chốt màu.

### 2.5 Điều phối màu trong Control (Component Color States)

Mỗi control tương tác cần định nghĩa đủ **5 trạng thái màu**:

**Button chính (Primary Button):**
| State | Background | Text | Border |
|-------|-----------|------|--------|
| Default | `primary-500` | White | none |
| Hover | `primary-600` | White | none |
| Active/Pressed | `primary-700` | White | none |
| Focus | `primary-500` | White | outline `primary-300` 2px, offset 2px |
| Disabled | `neutral-200` | `neutral-400` | none |

**Button phụ (Secondary/Outline Button):**
| State | Background | Text | Border |
|-------|-----------|------|--------|
| Default | Transparent/White | `neutral-700` | `neutral-300` |
| Hover | `neutral-50` | `neutral-900` | `neutral-400` |
| Active | `neutral-100` | `neutral-900` | `neutral-400` |
| Disabled | Transparent | `neutral-400` | `neutral-200` |

**Input/Form field:**
| State | Background | Border | Text |
|-------|-----------|--------|------|
| Default | White | `neutral-300` | `neutral-900` |
| Hover | White | `neutral-400` | `neutral-900` |
| Focus | White | `primary-500` (2px) | `neutral-900` |
| Error | White/`error-50` | `error-500` | `neutral-900` |
| Disabled | `neutral-50` | `neutral-200` | `neutral-400` |
| Success (validated) | White | `success-500` | `neutral-900` |

**Quy tắc phối màu tổng quát:**
1. **Tỷ lệ 60-30-10**: 60% màu trung tính (nền, khoảng trắng), 30% màu phụ (card, section, text), 10% màu nhấn (Primary/CTA). Nếu màu chính xuất hiện quá nhiều, nó mất tác dụng nhấn mạnh.
2. Mỗi màn hình chỉ nên có **1 CTA chính** dùng màu Primary đậm nhất — các hành động phụ dùng outline/ghost button để không cạnh tranh thị giác.
3. Không dùng quá **3-4 màu có sắc độ (hue) khác nhau** trên 1 màn hình (không tính neutral) — Primary + 1 Accent (nếu có) + các màu Semantic khi cần.
4. Nền và text luôn lấy từ **cùng 1 thang màu neutral** để đảm bảo hài hòa (không trộn xám lạnh với xám ấm).
5. Dark mode không phải là "đảo ngược màu" đơn giản — cần bảng riêng: nền tối dùng `#0F1115`-`#1A1D23` (không dùng đen tuyệt đối), text dùng trắng ngả xám `#E5E7EB` (không dùng trắng tinh), độ bão hòa (saturation) của Primary nên giảm nhẹ để đỡ chói mắt trên nền tối.

---

## 3. CONTROLS (THÀNH PHẦN GIAO DIỆN)

### 3.1 Button

- **Phân cấp**: Primary (1 nút/màn hình) → Secondary (outline) → Tertiary/Ghost (text-only, không nền không viền) → Destructive (đỏ, cho hành động xóa/hủy nguy hiểm).
- Khoảng cách giữa icon và text trong button: `8px`.
- Icon trong button luôn đặt trước text (trừ trường hợp đặc biệt như "Tiếp theo →").
- Độ dài text button: ngắn gọn, dùng động từ hành động ("Lưu thay đổi" thay vì "OK", "Xóa vĩnh viễn" thay vì "Có").
- Loading state: disable button + hiện spinner thay icon, giữ nguyên kích thước button (không co giãn).
- Khoảng cách tối thiểu giữa 2 button cạnh nhau: `12px`.

### 3.2 Input / Form Field

- Cấu trúc chuẩn: `Label → Input → Helper text/Error message`.
- Label luôn ở trên input (không đặt bên trái trừ form rất ngắn) — dễ scan theo chiều dọc, tốt cho responsive.
- Label bắt buộc có dấu `*` màu đỏ hoặc chữ "(Bắt buộc)"; field không bắt buộc có thể ghi "(Tùy chọn)".
- Padding trong input: 12-16px ngang, đảm bảo chiều cao tối thiểu 40px (md).
- Placeholder KHÔNG được dùng thay label — placeholder biến mất khi gõ, gây mất ngữ cảnh cho người dùng (đặc biệt tai hại trên mobile/người lớn tuổi).
- Error message: hiện ngay dưới input, màu `error-600`, kèm icon cảnh báo, font-size 12-13px.
- Validate theo thời điểm hợp lý: validate on blur (khi rời field) cho lần đầu, validate on change (real-time) sau khi đã có lỗi — tránh báo lỗi ngay khi người dùng chưa gõ xong.

### 3.3 Checkbox / Radio / Switch

- Kích thước chuẩn: 16-20px (checkbox/radio), vùng chạm bao quanh tối thiểu 24×24px.
- Checkbox: dùng cho chọn nhiều lựa chọn hoặc 1 lựa chọn độc lập (đồng ý điều khoản).
- Radio: dùng cho chọn 1 trong nhiều lựa chọn loại trừ lẫn nhau (tối thiểu 2, tối đa ~5-6 lựa chọn; nhiều hơn nên dùng Select/Dropdown).
- Switch (toggle): dùng cho bật/tắt trạng thái có hiệu lực **ngay lập tức** (không cần nút Lưu) — VD: bật/tắt thông báo. Không dùng Switch cho lựa chọn cần nhấn Submit mới áp dụng (trường hợp đó dùng Checkbox).
- Switch ON dùng màu Primary/Success, OFF dùng `neutral-300`.

### 3.4 Dropdown / Select

- Chiều cao control bằng input chuẩn (40px).
- Icon chevron-down bên phải, xoay 180° khi mở.
- Danh sách dropdown: max-height ~280-320px rồi scroll, mỗi item cao tối thiểu 36-40px.
- Item đang chọn: highlight nền `primary-50`, có thể kèm icon check bên phải.
- Với danh sách > 10 mục: thêm ô tìm kiếm (searchable select).
- Multi-select: hiển thị lựa chọn đã chọn dạng tag/chip có thể xóa từng cái.

### 3.5 Modal / Dialog

- Overlay nền: đen mờ `rgba(0,0,0,0.4-0.5)`.
- Width chuẩn: `sm` 400px (xác nhận đơn giản), `md` 560px (form), `lg` 720-800px (nội dung phức tạp), `full` gần full-screen (mobile hoặc workflow nhiều bước).
- Cấu trúc: Header (title + nút đóng X) → Body (scroll nếu dài) → Footer (button, căn phải: Cancel bên trái/ghost, Action chính bên phải/primary).
- Modal xác nhận hành động nguy hiểm (xóa): nút Confirm dùng màu Destructive (đỏ), không dùng Primary xanh.
- Đóng bằng: nút X, phím ESC, click ra ngoài overlay (trừ modal quan trọng cần xác nhận rõ ràng thì tắt click-outside).

### 3.6 Table / Bảng dữ liệu

- Header row: nền `neutral-50`, text `neutral-700` weight 500-600, font-size 12-13px, thường viết HOA hoặc letter-spacing nhẹ.
- Row height: 40-48px (dense) hoặc 56-64px (comfortable) tùy mật độ dữ liệu cần thiết.
- Zebra-striping (màu xen kẽ dòng) tùy chọn — nếu dùng, chỉ nên khác biệt rất nhẹ (`neutral-50`).
- Hover row: nền `neutral-50` hoặc `primary-50` nhạt.
- Số liệu căn phải (right-align), text căn trái (left-align), trạng thái/action căn giữa hoặc trái tùy layout.
- Cột action (sửa/xóa) luôn ở cuối cùng bên phải, dùng icon-button nhỏ (xs/sm).
- Empty state: khi bảng không có dữ liệu, hiện illustration/icon + text hướng dẫn, không để trống trơn.

### 3.7 Navigation (Sidebar, Tabs, Breadcrumb)

- **Sidebar**: width chuẩn 220-280px (expanded), 64-72px (collapsed/icon-only). Item active: nền `primary-50`, text/icon `primary-600`, có thể thêm thanh chỉ báo dọc bên trái `primary-500` 3-4px.
- **Tabs**: underline indicator cho tab active (2-3px, màu Primary) hoặc pill-style nền màu. Khoảng cách giữa các tab: 24-32px.
- **Breadcrumb**: font-size 13px, màu `neutral-500`, mục cuối (trang hiện tại) màu `neutral-900` không phải link.
- Độ sâu điều hướng tối đa nên ≤ 3 cấp để tránh người dùng lạc lối.

### 3.8 Card

- Padding trong: 16-24px tùy kích thước card.
- Border-radius: nhất quán với hệ thống chung (8-12px thường dùng).
- Border 1px `neutral-200` + shadow nhẹ `elevation-1`, hoặc chỉ dùng 1 trong 2 (không lạm dụng cả border đậm lẫn shadow đậm cùng lúc — trông nặng nề).
- Khoảng cách giữa các card trong grid: 16-24px.

### 3.9 Badge / Tag / Status Indicator

- Kích thước nhỏ gọn: chiều cao 20-24px, padding ngang 8-10px, font-size 12px, border-radius full (pill) hoặc 4-6px.
- Luôn dùng cặp màu nền nhạt + text đậm cùng tông (VD: nền `success-50` + text `success-700`) — không dùng nền đậm + text trắng cho badge trạng thái (quá nổi, gây mỏi mắt khi nhiều badge cùng lúc trong bảng).
- Trạng thái hệ thống nên thống nhất màu xuyên suốt app: "Hoạt động/Thành công" luôn xanh lá, "Chờ xử lý" luôn vàng, "Lỗi/Ngừng" luôn đỏ, "Nháp/Không xác định" luôn xám.

### 3.10 Tooltip

- Nền đậm (thường `neutral-900` hoặc gần đen), text trắng, font-size 12-13px, padding 6-8px 10-12px, border-radius 4-6px.
- Delay hiện: 300-500ms sau khi hover (tránh hiện tooltip ngay khi rê chuột lướt qua).
- Chỉ dùng cho thông tin bổ sung ngắn gọn (1 dòng, tối đa ~2 dòng) — không nhồi nhét nội dung dài.

### 3.11 Icon

- Bộ icon nhất quán 1 style xuyên suốt (outline HOẶC filled, không trộn lẫn tùy tiện — có thể dùng filled cho state active/selected, outline cho default).
- Kích thước chuẩn: 16px (trong text/button nhỏ), 20px (mặc định trong control), 24px (nút icon độc lập, header).
- Stroke-width nhất quán (thường 1.5-2px cho outline icon).
- Icon chức năng (không trang trí) cần đảm bảo đủ tương phản (3:1) với nền.

---

## 4. BỐ CỤC & LAYOUT

### 4.1 Cấu trúc trang chuẩn (Admin/Dashboard)

```
┌─────────────────────────────────────────┐
│ Header (64px): Logo | Search | Notif | Avatar │
├──────────┬──────────────────────────────┤
│          │  Breadcrumb                   │
│ Sidebar  │  Page Title + Action buttons  │
│ 240-280px│  ─────────────────────────    │
│          │  Content Area (max 1440px,     │
│          │  padding 24-32px)              │
│          │                                │
└──────────┴──────────────────────────────┘
```

### 4.2 Nguyên tắc phân cấp thị giác (Visual Hierarchy)

1. **Kích thước**: phần tử quan trọng hơn → to hơn.
2. **Độ đậm màu/tương phản**: nội dung chính dùng màu đậm nhất, phụ dùng nhạt hơn.
3. **Khoảng trắng (white space)**: phần tử quan trọng cần nhiều không gian xung quanh hơn để "thở".
4. **Vị trí**: theo hướng đọc F-pattern (trái sang phải, trên xuống dưới với văn hóa Latin) — thông tin quan trọng đặt góc trên-trái hoặc dọc theo trục quét mắt tự nhiên.
5. Không quá 1-2 điểm nhấn (focal point) trên 1 màn hình — nhiều điểm nhấn cùng lúc = không điểm nhấn nào cả.

### 4.3 Responsive Behavior

- Mobile-first hoặc desktop-first tùy sản phẩm, nhưng luôn kiểm tra đủ 3 breakpoint chính.
- Sidebar → chuyển thành bottom navigation hoặc hamburger menu trên mobile.
- Table rộng nhiều cột → chuyển thành card list trên mobile (mỗi row = 1 card).
- Form nhiều cột (2-3 cột trên desktop) → luôn 1 cột trên mobile.
- Font-size không giảm dưới ngưỡng tối thiểu (14px body) khi thu nhỏ màn hình — chỉ điều chỉnh spacing/layout, không thu nhỏ chữ quá mức.

### 4.4 Empty State, Loading State, Error State

Mọi màn hình hiển thị dữ liệu động đều cần thiết kế đủ 4 trạng thái:
- **Loading**: skeleton screen (khung xám nhấp nháy theo hình dạng nội dung thật) — ưu tiên hơn spinner cho nội dung có cấu trúc rõ (list, card, table).
- **Empty**: illustration/icon nhẹ nhàng + text giải thích + CTA hướng dẫn hành động tiếp theo (VD: "Chưa có đơn hàng nào — Tạo đơn hàng đầu tiên").
- **Error**: icon cảnh báo + thông báo lỗi rõ ràng bằng ngôn ngữ người dùng hiểu được (không hiện mã lỗi kỹ thuật thô) + nút "Thử lại".
- **Success/Loaded**: trạng thái bình thường.

---

## 5. QUY TẮC RIÊNG THEO LĨNH VỰC

### 5.1 SaaS Dashboard / Admin Panel

- **Ưu tiên**: mật độ thông tin cao, hiệu quả thao tác, khả năng scan nhanh số liệu.
- Màu Primary nên là xanh dương/tím (tạo cảm giác tin cậy, chuyên nghiệp, công nghệ) — tránh màu quá sặc sỡ.
- Dùng bảng số liệu (data table) làm trung tâm; ưu tiên density "comfortable" (44-48px/row) cho thao tác thường xuyên, "compact" (36-40px) cho power-user.
- KPI card ở đầu dashboard: số liệu lớn (`display`/`h1` size), kèm mũi tên + % thay đổi (xanh tăng/đỏ giảm), so sánh với kỳ trước.
- Sidebar cố định, hỗ trợ thu gọn (collapse) để tối ưu không gian làm việc.
- Dùng biểu đồ (chart) nhất quán bộ màu: mỗi metric 1 màu cố định xuyên suốt toàn hệ thống (VD: Doanh thu luôn xanh dương, Chi phí luôn cam).
- Bàn phím shortcut (command palette `Cmd+K`) là điểm cộng lớn cho power-user.

### 5.2 E-commerce (Thương mại điện tử)

- **Ưu tiên**: hình ảnh sản phẩm nổi bật, CTA mua hàng rõ ràng, giảm ma sát trong luồng thanh toán (checkout).
- Màu Primary cho nút "Mua ngay"/"Thêm vào giỏ" nên nổi bật, tương phản cao với nền — thường đỏ, cam, hoặc màu thương hiệu riêng biệt hoàn toàn với màu trung tính xung quanh.
- Giá tiền: font đậm, size lớn hơn text thường (18-24px), giá gạch/giảm giá dùng màu đỏ hoặc cam kèm gạch ngang giá gốc màu xám.
- Ảnh sản phẩm: tỷ lệ khung hình nhất quán (1:1 vuông phổ biến nhất), nền trắng/xám nhạt đồng nhất toàn site.
- Card sản phẩm: Ảnh → Tên → Giá → Rating/Review count → CTA. Giữ khoảng cách đều, grid 2-4 cột tùy màn hình.
- Luồng checkout: tối giản số bước (lý tưởng ≤ 3 bước: Giỏ hàng → Thông tin giao hàng → Thanh toán), hiển thị progress indicator rõ ràng, KHÔNG chèn quảng cáo/upsell gây xao nhãng trong bước thanh toán cuối.
- Badge tin cậy (miễn phí ship, đổi trả, bảo hành) đặt gần nút mua hàng để giảm lo ngại.
- Wishlist/So sánh sản phẩm: icon rõ ràng, trạng thái đã lưu thể hiện bằng màu/fill icon khác biệt.

### 5.3 Fintech / Ngân hàng

- **Ưu tiên**: độ tin cậy, chính xác tuyệt đối của số liệu, bảo mật, giảm lo lắng của người dùng khi thao tác với tiền.
- Màu Primary: xanh dương đậm hoặc xanh navy (tâm lý học màu sắc: xanh dương = tin cậy, ổn định) — tránh màu quá trẻ trung/sặc sỡ trừ khi target Gen Z (fintech neobank có thể dùng màu tươi hơn: tím, xanh mint).
- Số dương (tiền vào, lãi) LUÔN xanh lá; số âm (tiền ra, lỗ) LUÔN đỏ — tuyệt đối nhất quán trong toàn bộ app, không ngoại lệ.
- Số tiền: dùng font có chữ số rõ ràng (tabular figures/mono cho bảng số liệu để các cột thẳng hàng), luôn hiển thị đơn vị tiền tệ rõ ràng, phân cách hàng nghìn đúng chuẩn địa phương.
- Mọi giao dịch quan trọng (chuyển tiền, thanh toán) cần màn hình xác nhận (confirmation step) hiển thị đầy đủ: số tiền, người nhận, phí giao dịch — trước khi có nút xác nhận cuối cùng.
- Nút xác nhận giao dịch tiền: không nên dùng màu xanh lá dễ nhầm với "thành công" khi CHƯA thực hiện — dùng Primary color, chỉ chuyển xanh lá SAU khi giao dịch hoàn tất.
- Bắt buộc có trạng thái xác thực 2 lớp (OTP, sinh trắc học) trước hành động nhạy cảm — thiết kế màn hình nhập OTP rõ ràng, đếm ngược thời gian hết hạn.
- Biểu đồ tài chính (biến động số dư, chi tiêu): dùng gradient nhẹ dưới đường line để dễ đọc xu hướng, tooltip hiện chính xác số liệu khi hover.
- Ẩn số dư mặc định (icon con mắt để toggle hiện/ẩn) — tôn trọng quyền riêng tư khi dùng nơi công cộng.

### 5.4 Y tế / Sức khỏe (Healthcare)

- **Ưu tiên**: rõ ràng tuyệt đối (tránh mọi mơ hồ có thể ảnh hưởng an toàn), thân thiện giảm lo âu, khả năng tiếp cận cao (accessibility) cho người lớn tuổi/khuyết tật.
- Màu Primary: xanh dương nhạt hoặc xanh lá mint (gợi cảm giác sạch sẽ, yên tâm, y tế) — tránh đỏ làm màu chủ đạo (đỏ gắn với khẩn cấp/nguy hiểm, chỉ dùng đúng ngữ cảnh cảnh báo).
- Font-size tối thiểu nên cao hơn chuẩn thông thường (body 16px thay vì 14px) để phục vụ người dùng lớn tuổi.
- Độ tương phản màu nên đạt AAA (7:1) thay vì chỉ AA — đối tượng người dùng có thể có thị lực kém.
- Thông tin quan trọng (chỉ số sức khỏe bất thường, cảnh báo thuốc) cần nổi bật rõ ràng bằng màu + icon + text, không chỉ dựa vào 1 tín hiệu.
- Form nhập liệu y tế (triệu chứng, tiền sử bệnh): chia nhỏ thành nhiều bước ngắn (progressive disclosure) thay vì 1 form dài gây choáng ngợp; luôn có khả năng lưu nháp.
- Thuật ngữ y khoa cần đi kèm giải thích đơn giản (tooltip/expandable text) cho người dùng không chuyên.
- Lịch hẹn/nhắc nhở: thiết kế calendar/reminder rõ ràng, dễ thao tác, thông báo nhắc nhở nổi bật nhưng không gây hoảng loạn.
- Tuân thủ quy định bảo mật dữ liệu y tế (HIPAA/tương đương): ẩn thông tin nhạy cảm mặc định trên màn hình dùng chung, có cảnh báo rõ trước khi chia sẻ dữ liệu.

### 5.5 Phần mềm Kinh doanh / Quản lý (ERP, CRM, Quản lý dự án)

- **Ưu tiên**: xử lý khối lượng dữ liệu lớn hiệu quả, workflow phức tạp nhiều bước, khả năng tùy biến cao (customizable views).
- Màu Primary: trung tính, chuyên nghiệp (xanh dương, xám xanh, tím đậm) — sản phẩm B2B ưu tiên độ tin cậy hơn là bắt mắt.
- Dữ liệu dạng bảng là trung tâm: hỗ trợ sort, filter, group, resize cột, ẩn/hiện cột, export — toolbar thao tác đặt cố định trên đầu bảng.
- Kanban board (quản lý dự án/CRM pipeline): cột có màu định danh nhẹ ở header, card kéo-thả (drag-drop) cần chỉ báo trực quan rõ (shadow nổi khi kéo, placeholder vị trí thả).
- Form nhập liệu phức tạp (tạo đơn hàng, hợp đồng): chia theo tab hoặc section có thể collapse, auto-save để tránh mất dữ liệu khi làm việc lâu.
- Phân quyền vai trò (role-based access): UI cần ẩn/khóa rõ ràng các control mà user không có quyền, không chỉ disable mờ mờ gây khó hiểu — nên có tooltip giải thích "Bạn cần quyền X để thực hiện".
- Bảng điều khiển tùy biến (customizable dashboard/widget): cho phép kéo-thả sắp xếp lại, resize widget, lưu layout theo từng user.
- Thông báo hệ thống (notification center): phân loại rõ theo mức độ ưu tiên, có thể đánh dấu đã đọc/lưu trữ, badge số lượng chưa đọc trên icon chuông.

### 5.6 Phần mềm Trình duyệt (Browser)

- **Ưu tiên**: tối giản tối đa để nhường không gian cho nội dung web, hiệu năng cảm nhận (perceived performance) nhanh, thao tác bằng phím tắt.
- UI chrome (thanh công cụ, tab bar) nên tối giản, màu trung tính (xám/trắng hoặc theo theme OS), không cạnh tranh thị giác với nội dung trang web đang xem.
- Tab: kích thước co giãn theo số lượng tab mở, tab active cần phân biệt rõ (nền sáng hơn/đậm hơn tab khác + đường viền trên nếu cần), icon loading dạng spinner nhỏ mượt mà, favicon rõ nét dù thu nhỏ (16×16px).
- Thanh địa chỉ (address bar): rộng rãi, dễ bấm, có icon bảo mật (khóa/cảnh báo) rõ ràng bên trái, autocomplete gợi ý mượt không giật.
- Toolbar icon (back/forward/reload/bookmark): kích thước 20-24px, trạng thái disabled rõ ràng (VD: nút back mờ khi không có lịch sử).
- Context menu (chuột phải) và menu cài đặt: phân nhóm logic rõ ràng, có phím tắt hiển thị bên phải mỗi mục.
- Dark mode gần như bắt buộc — người dùng trình duyệt dùng nhiều giờ liên tục, cần giảm mỏi mắt.
- Trạng thái "đang tải trang" cần chỉ báo rõ nhưng không chặn thao tác khác (progress bar mảnh trên đầu, không phải overlay chặn toàn màn hình).

### 5.7 Phần mềm Antivirus / Bảo mật

- **Ưu tiên**: truyền đạt trạng thái an toàn/nguy hiểm CỰC KỲ rõ ràng và tức thời, xây dựng cảm giác an tâm, không gây hoảng loạn thái quá.
- Màu trạng thái tổng quan (dashboard chính) là yếu tố quan trọng nhất:
  - **An toàn**: xanh lá tràn ngập màn hình chính (nền hoặc icon khiên lớn), kèm text "Thiết bị của bạn được bảo vệ".
  - **Cảnh báo nhẹ** (cần cập nhật, quét định kỳ): vàng/cam, không quá gây lo lắng.
  - **Nguy hiểm** (phát hiện mối đe dọa): đỏ rõ ràng, kèm hành động khắc phục ngay lập tức (nút "Xử lý ngay" nổi bật).
- Icon khiên (shield) là biểu tượng phổ quát cho bảo mật — dùng nhất quán, đổi màu theo trạng thái (xanh/vàng/đỏ) thay vì đổi hình dạng.
- Kết quả quét virus: hiển thị tiến trình quét real-time (số file đã quét, thời gian ước tính còn lại), danh sách mối đe dọa phát hiện được liệt kê rõ ràng kèm mức độ nghiêm trọng (Thấp/Trung bình/Cao/Nghiêm trọng) và hành động đề xuất (Xóa/Cách ly/Bỏ qua).
- Thông báo pop-up cảnh báo mối đe dọa: cần nổi bật, có thể dùng animation nhẹ thu hút chú ý (không lạm dụng gây khó chịu), nhưng luôn có nút hành động rõ ràng, tránh dark pattern gây hoang mang để bán thêm gói dịch vụ.
- Log lịch sử quét/chặn: dạng timeline hoặc bảng, mỗi entry có timestamp, loại mối đe dọa, hành động đã thực hiện.
- Cài đặt bảo vệ nâng cao (firewall, quét theo lịch): dùng switch rõ ràng, nhóm theo mức độ kỹ thuật (Cơ bản/Nâng cao) để không làm người dùng phổ thông choáng ngợp.
- Tránh thiết kế gây "cảnh báo giả" (fake urgency) — đây là dark pattern phổ biến trong ngành antivirus miễn phí, làm giảm uy tín sản phẩm nghiêm túc.

### 5.8 Công cụ Thiết kế chung (Design Tools / Creative Software)

- **Ưu tiên**: tối đa hóa không gian canvas làm việc, công cụ (toolbar) dễ tiếp cận nhưng không chiếm diện tích, hỗ trợ workflow sáng tạo linh hoạt.
- Giao diện tối (dark theme) thường là mặc định — giúp màu sắc/nội dung thiết kế của người dùng nổi bật mà không bị ảnh hưởng bởi nền UI sáng.
- Toolbar công cụ: icon-only compact (20-24px), tooltip hiện tên công cụ + phím tắt khi hover, nhóm công cụ liên quan gần nhau, có thể thu gọn/mở rộng.
- Panel thuộc tính (properties panel): đặt cố định 1 bên (thường phải), width 240-320px, cập nhật real-time theo đối tượng đang chọn trên canvas.
- Layers panel: cấu trúc cây (tree) rõ ràng, hỗ trợ kéo-thả sắp xếp, icon ẩn/hiện + khóa layer luôn hiện sẵn (không ẩn trong hover) để thao tác nhanh.
- Canvas: nền trung tính (xám đậm phổ biến nhất) để không ảnh hưởng nhận diện màu sắc thật của thiết kế đang làm.
- Zoom control: luôn hiển thị % zoom hiện tại, dễ dàng reset về 100%/fit-to-screen bằng phím tắt.
- Color picker: hỗ trợ đầy đủ định dạng (HEX/RGB/HSL), có eyedropper (hút màu), lưu color palette/swatches của dự án.
- Undo/Redo: cực kỳ quan trọng trong công cụ sáng tạo — cần đáng tin cậy 100%, có thể xem lịch sử thao tác (history panel) cho phần mềm chuyên nghiệp.
- Context-sensitive toolbar: công cụ hiển thị thay đổi tùy theo đối tượng đang chọn (text/hình/ảnh) — giảm rối mắt bằng cách chỉ hiện option liên quan.

### 5.9 Game UI/HUD

- **Ưu tiên**: nhập vai vào không khí game (immersion), phản hồi tức thời (feedback loop), không che khuất gameplay.
- HUD (thanh máu, mana, minimap) đặt ở rìa màn hình, bán trong suốt khi không tương tác, đậm nét khi có sự kiện quan trọng (thấp máu → nhấp nháy đỏ).
- Font chữ và style UI theo đúng art direction của game (fantasy dùng font serif góc cạnh, sci-fi dùng font mono/futuristic) — đây là lĩnh vực DUY NHẤT được phép phá vỡ quy tắc "font hệ thống trung tính".
- Nút bấm trong menu game: cần trạng thái "focused" rõ ràng cho điều khiển bằng tay cầm/bàn phím (không chỉ hover chuột) — viền sáng hoặc scale phóng to khi được chọn bằng D-pad.
- Thông báo trong game (nhặt vật phẩm, lên cấp): xuất hiện ngắn gọn, tự động biến mất, không chặn thao tác (non-blocking toast).
- Inventory/Skill tree: dùng grid rõ ràng, icon vật phẩm cần độ nhận diện cao dù thu nhỏ, rarity (độ hiếm) thể hiện qua màu viền chuẩn ngành (trắng=thường, xanh lá=hiếm, tím=sử thi, cam/vàng=huyền thoại).
- Màn hình pause/settings: tương phản với gameplay (thường làm mờ nền game phía sau + overlay tối) để phân biệt rõ "đang ở UI" vs "đang chơi".

### 5.10 Streaming/Media (Xem phim, nghe nhạc)

- **Ưu tiên**: nội dung (poster, video) là trung tâm tuyệt đối, UI phải "biến mất" khi không cần thiết.
- Nền tối gần như bắt buộc (đen/xám rất đậm) để poster/thumbnail nổi bật và giả lập không khí rạp chiếu phim.
- Thumbnail/Poster: tỷ lệ khung hình chuẩn ngành (16:9 cho video, 2:3 cho poster phim), hover phóng to nhẹ kèm autoplay preview sau 1-2 giây.
- Thanh điều khiển video (play/pause/volume/seek): tự ẩn sau 2-3 giây không tương tác, hiện lại ngay khi di chuột/chạm màn hình.
- Thanh tiến trình (seek bar): vùng chạm lớn hơn đường kẻ hiển thị (dễ kéo chính xác), hiện preview thumbnail khi hover tại vị trí tua.
- Danh sách phát nhạc (playlist): item đang phát có chỉ báo động (equalizer bar nhấp nháy hoặc icon sóng âm) thay vì chỉ highlight tĩnh.
- Continue watching/Gợi ý: carousel ngang cuộn mượt, mỗi card hiện % đã xem bằng thanh progress mỏng ở đáy thumbnail.

### 5.11 Mạng xã hội (Social Feed, Chat, Story)

- **Ưu tiên**: luồng nội dung (feed) mượt mà không gián đoạn, cân bằng giữa nội dung và UI, khuyến khích tương tác tự nhiên.
- Feed dạng card: khoảng cách vừa đủ để phân biệt bài viết nhưng không quá rời rạc (thường liền khối, chỉ ngăn bằng divider mỏng hoặc khoảng trắng nhỏ).
- Nút tương tác (like/comment/share): icon 20-24px, có animation nhỏ khi bấm (like phóng to nảy nhẹ) để tạo cảm giác phản hồi thỏa mãn (delight).
- Avatar: luôn bo tròn hoàn toàn (circle), kích thước nhất quán theo ngữ cảnh (feed 40px, comment 32px, story ring 56-64px).
- Story/Status: viền tròn gradient (thường cam-hồng-tím) khi chưa xem, viền xám khi đã xem — chuẩn ngành phổ biến, không nên tự sáng tạo khác biệt gây khó hiểu.
- Story progress bar: nhiều đoạn ngang mảnh ở đầu màn hình, mỗi đoạn = 1 story, chạy tự động theo thời gian.
- Số lượng like/comment: rút gọn hiển thị (1.2K thay vì 1,234) khi vượt ngưỡng.
- Notification badge: số đỏ tròn góc trên-phải icon, ẩn khi = 0, hiện "99+" khi vượt ngưỡng thay vì số dài.

### 5.12 E-learning / LMS (Nền tảng học trực tuyến)

- **Ưu tiên**: cảm giác tiến bộ (progress) rõ ràng, giảm cảm giác choáng ngợp, khuyến khích hoàn thành khóa học.
- Progress bar là control trung tâm: hiện ở cấp khóa học, cấp chương, cấp bài học — luôn dùng màu Primary/Success nhất quán, có % hoặc số bài đã hoàn thành/tổng.
- Trạng thái bài học: chưa mở khóa (khóa/mờ) → đang học (highlight) → hoàn thành (checkmark xanh lá) — 3 trạng thái này cần phân biệt cực rõ bằng icon, không chỉ màu.
- Video bài giảng: kèm danh sách chương (chapter markers) trên thanh tua, tốc độ phát tùy chỉnh (0.5x-2x) luôn hiển thị dễ thấy.
- Quiz/Bài tập: đáp án đúng luôn phản hồi xanh lá + icon check, sai phản hồi đỏ + icon X, kèm giải thích ngay lập tức (không chỉ báo đúng/sai suông).
- Chứng chỉ/Badge hoàn thành: thiết kế trang trọng, có thể chia sẻ — đây là yếu tố tạo động lực (gamification) quan trọng.
- Dashboard học viên: hiện streak (chuỗi ngày học liên tục), tổng thời gian học, khóa học đang dở — dùng để khuyến khích quay lại.

### 5.13 Ghi chú / Productivity (Notion-style)

- **Ưu tiên**: tốc độ nhập liệu (không có độ trễ cảm nhận được), tính linh hoạt (block-based), giao diện "biến mất" để nhường chỗ cho nội dung người dùng tạo ra.
- Editor: tối giản triệt để — không viền, không nền khác biệt, giống trang giấy trắng; toolbar định dạng chỉ hiện khi bôi đen text (floating toolbar) hoặc gõ lệnh (`/command`).
- Block-based content: mỗi khối (heading, list, table, image...) có handle kéo-thả (⋮⋮) hiện khi hover ở lề trái, cho phép sắp xếp lại tự do.
- Sidebar cây thư mục (page tree): thu gọn/mở rộng bằng click mũi tên nhỏ, hỗ trợ kéo-thả di chuyển trang, indent rõ ràng theo cấp độ (16-20px/cấp).
- Auto-save liên tục, hiện trạng thái lưu rất nhỏ và kín đáo (VD: "Đã lưu lúc 10:32") — không làm gián đoạn luồng viết bằng thông báo nổi bật.
- Checkbox/to-do trong nội dung: click trực tiếp để tích, text tự động gạch ngang + mờ đi khi hoàn thành.
- Chế độ tối là gần như bắt buộc (người dùng làm việc nhiều giờ), font chữ cho phần nội dung nên cho phép tùy chỉnh (serif/sans-serif) theo sở thích đọc.

### 5.14 Email Client

- **Ưu tiên**: xử lý khối lượng lớn hiệu quả (triage nhanh), phân biệt rõ đã đọc/chưa đọc, độ tin cậy khi soạn/gửi.
- Danh sách email: email chưa đọc in đậm (font-weight 600) + có thể có chấm tròn nhỏ màu Primary bên trái; email đã đọc dùng weight thường, màu nhạt hơn (`neutral-600`).
- Mỗi dòng email hiện: Người gửi (đậm) → Tiêu đề → Snippet nội dung (màu nhạt, cắt bớt "...") → Timestamp (góc phải, format tương đối: "2 giờ trước", "Hôm qua", ngày cụ thể nếu cũ hơn 1 tuần).
- Hành động nhanh khi hover/swipe (mobile): Archive, Delete, Mark as read — icon rõ ràng, dùng màu semantic đúng ngữ cảnh (Delete = đỏ khi swipe).
- Đính kèm file: icon loại file (PDF/Excel/hình ảnh) dễ nhận diện, hiện dung lượng file.
- Compose window: có thể thu nhỏ (minimize) như cửa sổ chat để tiếp tục đọc email khác trong lúc soạn — pattern phổ biến của Gmail.
- Label/Folder màu: cho phép gán màu tùy chỉnh cho từng nhãn để phân loại trực quan bằng màu sắc.
- Spam/quan trọng: dùng icon sao (star) hoặc cờ (flag) màu vàng cho email quan trọng, tách biệt hoàn toàn với hệ thống màu semantic success/error.

### 5.15 Calendar / Lịch hẹn

- **Ưu tiên**: nhận diện nhanh mật độ lịch trình, tránh chồng chéo (conflict) sự kiện, dễ tạo/sửa sự kiện.
- Sự kiện (event block): mỗi loại lịch/calendar riêng có 1 màu cố định (Công việc = xanh dương, Cá nhân = xanh lá, Sinh nhật = hồng...), người dùng có thể tùy biến nhưng cần nhất quán trong phiên sử dụng.
- Event chồng giờ nhau: tự động chia cột song song trong cùng khung giờ, mỗi event đủ rộng để đọc tiêu đề tối thiểu.
- Chế độ xem: Ngày/Tuần/Tháng/Lịch trình (Agenda) — chuyển đổi nhanh bằng tab hoặc segmented control ở góc trên.
- "Hôm nay" luôn có chỉ báo nổi bật riêng (đường viền màu Primary quanh ô ngày, hoặc nền nhạt khác biệt) để định vị nhanh.
- Time indicator (đường kẻ ngang thể hiện giờ hiện tại) trong view Ngày/Tuần: màu đỏ mảnh, cập nhật real-time.
- Tạo sự kiện nhanh: click-drag trực tiếp trên lưới giờ để chọn khung thời gian, popup nhỏ hiện ngay để nhập tiêu đề mà không cần mở form đầy đủ.
- Nhắc nhở (reminder): icon chuông nhỏ trên event có đặt nhắc nhở, màu sắc nhất quán với hệ thống notification.

### 5.16 Developer Tools (IDE, Terminal, API Docs)

- **Ưu tiên**: hiệu năng, mật độ thông tin cao, hỗ trợ bàn phím tối đa, độ chính xác của monospace text.
- Font bắt buộc dùng **monospace** cho mọi vùng code/log (Fira Code, JetBrains Mono, Cascadia Code...) — không dùng font UI thường cho code dù chỉ 1 dòng.
- Syntax highlighting: bảng màu nhất quán theo convention ngành (keyword, string, comment, function... mỗi loại 1 màu cố định), đảm bảo đủ tương phản trên cả light/dark theme.
- Dark theme thường là mặc định (giảm mỏi mắt khi coding nhiều giờ, làm nổi bật syntax highlighting).
- Terminal/Console: nền đen/xám rất đậm, text trắng/xanh lá theo truyền thống terminal, hỗ trợ ANSI color codes đầy đủ.
- Line numbers: màu nhạt (`neutral-500`), căn phải, không được cho phép select cùng với code text khi copy.
- Error/Warning trong code: gạch chân lượn sóng đỏ (error) hoặc vàng (warning) dưới đoạn code lỗi, kèm icon ở lề trái (gutter).
- API documentation: code block có nút "Copy" hiện khi hover, syntax highlighting theo ngôn ngữ, có thể toggle giữa nhiều ngôn ngữ (cURL/Python/JS...) bằng tab.
- Command palette (`Cmd/Ctrl+Shift+P`) và phím tắt là trung tâm trải nghiệm — mọi action quan trọng cần có keyboard shortcut hiển thị rõ trong menu.

### 5.17 Chat / Nhắn tin (Messenger-style)

- **Ưu tiên**: cảm giác trò chuyện tự nhiên real-time, phân biệt rõ tin mình gửi/nhận, độ trễ cảm nhận = 0.
- Bong bóng chat (message bubble): tin của mình căn phải + màu Primary/đậm; tin của người khác căn trái + màu xám nhạt (`neutral-100`) — quy ước gần như toàn cầu, không nên đảo ngược.
- Border-radius bong bóng: bo tròn nhiều (16-18px) trừ góc gần avatar/liền tin trước đó (bo ít hơn, 4-6px) để thể hiện các tin liên tiếp thuộc cùng 1 "lượt nói".
- Trạng thái tin nhắn: Đang gửi (icon đồng hồ mờ) → Đã gửi (1 dấu tick) → Đã nhận (2 dấu tick) → Đã xem (2 dấu tick màu xanh dương) — chuẩn ngành phổ biến.
- Typing indicator: 3 chấm nhấp nháy trong bong bóng xám, xuất hiện/biến mất mượt mà.
- Trạng thái online: chấm tròn xanh lá nhỏ ở góc avatar (online), xám (offline), hoặc "Vừa truy cập X phút trước".
- Timestamp: ẩn mặc định, chỉ hiện khi hover hoặc tap vào tin nhắn — giữ giao diện gọn gàng.
- Emoji reaction: popup nhỏ khi long-press/hover tin nhắn, hiện dưới bong bóng dạng pill nhỏ kèm số lượng.

### 5.18 Video Call / Họp trực tuyến

- **Ưu tiên**: video là trung tâm tuyệt đối, control ít gây xao nhãng, độ tin cậy khi mạng yếu.
- Layout: Grid (nhiều người bằng nhau) hoặc Speaker view (người đang nói phóng to) — chuyển đổi dễ dàng, người đang nói luôn có viền sáng (Primary hoặc xanh lá) quanh khung hình.
- Thanh điều khiển (mic/cam/share/leave): đặt cố định dưới cùng, nền tối bán trong suốt, tự ẩn sau vài giây không tương tác khi đang gọi (giống media player).
- Nút "Rời cuộc gọi": LUÔN màu đỏ, tách biệt vị trí với các nút khác để tránh bấm nhầm.
- Trạng thái mic tắt: icon mic gạch chéo đỏ hiện đè lên avatar/video của người đó — chỉ báo cần rõ ràng dù ở view thu nhỏ.
- Chỉ báo chất lượng mạng: icon sóng (như sóng điện thoại) góc màn hình mỗi người, chuyển màu vàng/đỏ khi kém.
- Waiting room/Lobby: màn hình chờ rõ ràng trước khi vào phòng chính, host cần thấy danh sách người chờ để admit.
- Chia sẻ màn hình: viền màu nổi bật bao quanh toàn khung hình để nhắc người dùng biết đang share, kèm nút "Dừng chia sẻ" luôn hiện sẵn không cần hover tìm.

### 5.19 Giao đồ ăn / Giao hàng (Food Delivery)

- **Ưu tiên**: quyết định nhanh (ảnh món ăn hấp dẫn), theo dõi đơn hàng real-time tạo an tâm, tối giản bước đặt hàng.
- Ảnh món ăn: chất lượng cao, tỷ lệ nhất quán (thường 4:3 hoặc 1:1), chiếm diện tích lớn trong card vì là yếu tố quyết định mua hàng số 1.
- Card món ăn: Ảnh → Tên món → Mô tả ngắn → Giá → nút "+" thêm vào giỏ (nổi bật, thường tròn màu Primary góc dưới-phải ảnh).
- Giỏ hàng: thanh nổi cố định dưới cùng màn hình (sticky bar) hiện số lượng món + tổng tiền + nút "Xem giỏ hàng", luôn hiển thị khi có ít nhất 1 món.
- Theo dõi đơn hàng (order tracking): timeline dạng step rõ ràng (Đã nhận đơn → Đang chuẩn bị → Đang giao → Đã giao), kèm bản đồ real-time vị trí shipper nếu có, ETA (thời gian dự kiến) luôn hiện nổi bật ở đầu màn hình.
- Trạng thái đơn hàng dùng icon + màu tiến trình (Primary cho đang xử lý, Success xanh lá khi hoàn tất).
- Đánh giá quán ăn: sao vàng (★) chuẩn ngành, kèm số lượng review, thời gian giao trung bình hiển thị gần tên quán để hỗ trợ quyết định.

### 5.20 Du lịch / Đặt vé (Travel Booking)

- **Ưu tiên**: so sánh nhanh nhiều lựa chọn (giá, giờ, hãng), cảm giác tin cậy khi thanh toán số tiền lớn, hình ảnh điểm đến truyền cảm hứng.
- Kết quả tìm kiếm (chuyến bay/khách sạn): dạng list card có thể sort/filter mạnh (giá, thời gian, đánh giá, hãng) — filter sidebar/bottomsheet là control trung tâm.
- Card chuyến bay: hiện rõ giờ đi-đến, thời gian bay, số điểm dừng, hãng bay (logo), giá — giá luôn là yếu tố nổi bật nhất (font lớn, đậm, thường bên phải).
- Card khách sạn: ảnh lớn dạng carousel, tên, vị trí (khoảng cách đến trung tâm), rating sao + số review, giá/đêm nổi bật.
- Bộ lọc giá dạng range slider trực quan, kèm biểu đồ mini hiển thị phân bố giá theo khoảng.
- Luồng đặt vé: progress step rõ ràng (Chọn chuyến → Thông tin khách → Thanh toán → Xác nhận), thể hiện % hoàn thành.
- Trang xác nhận đặt chỗ: thiết kế trang trọng như "vé điện tử" thật, có mã đặt chỗ/QR code dễ tìm, nút tải về/thêm vào ví.
- Bản đồ chọn vị trí (chỗ ngồi máy bay, phòng khách sạn): sơ đồ trực quan, màu phân biệt rõ còn trống/đã đặt/đang chọn.

### 5.21 Bất động sản (Real Estate)

- **Ưu tiên**: hình ảnh/không gian là yếu tố quyết định, bản đồ tương tác mạnh, so sánh nhiều tiêu chí (giá/diện tích/vị trí).
- Card bất động sản: ảnh lớn (carousel nhiều ảnh + đếm số ảnh "1/12"), Giá (font lớn, đậm nhất trong card), Diện tích/Số phòng ngủ-tắm (dùng icon + số, căn hàng ngang gọn gàng), Địa chỉ/khu vực.
- Bản đồ tích hợp: pin giá tiền hiển thị trực tiếp trên bản đồ (không chỉ chấm tròn), cụm pin (cluster) khi zoom out nhiều bất động sản gần nhau.
- Bộ lọc: Giá (range slider), Diện tích, Số phòng, Loại hình (căn hộ/nhà phố/đất) — dạng chip có thể chọn nhanh, hiện số lượng kết quả cập nhật real-time khi lọc.
- Trang chi tiết: gallery ảnh full-width đầu trang, thông tin chia section rõ ràng (Tổng quan → Tiện ích → Vị trí/Bản đồ → Liên hệ), nút "Liên hệ/Đặt lịch xem nhà" luôn sticky/nổi bật.
- So sánh nhiều bất động sản: bảng so sánh song song các tiêu chí giống comparison table.
- Yêu thích (save): icon trái tim, trạng thái đã lưu đổi màu/fill rõ ràng, đồng bộ ngay lập tức.

### 5.22 Hẹn hò (Dating App)

- **Ưu tiên**: quyết định tức thời dựa trên ảnh (swipe), giảm ma sát tương tác, cân bằng vui vẻ và an toàn/riêng tư.
- Card profile: ảnh full-card chiếm gần như toàn bộ diện tích, thông tin cơ bản (Tên, Tuổi) overlay ở đáy ảnh với gradient tối dần để chữ trắng luôn đọc được.
- Swipe gesture: vuốt phải (thích) = xanh lá/hồng, vuốt trái (bỏ qua) = xám/đỏ — hiện overlay chữ "LIKE"/"NOPE" xoay nhẹ theo hướng kéo để phản hồi tức thời.
- Match animation: khi 2 người match nhau, hiệu ứng nổi bật đặc biệt (confetti, 2 ảnh đại diện chạm nhau) — đây là khoảnh khắc "delight" quan trọng nhất của app, cần đầu tư thiết kế animation.
- Nút hành động thay thế swipe (dislike/like/superlike): đặt dưới card, tròn, kích thước lớn (56-64px) dễ bấm bằng ngón cái 1 tay.
- Verification badge: icon xác thực (thường dấu tích xanh dương) cạnh tên, tăng độ tin cậy — vị trí và style cần rất rõ ràng vì liên quan an toàn người dùng.
- Report/Block: luôn dễ tìm (không giấu sâu trong menu) — ưu tiên an toàn người dùng hơn mọi yếu tố thẩm mỹ khác.

### 5.23 Fitness / Sức khỏe cá nhân (Theo dõi tập luyện)

- **Ưu tiên**: động lực (motivation) qua trực quan hóa tiến trình, cảm giác thành tựu, dữ liệu dễ hiểu không cần kiến thức y khoa.
- Vòng tròn tiến trình (progress ring/circle): pattern chuẩn ngành cho mục tiêu hàng ngày (bước chân, calo, phút vận động) — điền đầy dần theo % hoàn thành, màu rực rỡ (thường đỏ/xanh lá/xanh dương cho 3 chỉ số khác nhau kiểu Apple Watch).
- Biểu đồ xu hướng (cân nặng, nhịp tim theo thời gian): line chart mượt, có thể xem theo Tuần/Tháng/Năm.
- Đạt mục tiêu: animation ăn mừng ngắn (confetti, rung nhẹ), huy hiệu (achievement badge) sưu tập được.
- Nhật ký tập luyện: mỗi buổi tập hiện dạng card tóm tắt (loại bài tập, thời gian, calo tiêu thụ) kèm icon minh họa loại vận động.
- Nhắc nhở/Streak: chuỗi ngày liên tục đạt mục tiêu hiển thị nổi bật (thường kèm icon lửa 🔥) để tạo động lực duy trì thói quen.
- Số liệu sức khỏe nhạy cảm (cân nặng, BMI): cho phép ẩn/hiện, không mặc định phô ra màn hình chính nếu người dùng nhạy cảm về vấn đề này.

### 5.24 Bảo hiểm (Insurance)

- **Ưu tiên**: đơn giản hóa nội dung vốn phức tạp (điều khoản, hợp đồng), xây dựng tin cậy, giảm lo lắng khi claim bồi thường.
- Màu Primary: xanh dương hoặc xanh lá đậm (an tâm, bảo vệ) — tương tự Fintech nhưng cần cảm giác "che chở" nhiều hơn "công nghệ".
- So sánh gói bảo hiểm: bảng so sánh song song rõ ràng từng quyền lợi (comparison table), dùng icon check/x thay vì chỉ text để scan nhanh.
- Thuật ngữ bảo hiểm (miễn thường, đồng chi trả...): luôn kèm tooltip/icon (?) giải thích bằng ngôn ngữ đơn giản.
- Quy trình claim bồi thường: step-by-step rõ ràng như checkout, hiện trạng thái xử lý hồ sơ (Đã nộp → Đang xét duyệt → Đã duyệt/Từ chối) dạng timeline, kèm thời gian dự kiến xử lý.
- Upload chứng từ: khu vực kéo-thả rõ ràng, hiện preview file đã upload, checklist các giấy tờ cần thiết còn thiếu.
- Hợp đồng/Điều khoản: tóm tắt "Những điều cần biết" (key facts) ngắn gọn trước khi dẫn tới văn bản pháp lý đầy đủ dài.

### 5.25 Logistics / Vận chuyển (Theo dõi đơn hàng, Vận tải)

- **Ưu tiên**: hiển thị vị trí/trạng thái real-time cực rõ ràng, xử lý dữ liệu quy mô lớn (nhiều đơn/xe cùng lúc) hiệu quả.
- Theo dõi đơn hàng: timeline dọc chuẩn ngành (chấm tròn nối bằng đường kẻ), mỗi mốc có icon + timestamp + địa điểm, mốc hiện tại nổi bật (màu Primary, có thể có animation pulse nhẹ).
- Bản đồ real-time: icon xe/shipper di chuyển mượt trên bản đồ, đường đi (route) vẽ rõ, ETA cập nhật liên tục ở vị trí dễ thấy.
- Dashboard điều phối (cho vận hành nội bộ): bảng danh sách đơn/xe mật độ cao, mã trạng thái màu rõ ràng (Đang giao=xanh dương, Trễ hạn=đỏ, Hoàn thành=xanh lá), filter mạnh theo khu vực/tài xế/trạng thái.
- Mã vận đơn (tracking number): định dạng monospace để dễ đọc/copy chính xác, có nút copy nhanh.
- Cảnh báo ngoại lệ (đơn trễ, giao thất bại): nổi bật bằng màu đỏ/cam ngay trong danh sách, không lẫn với đơn bình thường.

### 5.26 POS / Bán lẻ (Điểm bán hàng, Thu ngân)

- **Ưu tiên**: tốc độ thao tác tối đa (thu ngân dùng hàng trăm lần/ngày), độ chính xác số liệu tuyệt đối, hoạt động tốt cả khi mất mạng.
- Nút sản phẩm/danh mục: kích thước LỚN hơn nhiều so với UI thông thường (tối thiểu 64-80px) — tối ưu cho thao tác nhanh, đôi khi dùng màn hình cảm ứng, giảm sai sót khi bấm vội.
- Layout chia đôi màn hình chuẩn ngành: bên trái/trên = danh sách sản phẩm dạng lưới lớn, bên phải/dưới = giỏ hàng hiện tại + tổng tiền.
- Tổng tiền thanh toán: font cực lớn (24-36px+), đậm, luôn ở vị trí cố định dễ thấy nhất màn hình — đây là con số quan trọng nhất toàn giao diện.
- Bàn phím số (numpad) cho nhập số lượng/giảm giá: nút to, bố trí chuẩn máy tính (không đảo ngược layout gây nhầm lẫn thao tác theo phản xạ).
- Trạng thái kết nối/đồng bộ: chỉ báo rõ ràng online/offline, vì POS cần hoạt động được cả khi mất mạng và đồng bộ lại sau.
- In hóa đơn/Gửi hóa đơn điện tử: nút hành động rõ ràng ngay sau khi thanh toán thành công, màn hình xác nhận đơn giản dễ thấy từ xa (khách hàng cũng nhìn được).

### 5.27 HR / Tuyển dụng

- **Ưu tiên**: quản lý quy trình nhiều bước (pipeline tuyển dụng), tổ chức lượng lớn hồ sơ, trải nghiệm chuyên nghiệp cho cả nhà tuyển dụng và ứng viên.
- Pipeline tuyển dụng: dạng Kanban board (Sàng lọc → Phỏng vấn → Offer → Đã tuyển), kéo-thả ứng viên giữa các cột, mỗi cột hiện số lượng ứng viên.
- Card ứng viên: Ảnh đại diện/Avatar → Tên → Vị trí ứng tuyển → Tag kỹ năng (dạng chip nhỏ) → Điểm đánh giá (nếu có).
- Hồ sơ ứng viên (CV): preview trực tiếp trong app (PDF viewer nhúng) thay vì bắt tải về, kèm ghi chú/đánh giá của người phỏng vấn ngay bên cạnh.
- Lịch phỏng vấn: tích hợp với Calendar, hiện rõ người tham gia, link phỏng vấn online nếu có.
- Trang tuyển dụng công khai (career page/job listing): thiết kế thân thiện, thương hiệu công ty nổi bật hơn phong cách "nội bộ enterprise" của phần quản trị.
- Onboarding nhân viên mới: checklist dạng step rõ ràng, progress bar tổng thể cho toàn bộ quá trình hòa nhập.

### 5.28 Pháp lý (Legal Tech)

- **Ưu tiên**: độ chính xác văn bản tuyệt đối, khả năng theo dõi thay đổi/phiên bản tài liệu, tin cậy và trang trọng.
- Màu sắc: tông trầm, chuyên nghiệp (xanh navy đậm, xám đậm, nâu đỏ đô) — tránh màu tươi sáng/vui nhộn, ngành pháp lý coi trọng sự nghiêm túc.
- Trình soạn thảo hợp đồng: hỗ trợ track changes (theo dõi chỉnh sửa) trực quan — thêm gạch chân xanh lá, xóa gạch ngang đỏ, kèm avatar người sửa cạnh mỗi thay đổi.
- So sánh phiên bản tài liệu (version diff): hiển thị song song 2 bản, highlight rõ phần khác biệt.
- Chữ ký điện tử (e-signature): khu vực ký rõ ràng, tách biệt, kèm timestamp + thông tin xác thực người ký hiển thị minh bạch sau khi ký.
- Quản lý case/hồ sơ vụ việc: timeline các mốc quan trọng (deadline nộp hồ sơ, ngày xét xử), cảnh báo deadline sắp tới nổi bật bằng màu cam/đỏ.
- Tìm kiếm văn bản pháp luật: kết quả cần highlight chính xác từ khóa trong đoạn văn dài, cho phép trích dẫn/copy điều khoản chính xác.

### 5.29 Ví điện tử / Crypto (Khác Fintech ngân hàng truyền thống)

- **Ưu tiên**: minh bạch biến động giá real-time, cân bằng giữa hiện đại/công nghệ và cảm giác an toàn (vì tài sản biến động cao, rủi ro mất tiền do thao tác sai lớn hơn ngân hàng truyền thống).
- Màu Primary: có thể phá cách hơn Fintech truyền thống — tím, xanh neon, gradient — phù hợp đối tượng người dùng trẻ, công nghệ; nhưng bảng Semantic (tăng/giảm giá) vẫn PHẢI theo chuẩn xanh lá/đỏ tuyệt đối.
- Biểu đồ giá (candlestick/line chart): tương tác mạnh (zoom, pan, chọn khung thời gian 1H/1D/1W/1M/1Y), nến tăng xanh lá/nến giảm đỏ theo chuẩn toàn cầu — không đảo ngược màu dù ở thị trường nào để tránh gây nhầm lẫn nghiêm trọng.
- Địa chỉ ví (wallet address): định dạng monospace, luôn có nút copy 1-chạm, hiển thị rút gọn (0x1234...5678) kèm khả năng xem đầy đủ khi cần.
- Giao dịch chuyển tài sản: CẦN màn hình xác nhận cực kỳ rõ ràng trước khi ký (địa chỉ nhận, số lượng, phí gas/network fee) — vì giao dịch blockchain không thể hoàn tác, đây là màn hình quan trọng nhất toàn app, nên có cảnh báo rõ nếu địa chỉ nhận chưa từng giao dịch trước đó.
- Bảo mật (seed phrase/private key): thiết kế màn hình backup cực kỳ nghiêm túc, cảnh báo rõ ràng nhiều lớp, không cho phép chụp màn hình nếu nền tảng hỗ trợ chặn.

### 5.30 IoT / Nhà thông minh (Smart Home)

- **Ưu tiên**: điều khiển thiết bị vật lý trực quan tức thời, trạng thái đồng bộ thời gian thực, dễ dùng cho mọi thành viên gia đình (kể cả người lớn tuổi/trẻ em).
- Card thiết bị: icon minh họa loại thiết bị (đèn/quạt/khóa cửa/camera) rõ ràng, trạng thái ON hiện nền màu sáng/ấm (thường vàng cho đèn), OFF hiện nền xám trung tính — phản hồi trực quan tức thời khi toggle.
- Nút bật/tắt chính: kích thước lớn, dễ bấm, có thể là toàn bộ card (không chỉ 1 switch nhỏ góc card) để giảm thao tác sai.
- Điều khiển độ sáng/nhiệt độ: dùng slider vòng tròn (circular slider) hoặc thanh trượt lớn, trực quan như núm vặn vật lý thật.
- Trạng thái kết nối thiết bị: rõ ràng Online/Offline/Đang cập nhật — thiết bị mất kết nối cần cảnh báo ngay (không được để người dùng tưởng đã tắt trong khi thực chất là mất kết nối).
- Nhóm phòng (room grouping): điều hướng theo phòng (Phòng khách/Phòng ngủ/Bếp) trực quan bằng tab hoặc card lớn có ảnh minh họa phòng.
- Camera an ninh: live view chiếm không gian lớn, nút Snapshot/Record dễ tiếp cận, lịch sử ghi hình dạng timeline có thumbnail.
- Automation/Kịch bản (VD: "Về nhà" tự bật đèn): xây dựng luồng dạng if-this-then-that trực quan, dễ hiểu cho người không rành kỹ thuật.

### 5.31 Wearable / Smartwatch

- **Ưu tiên**: tối giản CỰC ĐOAN do màn hình siêu nhỏ (thường 40-46mm), thao tác 1-2 giây, thông tin ưu tiên cao nhất mới được hiện.
- Chỉ hiển thị 1 thông tin chính/màn hình — không nhồi nhét nhiều dữ liệu như mobile app.
- Font-size tối thiểu phải LỚN hơn nhiều so với mobile (tối thiểu ~16-18px tương đương trên màn hình nhỏ) vì khoảng cách xem gần và màn hình cong/nhỏ.
- Touch target tối thiểu cao hơn chuẩn thông thường (tối thiểu 44px thực tế nhưng chiếm tỷ lệ % màn hình lớn hơn nhiều so với mobile) do độ chính xác chạm trên mặt kính cong thấp hơn.
- Complications (widget mặt đồng hồ): icon + số liệu cực ngắn gọn, dễ nhận diện dù chỉ liếc 0.5 giây.
- Màu nền: đen tuyệt đối được ưu tiên (không giống nguyên tắc chung ở mục 2.3) — vì màn OLED tắt pixel đen hoàn toàn giúp tiết kiệm pin đáng kể trên thiết bị pin nhỏ.
- Thao tác chính nên thực hiện được bằng 1 chạm hoặc vuốt đơn giản, tránh gesture phức tạp nhiều bước.
- Thông báo: rung + hiện ngắn gọn nhất có thể (tiêu đề + 1 dòng), có nút hành động nhanh (trả lời nhanh bằng câu dựng sẵn) thay vì bắt gõ phím ảo nhỏ xíu.

### 5.32 Kiosk / Màn hình tự phục vụ (ATM, Check-in sân bay, Máy gọi món)

- **Ưu tiên**: người dùng lạ lẫm với hệ thống (walk-up user, không có hướng dẫn trước), dùng 1 lần duy nhất, không có nút "Back" tâm lý như browser, cần rất rõ ràng và chịu lỗi tốt (error-tolerant).
- Nút bấm: kích thước RẤT lớn (tối thiểu 64-80px chiều cao), khoảng cách giữa các nút rộng rãi hơn chuẩn thông thường để tránh chạm nhầm (nhiều đối tượng dùng ngón tay to, đeo găng, hoặc thao tác vội).
- Luồng thao tác: tuyến tính rõ ràng từng bước 1 (step-by-step, không cho nhảy cóc), luôn có nút "Quay lại"/"Hủy giao dịch" hiện SẴN và rõ ràng ở vị trí cố định (thường góc trên-trái).
- Timeout: bắt buộc có — tự động hủy phiên và quay về màn hình chờ sau X giây không thao tác (bảo vệ người dùng sau bỏ quên thông tin nhạy cảm, VD: ATM).
- Ngôn ngữ: nút chọn ngôn ngữ đặt ngay màn hình đầu tiên, dễ thấy, dùng cờ quốc gia + tên ngôn ngữ bằng chính ngôn ngữ đó (không dịch tên ngôn ngữ).
- Font-size lớn hơn chuẩn ứng dụng thường (18-20px+ cho body) để đọc được từ khoảng cách đứng, không phải khoảng cách cầm tay.
- Xác nhận trước hành động không thể hoàn tác (rút tiền, in vé): luôn có bước xác nhận rõ ràng hiện đầy đủ thông tin giao dịch.
- Trạng thái xử lý (đang in vé, đang xử lý thẻ): chỉ báo tiến trình rõ ràng kèm text "Vui lòng đợi, không rút thẻ" khi cần thiết vì lý do phần cứng.

### 5.33 Ô tô / In-car UI (Infotainment)

- **Ưu tiên**: AN TOÀN khi lái xe là ưu tiên tuyệt đối cao hơn mọi yếu tố thẩm mỹ khác, thao tác tối thiểu, dễ dùng bằng giọng nói/nút vật lý song song với cảm ứng.
- Nguyên tắc "2 giây": mỗi thao tác trên màn hình không nên yêu cầu người lái nhìn màn hình quá 2 giây liên tục (tiêu chuẩn an toàn ngành ô tô — NHTSA).
- Touch target LỚN hơn cả kiosk (do rung lắc khi xe di chuyển ảnh hưởng độ chính xác chạm) — tối thiểu 76px+ cho control chính khi xe đang chạy.
- Độ tương phản và font-size phải đủ lớn để đọc được dưới ánh nắng trực tiếp/ban ngày (màn hình ô tô thường phải sáng hơn nhiều so với thiết bị thông thường).
- Chế độ "Đang lái" (Driving mode): tự động ẩn/khóa các tính năng gây xao nhãng (nhắn tin, duyệt web, video) khi xe đang di chuyển, chỉ cho phép qua điều khiển giọng nói.
- Bản đồ điều hướng (navigation): là màn hình trung tâm quan trọng nhất, chỉ dẫn rẽ hiện RẤT lớn, rõ ràng, đặt ở vị trí gần tầm nhìn người lái nhất (cluster sau vô lăng nếu có).
- Điều khiển âm nhạc/điều hòa: nên có phím tắt/nút vật lý song song trên vô lăng, không bắt buộc phải thao tác qua màn hình chạm cho các hành động thường xuyên.
- Màu sắc ban đêm: tự động chuyển dark mode với độ sáng giảm mạnh khi trời tối để không gây chói/lóa ảnh hưởng tầm nhìn ra ngoài kính xe.
- Cảnh báo an toàn (áp suất lốp thấp, sắp hết xăng): dùng màu semantic chuẩn (vàng/đỏ) + icon lớn dễ nhận + có thể kèm âm thanh, luôn ưu tiên hiển thị trên cụm đồng hồ sau vô lăng thay vì màn hình trung tâm để không rời mắt khỏi đường.

---

## 6. CHECKLIST KIỂM TRA TRƯỚC KHI SHIP

**Spacing & Layout:**
- [ ] Mọi khoảng cách dùng đúng token trong thang đo, không có giá trị tùy hứng.
- [ ] Grid và breakpoint hoạt động đúng ở cả 3 kích thước màn hình.
- [ ] Không có phần tử bị tràn/vỡ layout ở màn hình nhỏ nhất hỗ trợ.

**Typography:**
- [ ] Không quá 2 font, tối đa 3 độ đậm trên 1 màn hình.
- [ ] Không có text nào nhỏ hơn 11px.
- [ ] Line-height và line-length đảm bảo dễ đọc.

**Màu sắc:**
- [ ] Mọi cặp text/nền đạt tối thiểu contrast AA (4.5:1 text thường, 3:1 text lớn).
- [ ] Màu ngữ nghĩa (success/warning/error/info) dùng đúng và nhất quán toàn hệ thống.
- [ ] Không phụ thuộc 100% vào màu để truyền đạt thông tin (luôn kèm icon/text).
- [ ] Tỷ lệ 60-30-10 được tuân thủ, chỉ 1 CTA chính/màn hình.

**Controls:**
- [ ] Mọi control tương tác có đủ 5 trạng thái: default/hover/active/focus/disabled.
- [ ] Vùng chạm tối thiểu 44×44px (mobile) / 32×32px (desktop).
- [ ] Focus state rõ ràng, hỗ trợ điều hướng bàn phím (keyboard navigation).

**Trạng thái màn hình:**
- [ ] Đã thiết kế đủ Loading/Empty/Error/Success state cho mọi màn hình có dữ liệu động.
- [ ] Thông báo lỗi dùng ngôn ngữ người dùng hiểu, có hướng khắc phục.

**Đặc thù lĩnh vực:**
- [ ] Đã áp dụng đúng bộ quy tắc riêng của lĩnh vực tương ứng (Mục 5).
- [ ] Với dữ liệu nhạy cảm (tài chính/y tế): đã kiểm tra độ chính xác hiển thị số liệu và bảo mật thông tin mặc định.

---

*Tài liệu này là bộ khung nền tảng — khi áp dụng vào dự án thực tế, nên cụ thể hóa thành Design Tokens (file JSON/CSS variables) và Component Library (Figma/Storybook) để đảm bảo tính nhất quán khi triển khai.*
