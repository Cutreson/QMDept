using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>work_tasks: nghiệm thu việc được giao — DonePending / ItemDoneClaim / DoneReviewBy / DoneReviewAt / DoneRejectNote (yêu cầu 2026-10-24).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261024120000_TaskDoneReview")]
    public partial class TaskDoneReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "DonePending", table: "work_tasks", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "ItemDoneClaim", table: "work_tasks", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "DoneReviewBy", table: "work_tasks", type: "varchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "DoneReviewAt", table: "work_tasks", type: "datetime(6)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "DoneRejectNote", table: "work_tasks", type: "varchar(500)", maxLength: 500, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DonePending", table: "work_tasks");
            migrationBuilder.DropColumn(name: "ItemDoneClaim", table: "work_tasks");
            migrationBuilder.DropColumn(name: "DoneReviewBy", table: "work_tasks");
            migrationBuilder.DropColumn(name: "DoneReviewAt", table: "work_tasks");
            migrationBuilder.DropColumn(name: "DoneRejectNote", table: "work_tasks");
        }
    }
}
