using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>app_settings: cài đặt chung (hạn đăng ký làm thêm do Trưởng phòng chỉnh) — yêu cầu 2026-10-21.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261021130000_AppSettings")]
    public partial class AppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    Value = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_app_settings", x => x.Key));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "app_settings");
        }
    }
}
