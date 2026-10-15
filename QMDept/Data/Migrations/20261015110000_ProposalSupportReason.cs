using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <summary>calendar_proposals.SupportReason: lý do xin người hỗ trợ (yêu cầu 2026-10-15).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261015110000_ProposalSupportReason")]
    public partial class ProposalSupportReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupportReason",
                table: "calendar_proposals",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportReason",
                table: "calendar_proposals");
        }
    }
}
