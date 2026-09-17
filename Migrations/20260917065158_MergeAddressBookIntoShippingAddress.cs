using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectronicStore.Migrations
{
    /// <inheritdoc />
    public partial class MergeAddressBookIntoShippingAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bỏ khóa ngoại cũ TRƯỚC khi chuyển dữ liệu: bước remap bên dưới gán cho
            // Orders.AddressId những Id mới của bảng ShippingAddresses, các giá trị đó
            // không tồn tại trong Addresses nên ràng buộc cũ sẽ chặn lệnh UPDATE.
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Addresses_AddressId",
                table: "Orders");

            // Chuyển dữ liệu sổ địa chỉ cũ sang bảng mới rồi trỏ lại các đơn hàng.
            //
            // Dùng MERGE thay cho INSERT ... SELECT vì chỉ MERGE mới OUTPUT được đồng thời
            // cột của nguồn (Id cũ) lẫn cột của bản ghi vừa chèn (Id mới) — đó là cách duy
            // nhất lấy được bảng ánh xạ cũ→mới trong một lần chạy.
            //
            // Lưu ý về dữ liệu: bảng cũ chỉ có TÊN đơn vị hành chính, không có MÃ. Vì
            // ProvinceCode/WardCode là NOT NULL nên các dòng chuyển sang được điền chuỗi
            // rỗng, nghĩa là "chưa biết mã". Lần đầu khách mở form sửa địa chỉ đó, phần
            // validation sẽ buộc nhập mã — dữ liệu tự được hoàn thiện dần.
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Addresses]', N'U') IS NOT NULL
                BEGIN
                    DECLARE @Map TABLE (LegacyId int PRIMARY KEY, NewId int NOT NULL);

                    MERGE INTO [ShippingAddresses] AS target
                    USING (SELECT * FROM [Addresses]) AS source
                    ON 1 = 0
                    WHEN NOT MATCHED THEN
                        INSERT (UserId, ReceiverName, PhoneNumber,
                                ProvinceCode, ProvinceName,
                                DistrictCode, DistrictName,
                                WardCode, WardName,
                                StreetAddress, IsDefault, CreatedAt, UpdatedAt)
                        VALUES (source.UserId, source.FullName, source.PhoneNumber,
                                N'', source.Province,
                                NULL, NULLIF(LTRIM(RTRIM(source.District)), N''),
                                N'', source.Ward,
                                source.AddressLine,
                                -- Tài khoản đã có địa chỉ mặc định trong sổ mới thì dòng
                                -- chuyển sang không được mang cờ mặc định, nếu không sẽ vi
                                -- phạm UX_ShippingAddresses_UserId_Default.
                                CASE WHEN source.IsDefault = 1
                                       AND NOT EXISTS (SELECT 1 FROM [ShippingAddresses] existing
                                                       WHERE existing.UserId = source.UserId
                                                         AND existing.IsDefault = 1)
                                     THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                                source.CreatedAt, source.UpdatedAt)
                    OUTPUT source.Id, inserted.Id INTO @Map (LegacyId, NewId);

                    UPDATE o
                    SET o.AddressId = m.NewId
                    FROM [Orders] o
                    INNER JOIN @Map m ON o.AddressId = m.LegacyId;
                END
            ");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_ShippingAddresses_AddressId",
                table: "Orders",
                column: "AddressId",
                principalTable: "ShippingAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Down() dựng lại bảng Addresses và khóa ngoại cũ, nhưng KHÔNG khôi phục được dữ
        /// liệu: sau khi gộp, các dòng chuyển sang nằm lẫn với địa chỉ do khách tự thêm nên
        /// không còn cách nào phân biệt. Việc gộp này là một chiều — muốn quay lại thì phục
        /// hồi từ bản sao lưu database.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_ShippingAddresses_AddressId",
                table: "Orders");

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ward = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Addresses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_UserId",
                table: "Addresses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_Addresses_UserId_Default",
                table: "Addresses",
                column: "UserId",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Addresses_AddressId",
                table: "Orders",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
