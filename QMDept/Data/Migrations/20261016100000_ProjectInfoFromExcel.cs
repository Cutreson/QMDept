using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>Thông tin dự án / hạng mục nạp từ file "thông tin DA.xlsx" (yêu cầu 2026-10-16): mã dự án, số lượng, Leader phụ trách, PO, giao hàng, cấp độ, loại, ghi chú; PIC + ngày kế hoạch của hạng mục.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261016100000_ProjectInfoFromExcel")]
    public partial class ProjectInfoFromExcel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Category", table: "projects", type: "varchar(40)", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Code", table: "projects", type: "varchar(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "DeliveryDate", table: "projects", type: "date", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Level", table: "projects", type: "varchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Note", table: "projects", type: "varchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<int>(name: "OwnerLeaderId", table: "projects", type: "int", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "PoDate", table: "projects", type: "date", nullable: true);
            migrationBuilder.AddColumn<int>(name: "Quantity", table: "projects", type: "int", nullable: true);

            migrationBuilder.AddColumn<string>(name: "Pic", table: "plan_items", type: "varchar(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "PlanEnd", table: "plan_items", type: "date", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "PlanStart", table: "plan_items", type: "date", nullable: true);

            migrationBuilder.CreateIndex(name: "IX_projects_OwnerLeaderId", table: "projects", column: "OwnerLeaderId");
            migrationBuilder.AddForeignKey(
                name: "FK_projects_people_OwnerLeaderId",
                table: "projects",
                column: "OwnerLeaderId",
                principalTable: "people",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_projects_people_OwnerLeaderId", table: "projects");
            migrationBuilder.DropIndex(name: "IX_projects_OwnerLeaderId", table: "projects");
            foreach (var c in new[] { "Category", "Code", "DeliveryDate", "Level", "Note", "OwnerLeaderId", "PoDate", "Quantity" })
                migrationBuilder.DropColumn(name: c, table: "projects");
            foreach (var c in new[] { "Pic", "PlanEnd", "PlanStart" })
                migrationBuilder.DropColumn(name: c, table: "plan_items");
        }
    }
}
