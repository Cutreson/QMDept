using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>Thêm work_tasks.Supported: người hỗ trợ tích "đã hỗ trợ" (Leader dự án chịu trách nhiệm báo cáo kết quả).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261014120000_TaskSupported")]
    public partial class TaskSupported : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Supported",
                table: "work_tasks",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Supported",
                table: "work_tasks");
        }
    }
}
