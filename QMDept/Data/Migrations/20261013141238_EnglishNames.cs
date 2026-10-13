using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace QMDept.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnglishNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photos");

            migrationBuilder.RenameColumn(
                name: "Pc",
                table: "ot_sends",
                newName: "AllowanceCount");

            migrationBuilder.RenameColumn(
                name: "Nhe",
                table: "ot_sends",
                newName: "SnackCount");

            migrationBuilder.RenameColumn(
                name: "Com",
                table: "ot_sends",
                newName: "RiceCount");

            migrationBuilder.RenameColumn(
                name: "Bun",
                table: "ot_sends",
                newName: "NoodleCount");

            migrationBuilder.RenameColumn(
                name: "TpNote",
                table: "day_plans",
                newName: "ManagerNote");

            migrationBuilder.RenameColumn(
                name: "TpDecision",
                table: "day_plans",
                newName: "ManagerDecision");

            migrationBuilder.RenameColumn(
                name: "TpBy",
                table: "day_plans",
                newName: "ManagerBy");

            migrationBuilder.RenameColumn(
                name: "TpAt",
                table: "day_plans",
                newName: "ManagerAt");

            migrationBuilder.CreateTable(
                name: "notify_settings",
                columns: table => new
                {
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisabledTypes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notify_settings", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_notify_settings_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            // ===== Dữ liệu cũ: giá trị enum tiếng Việt → tên tiếng Anh (xem docs/database.md) =====
            var role = "Nv:Staff,Tp:Manager";
            var task = "ChuaLam:NotStarted,DangLam:InProgress,HoanThanh:Done,Cham:Late";
            var decided = "ChoDuyet:Pending,DaDuyet:Approved,TuChoi:Rejected";
            foreach (var (table, column, map) in new[]
            {
                ("people", "Role", role),
                ("projects", "StatusOverride", "Dat:OnTrack,DangLam:InProgress,Cham:Behind,NguyCo:AtRisk,HoanThanh:Completed"),
                ("plan_items", "Status", task),
                ("work_tasks", "Status", task),
                ("work_tasks", "Source", "KeHoach:Planned,NgoaiKeHoach:Unplanned,PhatSinh:Extra,ChuyenTiep:CarryOver,GiaoViec:Assigned"),
                ("day_plans", "Status", "CongTy:AtOffice,CtNgay:DayTrip,CtDai:LongTrip,NghiPhep:PaidLeave,NghiKl:UnpaidLeave"),
                ("day_plans", "Meal", "Cat:None,Com:Rice,Bun:Noodle"),
                ("day_plans", "LeaveType", "Phep:Paid,Kl:Unpaid"),
                ("day_plans", "LeaveSession", "Sang:Morning,Chieu:Afternoon,CaNgay:FullDay"),
                ("day_plans", "SupportScope", "Khong:None,Nhom:Team,NhomKhac:OtherTeam,PhongKhac:OtherDept"),
                ("extras", "Status", decided),
                ("extras", "DecidedRole", role),
                ("overtimes", "Role", role),
                ("overtimes", "DayType", "Lv:Workday,Nghi:Holiday"),
                ("overtimes", "Meal", "Khong:None,Nhe:Snack,Com:Rice,Bun:Noodle"),
                ("overtimes", "Status", decided),
                ("overtimes", "Reg", "DuTru:Early,ChinhThuc:OnTime,TreHan:Late"),
            })
            {
                var cases = string.Join(" ", map.Split(',').Select(p => p.Split(':')).Select(p => $"WHEN '{p[0]}' THEN '{p[1]}'"));
                var olds = string.Join(",", map.Split(',').Select(p => $"'{p.Split(':')[0]}'"));
                migrationBuilder.Sql($"UPDATE `{table}` SET `{column}` = CASE `{column}` {cases} END WHERE `{column}` IN ({olds});");
            }
            // Ảnh chụp báo cáo Leader: khóa JSON dự án kh/tt/st → need/onSite/status
            migrationBuilder.Sql("UPDATE `leader_reports` SET `SummaryJson` = REPLACE(REPLACE(REPLACE(`SummaryJson`, '\"kh\":', '\"need\":'), '\"tt\":', '\"onSite\":'), '\"st\":', '\"status\":');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notify_settings");

            migrationBuilder.RenameColumn(
                name: "AllowanceCount",
                table: "ot_sends",
                newName: "Pc");

            migrationBuilder.RenameColumn(
                name: "SnackCount",
                table: "ot_sends",
                newName: "Nhe");

            migrationBuilder.RenameColumn(
                name: "RiceCount",
                table: "ot_sends",
                newName: "Com");

            migrationBuilder.RenameColumn(
                name: "NoodleCount",
                table: "ot_sends",
                newName: "Bun");

            migrationBuilder.RenameColumn(
                name: "ManagerNote",
                table: "day_plans",
                newName: "TpNote");

            migrationBuilder.RenameColumn(
                name: "ManagerDecision",
                table: "day_plans",
                newName: "TpDecision");

            migrationBuilder.RenameColumn(
                name: "ManagerBy",
                table: "day_plans",
                newName: "TpBy");

            migrationBuilder.RenameColumn(
                name: "ManagerAt",
                table: "day_plans",
                newName: "TpAt");

            migrationBuilder.CreateTable(
                name: "photos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ExtraId = table.Column<int>(type: "int", nullable: true),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StoredName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    UploadedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_photos_extras_ExtraId",
                        column: x => x.ExtraId,
                        principalTable: "extras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_photos_work_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "work_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_photos_ExtraId",
                table: "photos",
                column: "ExtraId");

            migrationBuilder.CreateIndex(
                name: "IX_photos_TaskId",
                table: "photos",
                column: "TaskId");
        }
    }
}
