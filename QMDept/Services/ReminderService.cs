using Microsoft.EntityFrameworkCore;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Nhắc việc tự động (yêu cầu 2026-10-27): cứ 10 phút quét một lần và ĐẨY thông báo (SignalR + hộp thông báo) cho từng nhân sự về những việc họ PHẢI làm mà chưa làm:
/// chưa báo cáo đầu ngày, việc chưa cập nhật kết quả / chưa hoàn thành mà không chuyển việc, chưa gửi báo cáo cuối ngày, việc được giao chưa xác nhận, cập nhật kết quả làm thêm,
/// và (với người duyệt / người giao việc) việc đang chờ duyệt. Mỗi loại nhắc tối đa MỘT lần mỗi ngày cho mỗi người (kiểm tra bằng hộp thông báo nên khởi động lại máy chủ không nhắc lại).
/// Giờ theo <see cref="VnTime"/>, bỏ Chủ nhật, chỉ nhắc trong 07:00–21:30.
/// </summary>
public sealed class ReminderService(IServiceScopeFactory scopes, ILogger<ReminderService> log, IHostEnvironment env, IConfiguration config) : BackgroundService
{
    const string Prefix = "Nhắc việc";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (env.IsDevelopment() && !config.GetValue<bool>("Reminders:Enabled")) return;   // máy chạy thử dùng chung DB thật — không tự đẩy thông báo cho nhân sự
        try { await Task.Delay(TimeSpan.FromSeconds(90), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { log.LogWarning(ex, "Nhắc việc: lỗi khi quét"); }
            try { await Task.Delay(TimeSpan.FromMinutes(10), ct); } catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>Một lượt quét; trả số thông báo đã đẩy. dryRun = chỉ liệt kê vào <paramref name="preview"/> (không gửi, bỏ qua khung giờ) — dùng để kiểm thử ở /Dev/Reminders.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default, bool dryRun = false, List<string>? preview = null)
    {
        var today = VnTime.Today; var hm = VnTime.NowHm;
        if (!dryRun && (today.DayOfWeek == DayOfWeek.Sunday || string.CompareOrdinal(hm, "07:00") < 0 || string.CompareOrdinal(hm, "21:30") > 0)) return 0;
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var notify = sp.GetRequiredService<Notifier>();
        var from = today.AddDays(-7);
        var evening = dryRun || string.CompareOrdinal(hm, "17:00") >= 0;

        var people = await db.People.AsNoTracking().Where(p => !p.IsDeleted).ToListAsync(ct);
        var staffLike = people.Where(p => p.Role is Role.Staff or Role.Leader or Role.Manager).ToList();
        var plans = await db.DayPlans.AsNoTracking().Where(p => p.Date >= from && p.Date <= today).ToListAsync(ct);
        var reports = await db.DayReports.AsNoTracking().Where(r => r.Date >= from && r.Date <= today).ToListAsync(ct);
        var tasks = await db.WorkTasks.AsNoTracking().Where(t => t.Date >= from && t.Date <= today.AddDays(30)).ToListAsync(ct);
        var leaves = await db.Leaves.AsNoTracking().Where(l => l.Date >= from && l.Date <= today).ToListAsync(ct);
        var common = (await db.Projects.AsNoTracking().Select(p => new { p.Id, p.Category }).ToListAsync(ct)).Where(p => p.Category == Project.CommonCategory).Select(p => p.Id).ToHashSet();
        var leads = (await db.Assignments.AsNoTracking().Where(a => a.Role == ProjectRole.Lead).Select(a => new { a.ProjectId, a.EmployeeId }).ToListAsync(ct))
            .Where(a => !common.Contains(a.ProjectId)).GroupBy(a => a.ProjectId).ToDictionary(g => g.Key, g => g.First().EmployeeId);
        var dayStartUtc = today.ToDateTime(TimeOnly.MinValue).AddHours(-7);
        var sent = (await db.InboxItems.AsNoTracking().Where(i => i.Type == nameof(NotifyType.Reminder) && i.CreatedAt >= dayStartUtc).Select(i => new { i.PersonId, i.Title }).ToListAsync(ct))
            .Select(i => (i.PersonId, Kind: i.Title.Split(" · ").ElementAtOrDefault(1) ?? "")).ToHashSet();

        var n = 0;
        async Task Remind(Person p, string kind, string detail, string body, string url)
        {
            if (!sent.Add((p.Id, kind))) return;
            if (dryRun) { preview?.Add($"{p.Name} ({Labels.Title(p.Role)}): {Prefix} · {kind} · {detail}"); n++; return; }
            await notify.SendAsync([p.Id], NotifyType.Reminder, $"{Prefix} · {kind} · {detail}", body, url);
            n++;
        }
        string Own(Person p) => p.Role == Role.Staff ? "" : "&as=mine";
        string Page(Person p, string path, DateOnly d) => $"{path}?d={Fmt.Key(d)}{Own(p)}";
        bool FullLeave(int emp, DateOnly d) => leaves.Any(l => l.EmployeeId == emp && l.Date == d && Dept.IsFullDayLeave(l));
        bool IsSupport(WorkTask t) => leads.TryGetValue(t.ProjectId, out var l) && l != t.EmployeeId;

        foreach (var p in staffLike)
        {
            // 1. chưa báo cáo đầu ngày
            if (!plans.Any(x => x.EmployeeId == p.Id && x.Date == today) && !FullLeave(p.Id, today))
                await Remind(p, "chưa báo cáo đầu ngày", Fmt.Dmy(today), "Hãy báo cáo trạng thái làm việc và công việc hôm nay.", Page(p, "/Staff/Morning", today));

            // 2. việc chưa cập nhật kết quả / chưa hoàn thành mà không chuyển việc; 3. chưa gửi báo cáo cuối ngày
            var days = Enumerable.Range(0, 8).Select(i => today.AddDays(-i)).Where(d => d.DayOfWeek != DayOfWeek.Sunday && (d < today || evening)).Where(d => !FullLeave(p.Id, d)).OrderBy(d => d).ToList();
            var open = new List<WorkTask>();
            var noReport = new List<DateOnly>();
            foreach (var d in days)
            {
                var own = tasks.Where(t => t.EmployeeId == p.Id && t.Date == d && (t.Selected || p.Role == Role.Manager)).ToList();
                if (p.Role == Role.Manager) open.AddRange(own.Where(t => !t.NeedsGiverReview && t.Status != TaskState.Done && t.CarriedToId == null));
                else
                {
                    open.AddRange(own.Where(t => IsSupport(t) ? !t.Supported : t.Status != TaskState.Done && t.CarriedToId == null));
                    if (own.Count > 0 && plans.Any(x => x.EmployeeId == p.Id && x.Date == d && !LeaveRules.IsLeave(x.Status)) && !reports.Any(r => r.EmployeeId == p.Id && r.Date == d)) noReport.Add(d);
                }
            }
            if (open.Count > 0)
            {
                var ds = open.Select(t => t.Date).Distinct().OrderBy(x => x).ToList();
                var what = p.Role == Role.Manager ? "việc của bạn chưa cập nhật kết quả (Hoàn thành / chuyển sang ngày khác)" : "việc chưa hoàn thành hoặc chưa cập nhật mà chưa chuyển sang kế hoạch tiếp theo";
                await Remind(p, "việc chưa cập nhật kết quả", $"{open.Count} {what} — ngày {string.Join(", ", ds.Take(4).Select(x => Fmt.Dmy(x)))}",
                    string.Join("; ", open.Take(4).Select(t => t.Title)), Page(p, p.Role == Role.Manager ? "/Staff/Morning" : "/Staff/Evening", ds[0]));
            }
            if (noReport.Count > 0)
                await Remind(p, "chưa gửi báo cáo cuối ngày", $"ngày {string.Join(", ", noReport.Take(4).Select(x => Fmt.Dmy(x)))}", "Hãy cập nhật kết quả từng việc rồi gửi báo cáo cuối ngày.", Page(p, "/Staff/Evening", noReport[0]));

            // 4. việc được giao chưa xác nhận
            var unack = tasks.Where(t => t.EmployeeId == p.Id && t.GivenById != null && t.AckAt == null && t.Date >= from).OrderBy(t => t.Date).ToList();
            if (unack.Count > 0)
                await Remind(p, "việc được giao chưa xác nhận", $"{unack.Count} việc — ngày {string.Join(", ", unack.Select(t => t.Date).Distinct().Take(4).Select(x => Fmt.Dmy(x)))}",
                    string.Join("; ", unack.Take(4).Select(t => $"{t.Title} (giao bởi {t.GivenBy})")), Page(p, "/Staff/Month", unack[0].Date));
        }

        // 5. cập nhật kết quả làm thêm đã được duyệt (tới giờ làm mà chưa cập nhật hoặc bị từ chối kết quả)
        var ots = await db.Overtimes.AsNoTracking().Where(o => o.Date >= from && o.Date <= today && o.Status == OtStatus.Approved && (o.Completion == OtCompletion.None || o.Completion == OtCompletion.Rejected)).ToListAsync(ct);
        foreach (var g in ots.Where(o => o.Date < today || string.CompareOrdinal(hm, o.End) >= 0).GroupBy(o => o.PersonId))
            if (staffLike.Find(x => x.Id == g.Key) is { } pp)
                await Remind(pp, "cập nhật kết quả làm thêm", $"ngày {string.Join(", ", g.Select(o => o.Date).Distinct().OrderBy(x => x).Take(4).Select(x => Fmt.Dmy(x)))}", "Hãy cập nhật công việc đã triển khai và giờ làm thêm thực tế.", $"/Overtime?tab=mine&d={Fmt.Key(g.Min(o => o.Date))}{Own(pp)}");

        // 6. người DUYỆT: việc đang chờ duyệt
        var pend = await db.Overtimes.AsNoTracking().Where(o => o.Date >= from && (o.Status == OtStatus.Pending || o.Status == OtStatus.PendingManager)).ToListAsync(ct);
        var leavesP = await db.LeaveRequests.AsNoTracking().Where(l => l.Status == ExtraStatus.Pending).ToListAsync(ct);
        var propsP = await db.CalendarProposals.AsNoTracking().Where(c => c.Status == ExtraStatus.Pending).ToListAsync(ct);
        var extrasP = await db.Extras.AsNoTracking().Where(e => e.Status == ExtraStatus.Pending && e.Date >= from).ToListAsync(ct);
        var supP = await db.SupportRequests.AsNoTracking().Where(s => !s.Confirmed && !s.Declined).ToListAsync(ct);
        foreach (var lead in people.Where(x => x.Role == Role.Leader))
        {
            var team = people.Where(x => x.Role == Role.Staff && x.LeaderId == lead.Id).Select(x => x.Id).ToHashSet();
            var c = plans.Count(x => team.Contains(x.EmployeeId) && x.LeaderDecision == null) + reports.Count(x => team.Contains(x.EmployeeId) && x.LeaderDecision == null)
                + pend.Count(o => o.Status == OtStatus.Pending && team.Contains(o.PersonId)) + leavesP.Count(l => team.Contains(l.PersonId)) + propsP.Count(x => team.Contains(x.ProposerId))
                + extrasP.Count(e => team.Contains(e.EmployeeId)) + supP.Count(s => s.GroupLeaderId == lead.Id);
            if (c > 0) await Remind(lead, "việc chờ bạn duyệt", $"{c} việc của nhóm đang chờ duyệt (kế hoạch, báo cáo, đề xuất, xin nghỉ, làm thêm, hỗ trợ nhân sự)", "Vào Kế hoạch & duyệt để xử lý.", "/Planning/Proposals?as=me");
        }
        var leaderIds = people.Where(x => x.Role == Role.Leader).Select(x => x.Id).ToHashSet();
        var cm = plans.Count(x => leaderIds.Contains(x.EmployeeId) && x.LeaderDecision == null) + reports.Count(x => leaderIds.Contains(x.EmployeeId) && x.LeaderDecision == null) + leavesP.Count(l => leaderIds.Contains(l.PersonId))
            + pend.Count(o => (o.Status == OtStatus.Pending && leaderIds.Contains(o.PersonId)) || o.Status == OtStatus.PendingManager);
        if (cm > 0)
            foreach (var m in people.Where(x => x.Role is Role.Manager or Role.Admin))
                await Remind(m, "việc chờ bạn duyệt", $"{cm} việc đang chờ bạn duyệt (trạng thái làm việc + báo cáo cuối ngày của Leader, xin nghỉ, đăng ký làm thêm của nhân sự)", "Vào Phê duyệt để xử lý.", "/Manager/Approvals");

        // 7. người GIAO việc: việc đã báo hoàn thành / đề xuất lại đang chờ duyệt
        foreach (var g in tasks.Where(t => t.GivenById != null && (t.DonePending || t.RePending)).GroupBy(t => t.GivenById!.Value))
            if (people.Find(x => x.Id == g.Key) is { } giver)
                await Remind(giver, "việc bạn giao chờ duyệt", $"{g.Count()} việc đã báo hoàn thành hoặc đề xuất lại", "Duyệt ở khung đỏ đầu trang.", "/Staff/Month?as=mine");
        return n;
    }
}
