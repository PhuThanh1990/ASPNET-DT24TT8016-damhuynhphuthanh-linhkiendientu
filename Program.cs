using System.Text.Encodings.Web;
using System.Text.Unicode;
using ElectronicStore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found. See README.md for local database setup.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllersWithViews();

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

app.UseAuthorization();

app.MapStaticAssets();

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
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
