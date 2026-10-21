using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>overtimes: Leader đã duyệt (LeaderBy / LeaderAt) cho đăng ký của NV làm thêm sau 20:00 cần thêm Trưởng phòng duyệt — yêu cầu 2026-10-21.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261021100000_OvertimeManagerStep")]
    public partial class OvertimeManagerStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "LeaderBy", table: "overtimes", type: "varchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "LeaderAt", table: "overtimes", type: "datetime(6)", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LeaderBy", table: "overtimes");
            migrationBuilder.DropColumn(name: "LeaderAt", table: "overtimes");
        }
    }
}
