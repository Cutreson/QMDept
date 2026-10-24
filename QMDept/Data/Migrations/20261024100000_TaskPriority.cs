using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>work_tasks.Priority: mức độ ưu tiên khi giao việc — Normal / High / Urgent (yêu cầu 2026-10-24).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261024100000_TaskPriority")]
    public partial class TaskPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Priority", table: "work_tasks", type: "varchar(10)", maxLength: 10, nullable: false, defaultValue: "Normal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Priority", table: "work_tasks");
        }
    }
}
