using Microsoft.EntityFrameworkCore;

namespace QMDept.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<PlanItem> PlanItems => Set<PlanItem>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<DayPlan> DayPlans => Set<DayPlan>();
    public DbSet<DayPlanTask> DayPlanTasks => Set<DayPlanTask>();
    public DbSet<DayReport> DayReports => Set<DayReport>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<ProjectNeed> ProjectNeeds => Set<ProjectNeed>();
    public DbSet<Extra> Extras => Set<Extra>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<LeaderReport> LeaderReports => Set<LeaderReport>();
    public DbSet<Overtime> Overtimes => Set<Overtime>();
    public DbSet<OvertimeTask> OvertimeTasks => Set<OvertimeTask>();
    public DbSet<OtSend> OtSends => Set<OtSend>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Enum lưu dạng chuỗi để đọc DB trực tiếp vẫn hiểu.
        foreach (var t in typeof(Role).Assembly.GetTypes().Where(t => t.IsEnum && t.Namespace == typeof(Role).Namespace))
            b.Properties(t).HaveConversion<string>().HaveMaxLength(20);
        b.Properties<decimal>().HavePrecision(9, 2);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Person>(e =>
        {
            e.ToTable("people");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Leader).WithMany(x => x.Members).HasForeignKey(x => x.LeaderId).OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<Project>(e => e.ToTable("projects"));

        m.Entity<PlanItem>(e =>
        {
            e.ToTable("plan_items");
            e.HasOne(x => x.Project).WithMany(x => x.Items).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Assignment>(e =>
        {
            e.ToTable("assignments");
            e.HasIndex(x => new { x.ProjectId, x.EmployeeId }).IsUnique();
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<WorkTask>(e =>
        {
            e.ToTable("work_tasks");
            e.HasIndex(x => new { x.Date, x.EmployeeId });
            e.HasIndex(x => x.FromDate);
            e.HasIndex(x => x.ExtraId);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PlanItem).WithMany().HasForeignKey(x => x.PlanItemId).OnDelete(DeleteBehavior.SetNull);
            // CarriedFromId / CarriedToId: tham chiếu mềm (không ràng buộc FK) để xóa việc chuyển tiếp không bị chặn;
            // code luôn tự dọn tham chiếu khi xóa (xem WorkflowService.DeleteTaskAsync).
        });

        m.Entity<DayPlan>(e =>
        {
            e.ToTable("day_plans");
            e.HasIndex(x => new { x.Date, x.EmployeeId }).IsUnique();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<DayPlanTask>(e =>
        {
            e.ToTable("day_plan_tasks");
            e.HasKey(x => new { x.DayPlanId, x.WorkTaskId });
            e.HasOne(x => x.DayPlan).WithMany(x => x.PlanTasks).HasForeignKey(x => x.DayPlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.WorkTask).WithMany().HasForeignKey(x => x.WorkTaskId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<DayReport>(e =>
        {
            e.ToTable("day_reports");
            e.HasIndex(x => new { x.Date, x.EmployeeId }).IsUnique();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<Leave>(e =>
        {
            e.ToTable("leaves");
            e.HasIndex(x => new { x.Date, x.EmployeeId }).IsUnique();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<ProjectNeed>(e =>
        {
            e.ToTable("project_needs");
            e.HasIndex(x => new { x.Date, x.ProjectId }).IsUnique();
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Extra>(e =>
        {
            e.ToTable("extras");
            e.HasIndex(x => x.Date);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<Photo>(e =>
        {
            e.ToTable("photos");
            e.HasOne(x => x.Task).WithMany(x => x.Photos).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Extra).WithMany(x => x.Photos).HasForeignKey(x => x.ExtraId).OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<LeaderReport>(e =>
        {
            e.ToTable("leader_reports");
            e.HasIndex(x => new { x.Date, x.LeaderId }).IsUnique();
            e.HasOne(x => x.Leader).WithMany().HasForeignKey(x => x.LeaderId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SummaryJson).HasColumnType("longtext");
        });

        m.Entity<Overtime>(e =>
        {
            e.ToTable("overtimes");
            e.HasIndex(x => new { x.Date, x.PersonId }).IsUnique();
            e.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<OvertimeTask>(e =>
        {
            e.ToTable("overtime_tasks");
            e.HasOne(x => x.Overtime).WithMany(x => x.Items).HasForeignKey(x => x.OvertimeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.WorkTask).WithMany().HasForeignKey(x => x.WorkTaskId).OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<OtSend>(e =>
        {
            e.ToTable("ot_sends");
            e.HasIndex(x => x.Date).IsUnique();
        });

        // Tiếng Việt: database + mọi bảng luôn utf8mb4 bất kể charset mặc định của server MySQL.
        // (Đặt ở cấp model; KHÔNG dùng ForMySQLHasCharset theo entity vì file migration provider sinh ra sẽ thiếu using.)
        m.HasCharSet("utf8mb4");
    }
}
