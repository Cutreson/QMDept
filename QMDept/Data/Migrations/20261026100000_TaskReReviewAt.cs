using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>work_tasks.ReReviewAt: giờ người giao đồng ý / không đồng ý đề xuất lại (yêu cầu 2026-10-26: hiện giờ gửi / đề xuất / duyệt ở mọi nội dung).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261026100000_TaskReReviewAt")]
    public partial class TaskReReviewAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "ReReviewAt", table: "work_tasks", type: "datetime(6)", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReReviewAt", table: "work_tasks");
        }
    }
}
