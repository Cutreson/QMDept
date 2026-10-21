using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>tasks: GivenById + AckAt — việc do cấp quản lý giao, người nhận xác nhận đã nhận việc (yêu cầu 2026-10-21).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261021140000_TaskAck")]
    public partial class TaskAck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "GivenById", table: "work_tasks", type: "int", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "AckAt", table: "work_tasks", type: "datetime(6)", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "GivenById", table: "work_tasks");
            migrationBuilder.DropColumn(name: "AckAt", table: "work_tasks");
        }
    }
}
