using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>Thêm work_tasks.SupportDone / SupportNote: người hỗ trợ báo phần hỗ trợ của mình hoàn thành hay chưa (kèm lý do).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261104100000_SupportResult")]
    public partial class SupportResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SupportDone",
                table: "work_tasks",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportNote",
                table: "work_tasks",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SupportDone", table: "work_tasks");
            migrationBuilder.DropColumn(name: "SupportNote", table: "work_tasks");
        }
    }
}
