using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>projects.OwnerLeader2Id: Leader phụ trách thứ 2 của dự án, không bắt buộc (yêu cầu 2026-10-27).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261027100000_ProjectOwner2")]
    public partial class ProjectOwner2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "OwnerLeader2Id", table: "projects", type: "int", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OwnerLeader2Id", table: "projects");
        }
    }
}
