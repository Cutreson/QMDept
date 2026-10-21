using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>people: HrDuty — Leader được giao quản lý nhân sự / báo cơm / báo làm thêm tại công ty của cả phòng (yêu cầu 2026-10-21).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261021120000_PersonHrDuty")]
    public partial class PersonHrDuty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "HrDuty", table: "people", type: "tinyint(1)", nullable: false, defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HrDuty", table: "people");
        }
    }
}
