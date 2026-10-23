using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>inbox_items: hộp thông báo lưu lại cho từng tài khoản (yêu cầu 2026-10-23).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261023100000_InboxItems")]
    public partial class InboxItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbox_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Url = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Bad = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_inbox_items", x => x.Id));
            migrationBuilder.CreateIndex(name: "IX_inbox_items_PersonId_ReadAt", table: "inbox_items", columns: new[] { "PersonId", "ReadAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "inbox_items");
        }
    }
}
