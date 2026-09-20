using System.Text.Encodings.Web;
using System.Text.Unicode;
using ElectronicStore.Areas.Admin.Services;
using ElectronicStore.Data;
using ElectronicStore.Models;
using ElectronicStore.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found. See README.md for local database setup.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // The e-mail doubles as the user name, so it has to be unique.
        options.User.RequireUniqueEmail = true;

        // No e-mail confirmation flow in this scope.
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    // CORE-20 — AccountController checks ApplicationUser.IsActive at sign-in, but that is a
    // one-off: a cookie issued before the account was disabled stayed valid for the whole
    // 7 days, so a banned user (an admin included) kept full access. Identity's own security
    // stamp does not change when IsActive is toggled, so it cannot close this on its own.
    // Re-checking the flag whenever the cookie is validated makes disabling take effect on
    // the very next request.
    options.Events.OnValidatePrincipal = async context =>
    {
        // Runs Identity's standard stamp validation first, so "sign out everywhere" and
        // password changes keep working exactly as before.
        await SecurityStampValidator.ValidatePrincipalAsync(context);

        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.GetUserAsync(context.Principal);

        // Deleted account or disabled account: drop the cookie instead of trusting it.
        // Costs one primary-key lookup per authenticated request; if that ever matters,
        // call UserManager.UpdateSecurityStampAsync when toggling IsActive and this can
        // fall back to the stamp interval.
        if (user is null || !user.IsActive)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});

// Order business rules (CORE-16..18). Scoped so it shares the request's DbContext, which
// is what lets PlaceOrder/Cancel wrap their work in a single transaction.
builder.Services.AddScoped<IOrderService, OrderService>();

// ADDR-02 — dữ liệu địa giới hành chính đọc từ file JSON kèm repo, không đổi lúc chạy nên
// nạp một lần và dùng chung cho mọi request.
builder.Services.AddSingleton<IAdministrativeUnitService, AdministrativeUnitService>();

// CUS-16 — Cart và Checkout dùng chung một chỗ đối chiếu giỏ hàng với database.
builder.Services.AddScoped<ICartService, CartService>();

builder.Services.AddControllersWithViews();

// CUS-12 — giỏ hàng sống trong Session. AddDistributedMemoryCache là bộ nhớ trong của
// chính tiến trình web: đủ cho đồ án, nếu sau này chạy nhiều instance thì đổi sang Redis
// hoặc SQL Server session store mà không phải sửa code giỏ hàng.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".ElectronicStore.Session";
    options.Cookie.HttpOnly = true;

    // Giỏ hàng cần cookie này để hoạt động nên nó thuộc nhóm "essential".
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(2);
});

// Admin area services.
// ProductImageStorage writes uploaded images under wwwroot and hands back their URL.
builder.Services.AddScoped<ProductImageStorage>();

// Read-only reporting behind the Admin dashboard (ADM-17 -> ADM-19). It only counts and
// sums; every order write still goes through the Core IOrderService registered above.
builder.Services.AddScoped<DashboardService>();
// Order status changes go through the Core IOrderService registered above; the Admin area
// has no order service of its own.

// Mặc định Razor encode mọi ký tự ngoài bảng Latin cơ bản thành &#x...; nên tên sản phẩm
// tiếng Việt và ký hiệu ₫ bị "bẩn" trong HTML. Cho phép toàn bộ Unicode để giữ nguyên chữ.
builder.Services.Configure<WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Turns a bare 404 (unknown slug, wrong URL) into the styled Home/HttpError page while
// keeping the original address in the browser bar.
app.UseStatusCodePagesWithReExecute("/Home/HttpError", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseRouting();

// Authentication must run before authorization: the first builds the identity, the
// second decides what that identity is allowed to reach.
app.UseAuthentication();
app.UseAuthorization();

// Phải nằm trước khi map endpoint thì controller mới đọc được HttpContext.Session.
app.UseSession();

app.MapStaticAssets();

// Các controller dùng attribute routing (API nội bộ dưới /api/...). Route quy ước của
// storefront vẫn khai báo bên dưới.
app.MapControllers();

// SEO-friendly catalog URLs, matching the slug convention in docs/database-schema.md:
//   /san-pham                              -> product list
//   /san-pham/ram-corsair-vengeance-16gb   -> product detail
app.MapControllerRoute(
    name: "productList",
    pattern: "san-pham",
    defaults: new { controller = "Product", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "productDetail",
    pattern: "san-pham/{slug}",
    defaults: new { controller = "Product", action = "Details" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "cart",
    pattern: "gio-hang",
    defaults: new { controller = "Cart", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "checkout",
    pattern: "thanh-toan",
    defaults: new { controller = "Checkout", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "checkoutSuccess",
    pattern: "dat-hang-thanh-cong/{id:int}",
    defaults: new { controller = "Checkout", action = "Success" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "myOrders",
    pattern: "don-hang",
    defaults: new { controller = "Order", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "myOrderDetails",
    pattern: "don-hang/{id:int}",
    defaults: new { controller = "Order", action = "Details" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Development convenience only: apply migrations and seed reference data on startup so a
// teammate gets a working database from a single `dotnet run`. Other environments run
// `dotnet ef database update` explicitly - see README.md.
if (app.Environment.IsDevelopment())
{
    await app.InitializeDatabaseAsync();
}

app.Run();
