using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>overtimes: kết quả làm thêm (giờ thực tế, công việc đã triển khai, quyết định của người duyệt) — yêu cầu 2026-10-19.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261019100000_OvertimeCompletion")]
    public partial class OvertimeCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Completion", table: "overtimes", type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "None");
            migrationBuilder.AddColumn<string>(name: "ActualStart", table: "overtimes", type: "varchar(5)", maxLength: 5, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ActualEnd", table: "overtimes", type: "varchar(5)", maxLength: 5, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "ActualHours", table: "overtimes", type: "decimal(9,2)", precision: 9, scale: 2, nullable: true);
            migrationBuilder.AddColumn<string>(name: "DoneContent", table: "overtimes", type: "varchar(2000)", maxLength: 2000, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "DoneSentAt", table: "overtimes", type: "datetime(6)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "DoneDecidedBy", table: "overtimes", type: "varchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "DoneDecidedAt", table: "overtimes", type: "datetime(6)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "DoneReason", table: "overtimes", type: "varchar(2000)", maxLength: 2000, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var c in new[] { "Completion", "ActualStart", "ActualEnd", "ActualHours", "DoneContent", "DoneSentAt", "DoneDecidedBy", "DoneDecidedAt", "DoneReason" })
                migrationBuilder.DropColumn(name: c, table: "overtimes");
        }
    }
}
