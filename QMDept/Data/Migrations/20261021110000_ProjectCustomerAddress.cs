using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>projects: địa chỉ khách hàng (CustomerAddress) — yêu cầu 2026-10-21.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261021110000_ProjectCustomerAddress")]
    public partial class ProjectCustomerAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "CustomerAddress", table: "projects", type: "varchar(300)", maxLength: 300, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CustomerAddress", table: "projects");
        }
    }
}
