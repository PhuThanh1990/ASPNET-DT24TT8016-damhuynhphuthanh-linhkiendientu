# ASPNET-DT24TT8016-tranphattai-linhkiendientu

Website thương mại điện tử bán **linh kiện điện tử**, xây dựng bằng ASP.NET Core MVC.

## Công nghệ

| Thành phần | Phiên bản |
| --- | --- |
| .NET SDK | 10.0 (LTS) — bắt buộc, xem `global.json` |
| ASP.NET Core MVC | 10.0 |
| Entity Framework Core | 10.0.12 (provider SQL Server) |
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
├── Controllers/                 # MVC controllers
├── Data/
│   ├── ApplicationDbContext.cs  # EF Core DbContext
│   └── Configurations/          # Fluent API config cho từng entity
├── Migrations/                  # EF Core migrations
├── Models/                      # Entities + ViewModels
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

Áp dụng lên database local (chỉ chạy khi đã cấu hình connection string ở trên):

```bash
dotnet ef database update
```

Các lệnh hay dùng:

```bash
dotnet ef migrations add <TenMigration>   # tạo migration mới
dotnet ef migrations list                 # xem danh sách
dotnet ef migrations script -o out.sql    # xem SQL sinh ra mà không cần database
```

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
| CORE-08+ | Identity, Order, UI... | ⏳ Chưa làm |
