# ASPNET-DT24TT8016-tranphattai-linhkiendientu

Website thương mại điện tử bán **linh kiện điện tử**, xây dựng bằng ASP.NET Core MVC.

## Công nghệ

| Thành phần | Phiên bản |
| --- | --- |
| .NET SDK | 10.0 (LTS) — bắt buộc, xem `global.json` |
| ASP.NET Core MVC | 10.0 |
| Entity Framework Core | 10.0.12 (provider SQL Server) |
| ASP.NET Core Identity | 10.0.12 |
| Database | SQL Server |
| Frontend | Bootstrap 5 + JavaScript |

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
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=ElectronicStoreDb;User Id=sa;Password=<mat-khau-cua-ban>;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

Kiểm tra lại:

```bash
dotnet user-secrets list
```

### Cách 3 — Biến môi trường (dùng cho CI/CD hoặc deploy)

```bash
export ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"
```

> Thứ tự ưu tiên cấu hình: **biến môi trường** > **User Secrets** > `appsettings.{Environment}.json` > `appsettings.json`.

## Chạy dự án

```bash
dotnet restore
dotnet build
dotnet run
```

Mặc định ứng dụng chạy tại `https://localhost:7036` và `http://localhost:5155`
(xem `Properties/launchSettings.json`).

## Cấu trúc thư mục

```
├── Areas/Admin/                 # Khu vực Admin (chỉ role Admin truy cập được)
├── Controllers/                 # MVC controllers
├── Data/
│   ├── ApplicationDbContext.cs  # EF Core DbContext (IdentityDbContext)
│   ├── DbInitializer.cs         # Migrate + seed role, admin, catalog
│   ├── CatalogSeedData.cs       # Dữ liệu catalog mẫu
│   └── Configurations/          # Fluent API config cho từng entity
├── Helpers/                     # Helper hiển thị (định dạng giá, ảnh mặc định)
├── Migrations/                  # EF Core migrations
<<<<<<< HEAD
├── Models/                      # Entities + enum
│   └── ViewModels/              # ViewModel cho form (có DataAnnotation validation)
=======
├── Models/                      # Entities
│   └── ViewModels/              # ViewModel cho các trang khách hàng
├── ViewComponents/              # View component (menu danh mục ở navbar)
>>>>>>> kien/main
├── Views/                       # Razor views
├── wwwroot/                     # CSS, JS, ảnh, thư viện client
├── docs/
│   └── database-schema.md       # Thiết kế database của toàn hệ thống
├── Program.cs                   # Entry point + đăng ký DI
├── appsettings.json             # Cấu hình chung (KHÔNG chứa secret)
├── appsettings.Development.json # Cấu hình môi trường Development
├── global.json                  # Pin phiên bản .NET SDK cho cả nhóm
└── ElectronicStore.sln
```

## Tài khoản và phân quyền

Hệ thống dùng **ASP.NET Core Identity** (email làm username). Có 2 role: `Admin` và `Customer`.

- Đăng ký ở `/Account/Register` → tài khoản **luôn** nhận role `Customer`. Không có cách nào
  tự chọn role `Admin` từ form.
- Đăng nhập `/Account/Login`, đăng xuất bằng POST từ thanh điều hướng.
- Khu vực `Areas/Admin` yêu cầu role `Admin`; ai không đủ quyền bị đưa về `/Account/AccessDenied`.

### Tài khoản Admin mặc định

Email lấy từ `SeedAdmin:Email` trong `appsettings.json`
(mặc định `admin@electronicstore.local`). **Mật khẩu không nằm trong repo.**

Cách đặt mật khẩu admin cho máy của bạn:

```bash
dotnet user-secrets set "SeedAdmin:Password" "<mat-khau-cua-ban>"
```

Nếu chưa đặt:

| Môi trường | Hành vi |
| --- | --- |
| Development | Dùng mật khẩu dev có sẵn trong `Data/DbInitializer.cs` (`Admin@123456`) và ghi cảnh báo ra log. Chỉ để chạy thử trên máy cá nhân |
| Ngoài Development | **Không tạo** tài khoản admin, chỉ ghi cảnh báo. Không có mật khẩu mặc định nào lọt ra production |

> ⚠️ Mật khẩu dev ở trên là công khai trong source. Đừng dùng nó cho bất kỳ máy chủ nào
> có người khác truy cập được.

### Dữ liệu seed

Khi chạy ở Development, `Data/DbInitializer.cs` tự động: apply migration → tạo role
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

| Migration | Nội dung |
| --- | --- |
| `AddCatalogModels` | Tạo 4 bảng `Categories`, `Brands`, `Products`, `ProductImages` |
| `AddIdentityAndOrderFoundation` | Tạo các bảng Identity (`AspNetUsers`, `AspNetRoles`, ...) và `Addresses`, `Orders`, `OrderDetails` |

Áp dụng lên database local (chỉ chạy khi đã cấu hình connection string ở trên):

```bash
dotnet ef database update
```

> Ở môi trường **Development**, `dotnet run` đã tự gọi `Database.MigrateAsync()` rồi seed
> dữ liệu, nên thường không cần chạy tay lệnh trên. Môi trường khác thì phải chạy tay —
> ứng dụng không tự migrate ngoài Development.

Các lệnh hay dùng:

```bash
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

Sai đường dẫn hoặc slug không tồn tại sẽ trả về trang 404 `Home/HttpError`.

## Tài liệu

- [Thiết kế database](docs/database-schema.md) — bảng, khóa, quan hệ, index. **Đọc file này trước khi tạo entity.**

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
<<<<<<< HEAD
| CORE-08 | Setup ASP.NET Core Identity | ✅ |
| CORE-09 | Role `Admin` / `Customer` | ✅ |
| CORE-10 | Seed admin, category, brand, product | ✅ |
| CORE-11 | Migration + database ban đầu | ✅ |
| CORE-12 | Register / Login / Logout | ✅ |
| CORE-13 | Authorization theo role | ✅ |
| CORE-14 | Model `Address` | ✅ |
| CORE-15 | Models `Order`, `OrderDetail` | ✅ |
| CORE-16+ | Catalog UI, Cart, Checkout, Admin CRUD... | ⏳ Chưa làm |
=======
| CUS-01 | Customer Layout | ✅ |
| CUS-02 | Navbar & Footer responsive | ✅ |
| CUS-03 | Home Page | ✅ |
| CUS-04 | Product Card (partial dùng lại được) | ✅ |
| CUS-05 | Trang danh sách sản phẩm | ✅ |
| CUS-06 | Trang chi tiết sản phẩm | ✅ |
| CUS-07+ | Search / Filter / Sort / Paging | ⏳ Chưa làm |
| CORE-08+ | Identity, Cart, Order, Admin... | ⏳ Chưa làm |
>>>>>>> kien/main
