using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>plan_items.Note: ghi chú / nguyên nhân chậm của hạng mục (nạp từ file "thông tin DA.xlsx").</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261016110000_PlanItemNote")]
    public partial class PlanItemNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Note", table: "plan_items", type: "varchar(500)", maxLength: 500, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Note", table: "plan_items");
        }
    }
}
