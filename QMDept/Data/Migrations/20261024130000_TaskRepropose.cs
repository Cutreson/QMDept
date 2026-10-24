using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>work_tasks: đề xuất lại nội dung / thời gian việc được giao — RePending, ReTitle, ReDate, ReStart, ReEnd, ReReason, ReAt, ReRejectNote, ReReviewBy (yêu cầu 2026-10-24).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261024130000_TaskRepropose")]
    public partial class TaskRepropose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "RePending", table: "work_tasks", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "ReTitle", table: "work_tasks", type: "varchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<DateOnly>(name: "ReDate", table: "work_tasks", type: "date", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReStart", table: "work_tasks", type: "varchar(5)", maxLength: 5, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReEnd", table: "work_tasks", type: "varchar(5)", maxLength: 5, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReReason", table: "work_tasks", type: "varchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "ReAt", table: "work_tasks", type: "datetime(6)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReRejectNote", table: "work_tasks", type: "varchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReReviewBy", table: "work_tasks", type: "varchar(100)", maxLength: 100, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var c in new[] { "RePending", "ReTitle", "ReDate", "ReStart", "ReEnd", "ReReason", "ReAt", "ReRejectNote", "ReReviewBy" })
                migrationBuilder.DropColumn(name: c, table: "work_tasks");
        }
    }
}
