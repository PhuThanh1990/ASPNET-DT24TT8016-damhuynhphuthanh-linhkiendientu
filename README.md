# ASPNET-DT24TT8016-damhuynhphuthanh-linhkiendientu

- **Sinh viên:** Đàm Huỳnh Phú Thành
- **MSSV:** 170124365
- **Lớp:** DT24TT8016
- **Giảng viên hướng dẫn:** TS. Đoàn Phước Miền

Website thương mại điện tử bán **linh kiện điện tử**, xây dựng bằng ASP.NET Core MVC.

## Công nghệ

| Thành phần | Phiên bản |
| --- | --- |
| .NET SDK | 10.0 (LTS) — bắt buộc, xem `global.json` |
| ASP.NET Core MVC | 10.0 |
| Entity Framework Core | 10.0.12 (provider SQL Server) |
| ASP.NET Core Identity | 10.0.12 |
| Database | SQL Server |
| Frontend | Bootstrap 5 + Tabler UI (Admin) + JavaScript thuần (không framework SPA) |

## Yêu cầu môi trường

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) trở lên
- SQL Server (một trong các lựa chọn sau):
  - **Windows:** SQL Server Express / LocalDB / Developer Edition
  - **macOS / Linux:** SQL Server chạy bằng Docker (xem bên dưới)

## Cấu hình database (bắt buộc làm trước khi chạy)

Connection string tên **`DefaultConnection`**. Giá trị mặc định trong `appsettings.json`
dùng **Windows Authentication** nên **không chứa mật khẩu**:

```
Server=localhost;Database=ElectronicStoreDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

> ⚠️ **Không bao giờ commit mật khẩu thật vào Git.** Repo này là public.
> Mọi thông tin đăng nhập database phải nằm ở User Secrets hoặc biến môi trường.

### Cách 1 — Windows + SQL Server (Windows Authentication)

Không cần làm gì thêm, giá trị mặc định đã chạy được.
Nếu dùng LocalDB thì ghi đè bằng User Secrets (xem cách 2) với:

```
Server=(localdb)\MSSQLLocalDB;Database=ElectronicStoreDb;Trusted_Connection=True;MultipleActiveResultSets=True
```

### Cách 2 — macOS / Linux + SQL Server trong Docker (khuyến nghị)

Chạy SQL Server:

```bash
docker run -d --name sqlserver \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=<mat-khau-cua-ban>" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

Ghi connection string vào **User Secrets** (file này nằm ngoài repo nên không bị commit):

```bash
cd src
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=ElectronicStoreDb;User Id=sa;Password=<mat-khau-cua-ban>;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

Kiểm tra lại:

```bash
cd src
dotnet user-secrets list
```

### Cách 3 — Biến môi trường (dùng cho CI/CD hoặc deploy)

```bash
export ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"
```

> Thứ tự ưu tiên cấu hình: **biến môi trường** > **User Secrets** > `appsettings.{Environment}.json` > `appsettings.json`.

## Cấu trúc repository

```
.
├── README.md
├── setup/                       # File phục vụ cài đặt/chạy chương trình (nếu có)
├── src/                         # Source code và dữ liệu thử nghiệm
└── thesis/
    ├── doc/                     # Báo cáo Word
    └── pdf/                     # Báo cáo PDF
```

| Thư mục | Nội dung |
| --- | --- |
| `setup/` | Các file phục vụ cài đặt / chạy chương trình |
| `src/` | Source code của hệ thống và dữ liệu thử nghiệm |
| `thesis/doc/` | File báo cáo Word |
| `thesis/pdf/` | File báo cáo PDF |

`ElectronicStore.sln`, `global.json` (pin .NET SDK), `.config/dotnet-tools.json`
(pin `dotnet-ef`) và `.gitignore` nằm ở root vì phải áp dụng cho cả repository.

## Chạy dự án

```bash
dotnet restore
dotnet build            # chạy từ root, dùng ElectronicStore.sln

cd src
dotnet run              # chạy web app
```

Mặc định ứng dụng chạy tại `https://localhost:7036` và `http://localhost:5155`
(xem `src/Properties/launchSettings.json`).

## Cấu trúc source (`src/`)

```
src/
├── Areas/Admin/                 # Khu vực Admin (chỉ role Admin truy cập được)
│   ├── Controllers/             # Dashboard, Category, Brand, Product, Order
│   ├── Models/                  # ViewModel riêng của Admin
│   ├── Services/                # DashboardService (báo cáo), ProductImageStorage (upload)
│   └── Views/                   # Razor views + _AdminLayout (Tabler UI)
├── Controllers/                 # MVC controllers phía khách hàng
│   └── Api/                     # API nội bộ: /api/dia-gioi (tỉnh/thành, phường/xã)
├── Data/
│   ├── ApplicationDbContext.cs  # EF Core DbContext (IdentityDbContext)
│   ├── DbInitializer.cs         # Migrate + seed role, admin, catalog
│   ├── CatalogSeedData.cs       # Dữ liệu catalog mẫu
│   ├── AdministrativeUnits/     # Dataset địa giới hành chính VN (JSON, kèm repo)
│   └── Configurations/          # Fluent API config cho từng entity
├── Helpers/                     # Helper hiển thị (giá, ảnh), Session, giỏ hàng, parse spec
├── Migrations/                  # EF Core migrations
├── Models/                      # Entities + enum + quy tắc chuyển trạng thái đơn
│   └── ViewModels/              # ViewModel cho form và cho trang khách hàng
├── Services/                    # Nghiệp vụ dùng chung: OrderService, CartService,
│                                #   AdministrativeUnitService
├── ViewComponents/              # View component (menu danh mục, badge giỏ hàng ở navbar)
├── Views/                       # Razor views phía khách hàng
├── wwwroot/                     # CSS, JS, ảnh, thư viện client
├── docs/
│   ├── database-schema.md       # Thiết kế database của toàn hệ thống
│   └── design-system.md         # Design system (màu, typography, component)
├── Program.cs                   # Entry point + đăng ký DI
├── appsettings.json             # Cấu hình chung (KHÔNG chứa secret)
└── appsettings.Development.json # Cấu hình môi trường Development
```

Dataset địa giới hành chính được đọc qua `IWebHostEnvironment.ContentRootPath` nên
đường dẫn `Data/AdministrativeUnits/` vẫn đúng sau khi source chuyển vào `src/`.

## Tài khoản và phân quyền

Hệ thống dùng **ASP.NET Core Identity** (email làm username). Có 2 role: `Admin` và `Customer`.

- Đăng ký ở `/Account/Register` → tài khoản **luôn** nhận role `Customer`. Không có cách nào
  tự chọn role `Admin` từ form.
- Đăng nhập `/Account/Login`, đăng xuất bằng POST từ thanh điều hướng.
- Khu vực `src/Areas/Admin` yêu cầu role `Admin`; ai không đủ quyền bị đưa về `/Account/AccessDenied`.

### Tài khoản Admin mặc định

Email lấy từ `SeedAdmin:Email` trong `appsettings.json`
(mặc định `admin@electronicstore.local`). **Mật khẩu không nằm trong repo.**

Cách đặt mật khẩu admin cho máy của bạn:

```bash
cd src
dotnet user-secrets set "SeedAdmin:Password" "<mat-khau-cua-ban>"
```

Nếu chưa đặt:

| Môi trường | Hành vi |
| --- | --- |
| Development | Dùng mật khẩu dev có sẵn trong `src/Data/DbInitializer.cs` (`Admin@123456`) và ghi cảnh báo ra log. Chỉ để chạy thử trên máy cá nhân |
| Ngoài Development | **Không tạo** tài khoản admin, chỉ ghi cảnh báo. Không có mật khẩu mặc định nào lọt ra production |

> ⚠️ Mật khẩu dev ở trên là công khai trong source. Đừng dùng nó cho bất kỳ máy chủ nào
> có người khác truy cập được.

### Dữ liệu seed

Khi chạy ở Development, `src/Data/DbInitializer.cs` tự động: apply migration → tạo role
`Admin`/`Customer` → tạo admin mặc định → seed 5 category, 4 brand và 5 sản phẩm mẫu
(ESP32 DevKit, Arduino Uno R3, DHT22, HC-SR04, Module Relay 5V).

Seed đối chiếu theo `Slug` nên **chạy lại nhiều lần không tạo dữ liệu trùng**.

## Migration

Công cụ `dotnet-ef` được pin theo project trong `.config/dotnet-tools.json` (đúng phiên bản
EF Core 10.0.12), nên **không cần cài global**. Sau khi clone repo:

```bash
dotnet tool restore
```

Migration hiện có:

| # | Migration | Nội dung |
| --- | --- | --- |
| 1 | `AddCatalogModels` | Tạo 4 bảng `Categories`, `Brands`, `Products`, `ProductImages` |
| 2 | `AddIdentityAndOrderFoundation` | Tạo các bảng Identity (`AspNetUsers`, `AspNetRoles`, ...) và `Addresses`, `Orders`, `OrderDetails` |
| 3 | `AddAdministrativeUnitCodesToAddress` | `Addresses`: thêm `ProvinceCode` / `WardCode`, đổi `District` thành nullable (cơ cấu hành chính 2 cấp từ 01/07/2025) |

Áp dụng lên database local (chỉ chạy khi đã cấu hình connection string ở trên):

```bash
cd src
dotnet ef database update
```

> Ở môi trường **Development**, `dotnet run` đã tự gọi `Database.MigrateAsync()` rồi seed
> dữ liệu, nên thường không cần chạy tay lệnh trên. Môi trường khác thì phải chạy tay —
> ứng dụng không tự migrate ngoài Development.

Các lệnh hay dùng:

```bash
cd src
dotnet ef migrations add <TenMigration>   # tạo migration mới
dotnet ef migrations list                 # xem danh sách
dotnet ef migrations script -o out.sql    # xem SQL sinh ra mà không cần database
```

## Route phía khách hàng

| URL | Action | Ghi chú |
| --- | --- | --- |
| `/` | `Home/Index` | Trang chủ: banner, danh mục, sản phẩm nổi bật & mới nhất |
| `/san-pham` | `Product/Index` | Danh sách sản phẩm |
| `/san-pham/{slug}` | `Product/Details` | Chi tiết sản phẩm, tra theo `Slug` (UNIQUE) |
| `/gio-hang` | `Cart/Index` | Giỏ hàng (lưu trong Session) |
| `/thanh-toan` | `Checkout/Index` | Trang thanh toán — cần đăng nhập |
| `/dat-hang-thanh-cong/{id}` | `Checkout/Success` | Xác nhận đặt hàng — chỉ chủ đơn xem được |
| `/don-hang` | `Order/Index` | Đơn hàng của tôi — cần đăng nhập |
| `/don-hang/{id}` | `Order/Details` | Chi tiết đơn — chỉ chủ đơn xem được |
| `/ShippingAddress` | `ShippingAddress/Index` | Sổ địa chỉ giao hàng — cần đăng nhập |
| `/Account/Register`, `/Account/Login` | `Account/*` | Đăng ký / đăng nhập |
| `/api/dia-gioi/tinh-thanh`, `/api/dia-gioi/phuong-xa?provinceCode=` | `Api/AdministrativeUnits` | API nội bộ đổ selectbox địa chỉ |

Danh sách sản phẩm nhận các tham số query string, kết hợp được với nhau và luôn được giữ
lại trên link phân trang:

| Tham số | Ví dụ | Ý nghĩa |
| --- | --- | --- |
| `keyword` | `?keyword=esp32` | Tìm theo tên sản phẩm, mã SKU hoặc tên thương hiệu |
| `category` | `?category=cam-bien` | Lọc theo slug danh mục |
| `brand` | `?brand=espressif` | Lọc theo slug thương hiệu |
| `sort` | `?sort=price-asc` | `newest` (mặc định), `price-asc`, `price-desc`, `name` |
| `page` | `?page=2` | Trang hiện tại, 12 sản phẩm mỗi trang |

Sai đường dẫn hoặc slug không tồn tại sẽ trả về trang 404 `Home/HttpError`.
Giá trị `category` / `brand` / `sort` / `page` không hợp lệ thì trang vẫn hiển thị bình
thường (0 kết quả hoặc quay về giá trị mặc định), không báo lỗi.

## Chức năng phía khách hàng (Customer)

| Nhóm | Chức năng |
| --- | --- |
| Tài khoản | Đăng ký (luôn nhận role `Customer`), đăng nhập, đăng xuất; khóa tài khoản sau 5 lần sai mật khẩu trong 15 phút |
| Catalog | Trang chủ (danh mục, sản phẩm nổi bật, mới nhất); danh sách sản phẩm; chi tiết sản phẩm kèm thông số kỹ thuật dạng JSON |
| Tìm & lọc | Tìm theo tên / SKU / thương hiệu; lọc theo danh mục và thương hiệu; sắp xếp theo giá và ngày; phân trang server-side |
| Giỏ hàng | Thêm vào giỏ, sửa số lượng, xóa dòng, xóa giỏ; giỏ lưu trong Session; badge số lượng trên navbar; tự điều chỉnh và cảnh báo khi giá hoặc tồn kho đã đổi |
| Sổ địa chỉ | Thêm / sửa / xóa địa chỉ, đặt địa chỉ mặc định; chọn Tỉnh/Thành phố → Phường/Xã bằng selectbox |
| Đặt hàng | Checkout bằng địa chỉ đã lưu hoặc nhập địa chỉ mới; phí ship và tổng tiền do server tính; trang đặt hàng thành công |
| Đơn hàng | Danh sách đơn của tôi (phân trang, lọc trạng thái), chi tiết đơn kèm thanh tiến trình trạng thái, tự hủy đơn khi còn `Pending` |

## Chức năng phía quản trị (Admin)

Toàn bộ nằm trong `src/Areas/Admin`, **bắt buộc role `Admin`** (`AdminControllerBase`).

| Module | URL | Chức năng |
| --- | --- | --- |
| Dashboard | `/Admin/Home` | KPI (số đơn, đơn chờ / hoàn tất / đã hủy, doanh thu, sản phẩm, sản phẩm hết hàng, số khách); biểu đồ doanh thu 7 / 30 / 90 ngày; sản phẩm bán chạy; đơn mới nhất |
| Danh mục | `/Admin/Category` | Danh sách, thêm, sửa, xóa; danh mục cha/con; tự sinh slug |
| Thương hiệu | `/Admin/Brand` | Danh sách, thêm, sửa, xóa; tự sinh slug |
| Sản phẩm | `/Admin/Product` | Danh sách, thêm, sửa, xóa; bật/tắt bán (`IsActive`); điều chỉnh tồn kho |
| Ảnh sản phẩm | `/Admin/Product/Images/{id}` | Upload ảnh (kiểm tra đuôi, content-type và **magic bytes**, tối đa 2MB), đặt ảnh đại diện, xóa ảnh |
| Đơn hàng | `/Admin/Order` | Danh sách kèm badge đếm theo trạng thái, lọc theo trạng thái, chi tiết đơn; chuyển trạng thái theo đúng state machine; hủy đơn và **hoàn trả tồn kho** |

Doanh thu chỉ tính trên đơn `Completed`. Mọi thay đổi trạng thái đơn và tồn kho đều đi qua
`src/Services/OrderService.cs`; Admin **không** có state machine riêng.

## Tài liệu

- [Thiết kế database](src/docs/database-schema.md) — bảng, khóa, quan hệ, index. **Đọc file này trước khi tạo entity.**
- [Design system](src/docs/design-system.md) — màu, typography, component dùng chung.

## Tiến độ

| Task | Nội dung | Trạng thái |
| --- | --- | --- |
| CORE-01 | Khởi tạo ASP.NET Core MVC project | ✅ |
| CORE-02 | Cấu hình SQL Server connection | ✅ |
| CORE-03 | Cài đặt & cấu hình EF Core | ✅ |
| CORE-04 | Tạo `ApplicationDbContext` | ✅ |
| CORE-05 | Thiết kế database schema | ✅ |
| CORE-06 | Models `Category`, `Brand` | ✅ |
| CORE-07 | Models `Product`, `ProductImage` | ✅ |
| CORE-08 | Setup ASP.NET Core Identity | ✅ |
| CORE-09 | Role `Admin` / `Customer` | ✅ |
| CORE-10 | Seed admin, category, brand, product | ✅ |
| CORE-11 | Migration + database ban đầu | ✅ |
| CORE-12 | Register / Login / Logout | ✅ |
| CORE-13 | Authorization theo role | ✅ |
| CORE-14 | Model `Address` | ✅ |
| CORE-15 | Models `Order`, `OrderDetail` | ✅ |
| CORE-16 | `OrderService` | ✅ |
| CORE-17 | Validate tồn kho khi đặt hàng | ✅ |
| CORE-18 | Order status workflow + hủy đơn | ✅ |
| CORE-20 | Chặn tài khoản bị khóa (`IsActive = 0`) ngay ở request kế tiếp | ✅ |
| CUS-01 | Customer Layout | ✅ |
| CUS-02 | Navbar & Footer responsive | ✅ |
| CUS-03 | Home Page | ✅ |
| CUS-04 | Product Card (partial dùng lại được) | ✅ |
| CUS-05 | Trang danh sách sản phẩm | ✅ |
| CUS-06 | Trang chi tiết sản phẩm | ✅ |
| CUS-07 | Hiển thị thông số kỹ thuật (JSON) | ✅ |
| CUS-08 | Search sản phẩm | ✅ |
| CUS-09 | Filter theo Category / Brand | ✅ |
| CUS-10 | Sort theo giá và mới nhất | ✅ |
| CUS-11 | Pagination server-side | ✅ |
| CUS-12 | Add to Cart (Session) | ✅ |
| CUS-13 | Trang Shopping Cart | ✅ |
| CUS-14 | Update quantity / Remove item | ✅ |
| CUS-15 | Subtotal & Total giỏ hàng | ✅ |
| CUS-16 | Trang Checkout | ✅ |
| CUS-17 | Submit Checkout, tạo Order qua OrderService | ✅ |
| CUS-18 | Order Success + clear Cart | ✅ |
| CUS-19 | My Orders, Order Detail, hủy đơn | ✅ |
| CUS-20 | Responsive, UI polish, test customer flow | ✅ |
| ADM-04 → ADM-13 | Admin layout, CRUD Category / Brand / Product, upload ảnh | ✅ |
| ADM-14 | Danh sách đơn hàng + lọc theo trạng thái | ✅ |
| ADM-15 | Chi tiết đơn hàng | ✅ |
| ADM-16 | Chuyển trạng thái đơn + hủy đơn (hoàn kho) | ✅ |
| ADM-17 → ADM-20 | Dashboard: KPI, biểu đồ doanh thu, bán chạy, đơn mới | ✅ |
| UI-00 → UI-08 | Design system, Tabler UI, redesign storefront + admin, responsive | ✅ |
| ADDR-01 | Sổ địa chỉ CRUD trên entity `Address` có sẵn | ✅ |
| ADDR-02 | Selectbox Tỉnh/Thành phố → Phường/Xã (cơ cấu 2 cấp) | ✅ |
| ADDR-03 | Checkout dùng địa chỉ đã lưu | ✅ |
| FINAL-ADDR | Thống nhất sổ địa chỉ và checkout dùng chung một cơ chế địa chỉ | ✅ |
| FINAL-QA | Rà soát và sửa lỗi end-to-end | ✅ |
| Ngoài phạm vi | Thanh toán online (VNPay), đánh giá sản phẩm, wishlist, mã giảm giá | ⏳ Chưa làm |
