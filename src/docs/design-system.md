# Design System — Customer Storefront (UI-00)

Tài liệu chuẩn giao diện cho phía **khách hàng**. UI-02 (Header), UI-03 (Product
Listing) và UI-04 (Product Detail) phải tuân theo tài liệu này.

> **Phạm vi:** chỉ storefront. Khu vực Admin dùng `wwwroot/css/admin.css` riêng và
> không nạp CSS của customer, nên mọi thay đổi ở đây không chạm tới Admin.

| File | Vai trò |
| --- | --- |
| `wwwroot/css/customer-tokens.css` | Biến thiết kế, typography, layout gốc. **Nạp trước.** |
| `wwwroot/css/customer.css` | Style của từng component. Nạp sau. |
| `wwwroot/design-system.html` | Trang xem thử mọi component (mở `/design-system.html`). |

> **Framework nền (UI-01):** storefront dùng **Tabler v1.5.1**, là bản build Bootstrap 5
> với tiền tố biến `--tblr-*`. Bootstrap CSS/JS **không** được nạp song song. Vì vậy mọi
> override component phải ghi `--tblr-btn-bg`… chứ không phải `--bs-btn-bg` — ghi nhầm
> tiền tố thì khai báo không có tác dụng nào cả. Icon dùng **Tabler Icons** (`<i class="ti ti-...">`).

Quy tắc bắt buộc khi viết CSS mới:

1. **Không hard-code mã màu / px lẻ.** Dùng biến trong `customer-tokens.css`.
2. **Không viết selector trần** (`button {}`, `input {}`, `table {}`). Ghi đè
   component bằng biến `--tblr-*` của chính nó hoặc bằng class cụ thể.
3. **Không đổi tên class đang có.** Mọi class trong `customer.css` đều đang được
   view sử dụng.
4. Không thêm framework (Tailwind/React/Vue) và không thêm webfont nặng.

---

## 1. Design direction

**Modern Tech Ecommerce** — nền sáng, viền mảnh, bóng rất nhẹ, nhiều khoảng thở,
đúng một màu nhấn.

| Có | Không |
| --- | --- |
| Nền trắng / xám rất nhạt | Nền tối toàn trang |
| Viền 1px màu nhạt | Viền dày, bóng đậm |
| Bóng chỉ để tạo chiều sâu | Bóng kiểu dashboard/card nổi |
| Một màu nhấn xanh + một màu giá đỏ | Nhiều màu nhấn cạnh tranh nhau |
| Khoảng trắng rộng | Nhồi nhét dày đặc |
| Bo góc 8–20px | Bo góc mặc định 4px của Bootstrap |
| Ảnh sản phẩm 1:1 | Tỉ lệ ảnh lộn xộn |

Tránh: gradient nhiều tầng, neon/gaming, glassmorphism, sidebar kiểu admin.

---

## 2. Màu

Tất cả khai báo ở `:root` trong `customer-tokens.css`.

### Nền & chữ

| Token | Giá trị | Dùng cho |
| --- | --- | --- |
| `--c-surface` | `#ffffff` | Nền card, panel, header |
| `--c-surface-2` | `#f6f7f9` | Nền trang, nền input phụ |
| `--c-surface-3` | `#eef0f4` | Topbar, nền nhấn nhẹ, skeleton |
| `--c-text` | `#16202c` | Chữ nội dung |
| `--c-text-strong` | `#0b121c` | Heading, tên sản phẩm |
| `--c-text-secondary` | `#5a6875` | Mô tả, metadata |
| `--c-text-muted` | `#8a95a1` | Placeholder, chữ mờ |
| `--c-border` | `#e6e9ee` | Viền mặc định |
| `--c-border-strong` | `#d5dae2` | Viền input, viền hover |

### Thương hiệu & hành động

| Token | Giá trị | Dùng cho |
| --- | --- | --- |
| `--c-primary` | `#2563eb` | Link, nút chính, trạng thái active |
| `--c-primary-hover` | `#1d4ed8` | Hover nút chính |
| `--c-primary-soft` | `#eff4ff` | Nền nhấn nhẹ, badge, menu active |
| `--c-cta` | `#16202c` | **CTA mua hàng** (thêm giỏ, đặt hàng, tìm) |
| `--c-price` | `#d92d20` | Giá bán |

> Hai cấp CTA có chủ đích: `--c-cta` (mực đậm) cho hành động mua hàng, `--c-primary`
> (xanh) cho hành động chính thông thường. Đừng thêm màu nhấn thứ ba.

### Ngữ nghĩa

| Trạng thái | Chữ | Nền | Viền |
| --- | --- | --- | --- |
| Success | `--c-success` `#0f9d58` | `--c-success-soft` | `--c-success-border` |
| Warning | `--c-warning` `#b54708` | `--c-warning-soft` | `--c-warning-border` |
| Danger | `--c-danger` `#d92d20` | `--c-danger-soft` | `--c-danger-border` |
| Info | `--c-info` `#0b7285` | `--c-info-soft` | `--c-info-border` |

Footer dùng nền tối `--c-ink` `#101720`.

---

## 3. Typography

Font: system stack (`--font-sans`), không nạp webfont. Hiển thị tiếng Việt tốt và
không tốn thêm request. Muốn dùng Inter thì thêm `@font-face`, biến giữ nguyên.

Base `html { font-size: 16px }` ở **mọi** kích thước màn hình (template cũ để 14px
trên mobile khiến chữ bị nhỏ).

| Vai trò | Token / class | Desktop | Mobile |
| --- | --- | --- | --- |
| Hero title | `.shop-hero h1` | `clamp()` tới 36px | 26px |
| H1 trang | `h1` | 32px / 700 | 28px |
| Section title | `.section-title` | 22px / 700 | 18px |
| H3 | `h3` | 18px / 700 | 18px |
| Body | `body` | 16px / 1.55 | 16px |
| Small | `.small` | 14px | 14px |
| Metadata | `.product-meta` | 13px, `--c-text-secondary` | 13px |
| Tên sản phẩm (card) | `.product-card .product-name` | 14px / 600, **clamp 2 dòng** | 14px |
| Giá (card) | `.product-price` | 17px / 700, `--c-price` | 16px |
| Giá (detail) | `.detail-price` | 28px / 700 | 24px |
| Giá gạch | `.product-old-price` | 13px, gạch ngang | 13px |

Heading dùng `letter-spacing: -.01em` cho chắc chữ; `line-height` 1.25 (tiêu đề) /
1.55 (nội dung).

---

## 4. Spacing, radius, shadow

### Radius

| Token | px | Dùng cho |
| --- | --- | --- |
| `--r-xs` | 6 | Badge vuông, input nhỏ |
| `--r-sm` | 8 | Button, input, thumbnail |
| `--r-md` | 12 | Alert, dropdown |
| `--r-lg` | 16 | Card, panel, empty state |
| `--r-xl` | 20 | Hero, banner lớn |
| `--r-pill` | 999 | Badge tròn, ô tìm kiếm |

### Shadow

| Token | Dùng cho |
| --- | --- |
| `--sh-xs` | Badge nổi trên ảnh |
| `--sh-sm` | Trạng thái nghỉ (hiếm dùng — ưu tiên viền) |
| `--sh-md` | Hover card, dropdown |
| `--sh-lg` | Offcanvas, modal |
| `--sh-focus` | Vòng focus (xanh, 3px) |

Mặc định card **không có bóng** — chỉ viền 1px. Bóng chỉ xuất hiện khi hover.

### Spacing

Thang: `--s-1` (4px) → `--s-20` (80px).

| Token | Mobile | ≥992px | ≥1400px |
| --- | --- | --- | --- |
| `--space-section` | 32px | 64px | 80px |
| `--space-block` | 20px | 32px | 32px |
| `--grid-gap` | 16px | 24px | 24px |

Dùng `.section` (margin-bottom = `--space-section`) và `.section-head` (hàng tiêu
đề + nút hành động) cho mọi khối lớn.

### Kích thước điều khiển

| Token | px | Dùng cho |
| --- | --- | --- |
| `--control-h-sm` | 36 | Nút trong card, nút phân trang |
| `--control-h` | 44 | Button / input tiêu chuẩn |
| `--control-h-lg` | 48 | CTA lớn, ô tìm kiếm header |

Bề ngang nội dung: `--container-max` = **1280px**, có lề 24px hai bên từ ≥1200px.

---

## 5. Component

### Button

| Class | Vai trò |
| --- | --- |
| `.btn-accent` | CTA mua hàng — nền mực đậm |
| `.btn-primary` | Hành động chính — nền xanh |
| `.btn-outline-primary` | Hành động phụ có nhấn |
| `.btn-outline-secondary` | Hành động trung tính |
| `.btn-outline-danger` / `.btn-danger` | Hành động phá hủy |
| `.btn-icon` | Nút vuông chỉ có icon (thêm kèm `.btn-*` khác) |

Kích thước: `.btn-sm` (36px) · mặc định (44px) · `.btn-lg` (48px, full-width trên
mobile). Trạng thái hover/active/disabled/focus đã định nghĩa sẵn — **không viết
lại**, chỉ chọn đúng class.

### Input

`.form-control`, `.form-select`, `textarea.form-control` cao 44px, bo 8px, viền
`--c-border-strong`, focus đổi viền xanh + vòng `--sh-focus`. Lỗi: thêm
`.is-invalid` (jQuery validate tự gắn `.input-validation-error`, đã style sẵn).
Ô số lượng dùng `.cart-qty` (rộng 3.5rem, ẩn spinner mặc định).

### Badge

- **Badge mềm** (mặc định cho trạng thái): `badge bg-<màu>-subtle text-<màu>-emphasis border border-<màu>-subtle`.
  Dùng cho còn hàng / hết hàng / trạng thái đơn. `CustomerOrderSummaryViewModel.StatusBadgeClass`
  đã trả về đúng bộ class này.
- **Badge đặc**: chỉ cho số đếm (`.shop-cart-badge`) và nhãn khuyến mãi
  (`.product-badge-discount`).

### Product card — quy chuẩn

```
┌──────────────────────┐
│  ảnh 1:1, contain    │  ← .product-thumb, padding 16px, badge -% góc trên trái
├──────────────────────┤
│ Brand · Category     │  ← .product-meta, 1 dòng, truncate
│ Tên sản phẩm         │  ← .product-name, 14px/600, clamp 2 dòng (cao cố định)
│ 1.250.000 ₫  cũ      │  ← .product-price + .product-old-price
│ [ Còn hàng (12) ]    │  ← badge mềm success / secondary
│ [ Thêm vào giỏ    ]  │  ← .btn-accent.btn-sm.w-100
│ [ Xem chi tiết    ]  │  ← .btn-outline-primary.btn-sm
└──────────────────────┘
```

- Viền 1px, bo 16px, **không bóng khi nghỉ**.
- Hover: nâng 2px + `--sh-md` + ảnh phóng nhẹ 1.03.
- Tên clamp đúng 2 dòng để lưới không so le.

### Section

```html
<section class="section">
    <div class="section-head">
        <div>
            <h2 class="section-title">Sản phẩm nổi bật</h2>
            <p class="section-subtitle">Linh kiện được mua nhiều nhất tuần này</p>
        </div>
        <a class="btn btn-outline-secondary btn-sm" href="...">Xem tất cả</a>
    </div>
    ...
</section>
```

### Empty state

Một mẫu duy nhất cho: chưa có sản phẩm · không có kết quả tìm · giỏ trống · chưa
có đơn hàng.

```html
<div class="empty-state">
    <div class="empty-icon">🔍</div>
    <h2 class="h5 text-body">Không tìm thấy sản phẩm phù hợp</h2>
    <p class="mb-3">Thử bỏ bớt bộ lọc hoặc dùng từ khóa ngắn hơn.</p>
    <a class="btn btn-outline-primary btn-sm" href="...">Xem tất cả sản phẩm</a>
</div>
```

### Loading / skeleton

Chỉ là helper CSS, **không** kèm hệ thống loading kiểu SPA: thêm `.skeleton` vào
khối giữ chỗ, kèm `.skeleton-text` / `.skeleton-title` / `.skeleton-thumb`. Tự tắt
hiệu ứng khi người dùng bật `prefers-reduced-motion`.

---

## 6. Header — đã triển khai ở UI-02

### Cấu trúc

```
header.customer-header
├── .customer-header__top      thanh thông tin — chỉ hiện từ ≥992px
├── .customer-header__main     logo · tìm kiếm · tài khoản · giỏ hàng (sticky từ ≥992px)
│   ├── .customer-header__toggle   nút ☰, chỉ hiện dưới 992px
│   ├── .customer-brand            .customer-brand__mark + .customer-brand__text
│   ├── .customer-search           .customer-search__icon/__input/__submit
│   └── .customer-actions          _LoginPartial + view component CartSummary
└── .customer-nav              hàng danh mục — chỉ hiện từ ≥992px
    └── .customer-nav__list > li > .customer-nav__link (.is-active)

#customerMenu  .offcanvas.offcanvas-start.customer-offcanvas   ← menu mobile
└── .customer-menu > li > .customer-menu__link (+ .customer-menu__sub cho danh mục con)
```

### Desktop (≥992px)

- Nền trắng, viền dưới 1px. `.customer-header__main` dính mép trên khi cuộn (`z-index: 1020`).
- Logo trái · ô tìm kiếm chiếm phần giữa (tối đa 34rem) · tài khoản + giỏ hàng phải.
- Hàng danh mục nằm dưới cùng, **wrap xuống hàng** khi tràn — tuyệt đối không đặt
  `overflow` lên `.customer-nav__list` vì nó tạo clipping context và cắt mất dropdown
  danh mục con.
- Mục đang mở: `.customer-nav__link.is-active` (chữ xanh + gạch chân 2px).

### Mobile (<992px)

- Header 2 hàng: `[☰] [logo] … [tài khoản] [giỏ hàng]` rồi tới ô tìm kiếm chiếm trọn bề ngang.
- Ô tìm kiếm luôn thấy, không giấu trong menu.
- Danh mục + điều hướng nằm trong offcanvas trái (`data-bs-toggle="offcanvas"`), dùng JS
  của Tabler, không viết JS riêng.
- Header **không** sticky trên mobile để không ăn chiều cao màn hình.
- Nhãn chữ của `.header-action` tự ẩn dưới 768px, chỉ còn icon (đã có `aria-label`).

### Tài khoản & giỏ hàng

- `_LoginPartial` luôn là một nút icon + dropdown ở mọi kích thước:
  khách chưa đăng nhập → Đăng nhập / Đăng ký; đã đăng nhập → tên tài khoản,
  Đơn hàng của tôi, Giỏ hàng, Đăng xuất (**form POST + antiforgery**).
- `CartSummary` render `.cart-link` + `.cart-link__badge`. Giỏ rỗng vẫn hiện số 0 nhưng
  dùng `.is-empty` (nền xám trung tính) thay vì màu đỏ báo động.

### Search

Chỉ **một** form trong DOM; mobile và desktop khác nhau ở `order`/`flex` của flexbox chứ
không nhân đôi markup (tránh trùng `id` và trùng field khi submit). Form giữ nguyên
`GET /san-pham?keyword=…` của CUS-08.

## 7. Product listing — spec cho UI-03

### Lưới

| Breakpoint | Số cột | Gap |
| --- | --- | --- |
| ≥1200px | 4 | 24px |
| 992–1199px | 3 | 24px |
| 768–991px | 3 | 20px |
| 576–767px | 2 | 16px |
| <576px | 2 | 16px |

Markup: `row row-cols-2 row-cols-md-3 row-cols-xl-4 g-3 g-lg-4`.

### Bộ lọc

- **Desktop:** cột trái cố định ~260–280px dùng `.filter-panel` +
  `.filter-panel-sticky`, nhóm điều kiện bằng `.filter-group` /
  `.filter-group-title`. Nội dung sản phẩm chiếm phần còn lại.
- **Mobile/tablet:** nút "Bộ lọc" mở `.offcanvas` từ dưới hoặc trái; bên trong là
  đúng markup của `.filter-panel`.
- `.filter-bar` (thanh ngang hiện tại) vẫn dùng được cho trang kết quả tìm kiếm.

### Sort & pagination

- Sort là `.form-select` đặt bên phải hàng `.section-head`, cạnh số kết quả.
- Pagination dùng `.pagination` đã chuẩn hóa: nút vuông 36px, bo 8px, khoảng cách
  8px, trang hiện tại nền `--c-primary`. Luôn giữ nguyên keyword/filter/sort trên
  URL (đã có `ProductListViewModel.RouteValues`).

---

## 8. Product detail — spec cho UI-04

### Desktop

```
┌────────────────────────┬──────────────────────────────┐
│  ảnh chính 1:1         │  Tên sản phẩm (h1)           │
│  .detail-main-image    │  Brand · Danh mục · SKU      │
│                        │  GIÁ (.detail-price) + giảm% │
│  [thumb][thumb][thumb] │  Badge tồn kho               │
│  .detail-thumb         │  Số lượng [−][ 1 ][+]        │
│                        │  [ Thêm vào giỏ hàng ] (CTA) │
└────────────────────────┴──────────────────────────────┘
┌────────────────────────┬──────────────────────────────┐
│ Mô tả sản phẩm         │ Thông số kỹ thuật (.spec-table)│
└────────────────────────┴──────────────────────────────┘
```

- Cột trái 5/12, cột phải 7/12 (đang dùng, giữ nguyên).
- Thứ tự ưu tiên thông tin: **tên → brand/danh mục → giá → tồn kho → số lượng →
  CTA → thông số → mô tả**.
- Gallery: ảnh chính bo 16px, thumbnail 64px, ảnh đang chọn có viền xanh + ring.

### Mobile

- Xếp dọc: gallery → tên → giá → tồn kho → CTA → thông số → mô tả.
- CTA `.btn-lg` tự chiếm full-width (đã set ở breakpoint <576px).

---

## 9. Cart / Checkout — spec

### Cart

- **Không dùng bảng chỉ chạy được trên desktop.** Bảng hiện tại đã bọc
  `.table-responsive` + ẩn cột phụ bằng `d-none d-md-table-cell`; UI sau có thể
  chuyển hẳn sang list/card để co tốt hơn trên mobile.
- Mỗi dòng: thumbnail 64px (`.cart-thumb`) · tên · đơn giá · ô số lượng
  (`.cart-qty` + hai nút `−`/`+`) · thành tiền · nút xóa.
- Khối tổng tiền `.cart-summary` dính bên phải trên desktop (`position: sticky`).

### Checkout

- Desktop: **trái = form** (7/12), **phải = tóm tắt đơn** (5/12, sticky).
- Mobile: xếp dọc, tóm tắt đơn nằm **trên** nút đặt hàng.
- Tổng tiền phải nổi bật nhất trong khối tóm tắt: dùng `.detail-price`.
- `.checkout-items` cuộn dọc tối đa 320px khi giỏ nhiều dòng.

---

## 10. Order — spec

### My Orders

- Bảng responsive (đang dùng) hoặc card trên mobile: mã đơn · ngày · số sản phẩm ·
  tổng tiền · badge trạng thái · nút chi tiết.
- Badge trạng thái dùng badge **mềm** (xem mục 5).

### Order Detail

- Header: mã đơn + ngày đặt + badge trạng thái + nút hủy (khi Core cho phép).
- Hai cột: thông tin nhận hàng (5/12) · danh sách hàng + tiền (7/12).
- Có thể thêm timeline trạng thái bằng `.order-timeline` (thêm `.is-done` cho các
  mốc đã qua).

---

## 11. Responsive — quy tắc chung

| Breakpoint | Ứng với |
| --- | --- |
| `<576px` | Điện thoại |
| `≥576px` | Điện thoại ngang |
| `≥768px` | Tablet |
| `≥992px` | Laptop — bắt đầu bố cục 2 cột, header sticky |
| `≥1200px` | Desktop — container tối đa 1280px |

Bắt buộc:

- Mọi trang bọc trong `.container`; layout **không** tự thêm container.
- Mọi `<table>` nằm trong `.table-responsive`.
- Vùng bấm tối thiểu 44px (`--control-h`).
- Không đặt `min-width` cố định lớn hơn màn hình.
