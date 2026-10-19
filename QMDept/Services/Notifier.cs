using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Hub SignalR "/hubs/notify" — mỗi tài khoản nhận thông báo theo Clients.User(PersonId) (UserIdentifier = claim NameIdentifier).
/// Client: wwwroot/js/site.js (phần "thông báo đẩy"), thư viện wwwroot/lib/microsoft-signalr.
/// </summary>
[Authorize]
public sealed class NotifyHub : Hub
{
    /// <summary>Kết nối đang mở: connectionId → (người, SecurityStamp lúc kết nối, context để ngắt). Notifier chỉ gửi tới
    /// kết nối có stamp còn khớp — tab mở từ phiên đã bị thu hồi (đổi mật khẩu / xóa tài khoản) không nhận gì và bị ngắt.</summary>
    internal static readonly ConcurrentDictionary<string, (int PersonId, string? Stamp, HubCallerContext Ctx)> Open = new();

    public override Task OnConnectedAsync()
    {
        if (int.TryParse(Context.UserIdentifier, out var id))
            Open[Context.ConnectionId] = (id, Context.User?.FindFirst(ScopeResolver.StampClaim)?.Value, Context);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Open.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>Danh mục loại thông báo: nhãn, mô tả, vai trò nhận (để trang Cài đặt chỉ hiện loại liên quan).</summary>
public static class NotifyCatalog
{
    public sealed record Item(NotifyType Type, string Label, string Description, Role[] Roles);

    public static readonly Item[] All =
    [
        new(NotifyType.PlanSubmitted, "Kế hoạch đầu ngày mới", "Nhân viên trong nhóm gửi / gửi lại kế hoạch đầu ngày (Leader gửi trạng thái làm việc thì Trưởng phòng nhận)", [Role.Leader, Role.Manager]),
        new(NotifyType.ReportSubmitted, "Báo cáo cuối ngày mới", "Nhân viên trong nhóm gửi báo cáo cuối ngày", [Role.Leader]),
        new(NotifyType.ExtraProposed, "Đề xuất phát sinh mới", "Nhân viên trong nhóm đề xuất công việc phát sinh", [Role.Leader]),
        new(NotifyType.SupportNeedsManager, "Yêu cầu hỗ trợ nhân sự", "Kế hoạch cần Trưởng phòng duyệt hỗ trợ nhân sự nhóm khác / phòng ban khác", [Role.Manager]),
        new(NotifyType.LeaderReportSent, "Báo cáo ngày của Leader", "Leader gửi báo cáo ngày cho Trưởng phòng", [Role.Manager]),
        new(NotifyType.OvertimeSubmitted, "Đăng ký làm thêm cần duyệt", "Nhân viên (với Leader) hoặc Leader (với TP) đăng ký làm thêm", [Role.Leader, Role.Manager]),
        new(NotifyType.OvertimeCancelled, "Hủy đăng ký làm thêm", "Nhân viên (với Leader) hoặc Leader (với TP) hủy đăng ký làm thêm", [Role.Leader, Role.Manager]),
        new(NotifyType.CarryProposed, "Đề xuất chuyển tiếp mới", "Nhân viên đề xuất / sửa việc chuyển tiếp sau khi báo cáo cuối ngày đã được duyệt", [Role.Leader]),
        new(NotifyType.PlanDecided, "Kết quả duyệt kế hoạch", "Kế hoạch đầu ngày của bạn được duyệt / từ chối / góp ý", [Role.Staff, Role.Leader]),
        new(NotifyType.ReportDecided, "Kết quả duyệt báo cáo", "Báo cáo cuối ngày của bạn được duyệt / yêu cầu bổ sung", [Role.Staff]),
        new(NotifyType.ExtraDecided, "Kết quả phát sinh", "Phát sinh của bạn được duyệt / từ chối, Leader thêm phát sinh cho bạn", [Role.Staff]),
        new(NotifyType.CarryOver, "Kế hoạch chuyển tiếp", "Việc chuyển tiếp của bạn được duyệt / chỉnh / hoàn tác", [Role.Staff]),
        new(NotifyType.ProjectOwner, "Dự án giao nhóm phụ trách", "Trưởng phòng giao / bỏ giao một dự án cho nhóm của bạn phụ trách", [Role.Leader]),
        new(NotifyType.TeamChanged, "Đổi nhóm trên sơ đồ nhân sự", "Trưởng phòng chuyển nhân sự sang nhóm khác (bạn hoặc nhân sự trong nhóm bạn)", [Role.Staff, Role.Leader]),
        new(NotifyType.CalendarProposed, "Đề xuất lịch mới", "Nhân viên trong nhóm đề xuất thêm công việc / người hỗ trợ ở lịch tháng", [Role.Leader]),
        new(NotifyType.CalendarDecided, "Kết quả đề xuất lịch", "Đề xuất lịch của bạn được duyệt / từ chối", [Role.Staff]),
        new(NotifyType.ProjectLead, "Leader dự án", "Bạn được chọn làm Leader dự án hoặc không còn là Leader dự án", [Role.Staff]),
        new(NotifyType.TaskAssigned, "Giao việc", "Bạn được giao việc, có việc mới hoặc bị xóa khỏi lịch", [Role.Staff]),
        new(NotifyType.LeaveMarked, "Báo nghỉ", "Leader ghi nghỉ / bỏ nghỉ cho bạn", [Role.Staff]),
        new(NotifyType.OvertimeDecided, "Kết quả làm thêm", "Đăng ký làm thêm của bạn được duyệt / từ chối / hoàn tác", [Role.Staff, Role.Leader]),
    ];

    public static Item Get(NotifyType t) => All.First(i => i.Type == t);
    public static IEnumerable<Item> ForRole(Role r) => All.Where(i => i.Roles.Contains(ScopeResolver.UiRoleOf(r)));
}

/// <summary>
/// Gửi thông báo đẩy sau khi một thao tác ghi thành công. KHÔNG BAO GIỜ ném lỗi ra ngoài (lỗi chỉ ghi log) để không
/// làm hỏng thao tác chính. Bỏ qua người tự thao tác và người đã tắt loại thông báo đó (NotifySetting).
/// Controller gọi qua thuộc tính <c>Notify</c> của AppController, SAU khi SaveChanges thành công.
/// </summary>
public sealed class Notifier(IHubContext<NotifyHub> hub, ViewScope scope, DataCache cache, ILogger<Notifier> log)
{
    string Actor => scope.Resolved ? scope.ActorName : "Hệ thống";
    static string Q(string path, DateOnly d) => $"{path}{(path.Contains('?') ? "&" : "?")}d={Fmt.Key(d)}&as=me";

    /// <summary>Gửi một thông báo tới danh sách người nhận (lọc theo cài đặt từng người).</summary>
    public async Task SendAsync(IEnumerable<int?> to, NotifyType type, string title, string? body, string url)
    {
        try
        {
            var me = scope.Resolved ? scope.Account.Id : 0;
            var ids = to.OfType<int>().Where(i => i != me).Distinct().ToList();
            if (ids.Count == 0) return;
            var snap = await cache.GetBaseAsync();
            var targets = ids.Where(i => !snap.Notify.TryGetValue(i, out var s) || s.Allows(type)).ToHashSet();
            if (targets.Count == 0) return;
            // Chỉ gửi tới kết nối của phiên còn hợp lệ (stamp khớp, tài khoản chưa xóa); kết nối cũ thì ngắt.
            var conns = new List<string>();
            foreach (var (cid, c) in NotifyHub.Open)
            {
                if (!targets.Contains(c.PersonId)) continue;
                var p = snap.People.Find(x => x.Id == c.PersonId);
                if (p is { IsDeleted: false } && p.SecurityStamp == c.Stamp) conns.Add(cid);
                else if (NotifyHub.Open.TryRemove(cid, out _)) c.Ctx.Abort();
            }
            if (conns.Count == 0) return;
            await hub.Clients.Clients(conns).SendAsync("notify", new
            {
                type = type.ToString(), label = NotifyCatalog.Get(type).Label, title, body, url, at = VnTime.NowHm,
            });
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Không gửi được thông báo {Type}", type);
        }
    }

    // Đọc người từ DataCache (không tốn truy vấn DB; cache tự làm mới sau mỗi lệnh ghi).
    async Task<List<Person>> People() => (await cache.GetBaseAsync()).People;
    async Task<int?> LeaderOf(int emp) => (await People()).Find(p => p.Id == emp)?.LeaderId;
    // Quản trị bao trùm chức năng TP → cũng nhận thông báo của TP.
    async Task<List<int?>> Managers() => (await People()).Where(p => (p.Role == Role.Manager || p.Role == Role.Admin) && !p.IsDeleted).Select(p => (int?)p.Id).ToList();
    async Task<bool> IsLeader(int id) => (await People()).Find(p => p.Id == id)?.Role == Role.Leader;
    async Task<string> Name(int id) => (await People()).Find(p => p.Id == id)?.Name ?? "—";

    // ======================= Sự kiện nghiệp vụ =======================

    /// <summary>NV gửi / gửi lại kế hoạch đầu ngày → Leader nhóm.</summary>
    public async Task PlanSubmittedAsync(int emp, DateOnly d)
    {
        // Leader nhóm tự gửi kế hoạch của mình → Trưởng phòng duyệt (yêu cầu 2026-10-16).
        if (await IsLeader(emp)) { await SendAsync(await Managers(), NotifyType.PlanSubmitted, $"{await Name(emp)} (Leader) báo trạng thái làm việc ngày {Fmt.Dm(d)}", "Trạng thái làm việc của Leader đang chờ bạn duyệt.", Q("/Manager/Approvals?mode=leader", d)); return; }
        await SendAsync([await LeaderOf(emp)], NotifyType.PlanSubmitted, $"{await Name(emp)} gửi kế hoạch ngày {Fmt.Dm(d)}", "Kế hoạch đầu ngày đang chờ bạn duyệt.", Q("/Planning/Approvals?ap=pending", d));
    }

    /// <summary>Kết quả kế hoạch (what: "đã duyệt", "từ chối", "góp ý"…) → NV (+ Leader nhóm nếu do TP quyết định).</summary>
    public async Task PlanDecidedAsync(int emp, DateOnly d, string what, string? note = null, bool alsoLeader = false)
    {
        await SendAsync([emp], NotifyType.PlanDecided, $"Kế hoạch ngày {Fmt.Dm(d)}: {what}", Body(note), Q("/Staff/Morning", d));
        if (alsoLeader)
            await SendAsync([await LeaderOf(emp)], NotifyType.PlanDecided, $"Kế hoạch của {await Name(emp)} ngày {Fmt.Dm(d)}: {what}", Body(note), Q("/Planning/Approvals", d));
    }

    /// <summary>Kế hoạch cần TP duyệt hỗ trợ nhân sự → mọi TP.</summary>
    public async Task SupportNeedsManagerAsync(int emp, DateOnly d) =>
        await SendAsync(await Managers(), NotifyType.SupportNeedsManager, $"{await Name(emp)} cần hỗ trợ nhân sự ngày {Fmt.Dm(d)}", "Leader đã duyệt, đang chờ Trưởng phòng duyệt.", Q("/Manager/Approvals", d));

    /// <summary>NV gửi báo cáo cuối ngày → Leader nhóm.</summary>
    public async Task ReportSubmittedAsync(int emp, DateOnly d)
    {
        await SendAsync([await LeaderOf(emp)], NotifyType.ReportSubmitted, $"{await Name(emp)} gửi báo cáo cuối ngày {Fmt.Dm(d)}", "Báo cáo đang chờ bạn duyệt.", Q("/Planning/Reports", d));
    }

    /// <summary>Kết quả báo cáo cuối ngày → NV.</summary>
    public async Task ReportDecidedAsync(int emp, DateOnly d, bool ok, string? note = null) =>
        await SendAsync([emp], NotifyType.ReportDecided, $"Báo cáo cuối ngày {Fmt.Dm(d)}: {(ok ? "đã duyệt" : "yêu cầu bổ sung")}", Body(note), Q("/Staff/Evening", d));

    /// <summary>Leader hoàn tác quyết định báo cáo cuối ngày → NV.</summary>
    public async Task ReportUndoneAsync(int emp, DateOnly d) =>
        await SendAsync([emp], NotifyType.ReportDecided, $"Báo cáo cuối ngày {Fmt.Dm(d)}: Leader hoàn tác quyết định, chờ duyệt lại", null, Q("/Staff/Evening", d));

    /// <summary>NV đề xuất phát sinh → Leader nhóm.</summary>
    public async Task ExtraProposedAsync(int emp, DateOnly d, string title) =>
        await SendAsync([await LeaderOf(emp)], NotifyType.ExtraProposed, $"{await Name(emp)} đề xuất phát sinh", title, Q("/Planning/Approvals", d));

    /// <summary>Kết quả phát sinh (what: "đã duyệt", "từ chối", "Leader thêm phát sinh") → NV.</summary>
    public async Task ExtraDecidedAsync(int emp, DateOnly d, string title, string what, string? note = null) =>
        await SendAsync([emp], NotifyType.ExtraDecided, $"Phát sinh {what}: {title}", Body(note), Q("/Staff/Evening", d));

    /// <summary>Kế hoạch chuyển tiếp (what: "đã duyệt", "đã chỉnh và duyệt", "bị hoàn tác") → NV.</summary>
    public async Task CarryOverAsync(int emp, DateOnly date, string title, string what) =>
        await SendAsync([emp], NotifyType.CarryOver, $"Việc chuyển tiếp {what}", $"{title} · {Fmt.Short(date)}", Q("/Staff/Month", date));

    /// <summary>Giao việc / thêm / xóa việc trong lịch (what: "Bạn được giao việc", "Việc mới trong lịch", "Việc bị xóa khỏi lịch") → NV.</summary>
    public async Task TaskAssignedAsync(IEnumerable<int> emps, DateOnly d, string title, string what = "Bạn được giao việc") =>
        await SendAsync(emps.Cast<int?>(), NotifyType.TaskAssigned, $"{what} ngày {Fmt.Dm(d)}", $"{title} · bởi {Actor}", Q("/Staff/Month", d));

    /// <summary>NV đề xuất lịch (công việc / người hỗ trợ) → Leader nhóm của người đề xuất.</summary>
    public async Task CalendarProposedAsync(int proposer, DateOnly d, string title) =>
        await SendAsync([await LeaderOf(proposer)], NotifyType.CalendarProposed, $"{await Name(proposer)} đề xuất lịch ngày {Fmt.Dm(d)}", title, Q("/Planning/Proposals", d));

    /// <summary>Đề xuất lịch được duyệt / từ chối → NV đề xuất.</summary>
    public async Task CalendarDecidedAsync(int proposer, DateOnly d, string title, bool approved, string? note) =>
        await SendAsync([proposer], NotifyType.CalendarDecided, $"Đề xuất lịch ngày {Fmt.Dm(d)} {(approved ? "đã được duyệt" : "bị từ chối")}",
            string.IsNullOrWhiteSpace(note) ? $"{title} · bởi {Actor}" : $"{title} — {note.Trim()} · bởi {Actor}", Q("/Staff/Month", d));

    /// <summary>TP giao / bỏ giao dự án cho nhóm Leader phụ trách → Leader đó.</summary>
    /// <summary>TP chuyển NV sang nhóm khác trên sơ đồ nhân sự (yêu cầu 2026-10-19) → NV, Leader cũ và Leader mới.</summary>
    public async Task TeamChangedAsync(int emp, int? oldLeader, int? newLeader)
    {
        var name = await Name(emp);
        var to = newLeader is int nl ? $"nhóm {await Name(nl)}" : "trạng thái chưa xếp nhóm";
        await SendAsync([emp], NotifyType.TeamChanged, $"Bạn được chuyển sang {to}", $"Bởi {Actor}", "/Staff/Month?as=me");
        await SendAsync([newLeader], NotifyType.TeamChanged, $"{name} được chuyển vào nhóm của bạn", $"Bởi {Actor}", "/Planning/People?as=me");
        await SendAsync([oldLeader], NotifyType.TeamChanged, $"{name} được chuyển sang {to}", $"Bởi {Actor}", "/Planning/People?as=me");
    }

    public async Task ProjectOwnerAsync(int leader, string project, bool assigned) =>
        await SendAsync([leader], NotifyType.ProjectOwner, assigned ? $"Dự án {project} giao cho nhóm bạn phụ trách" : $"Dự án {project} không còn do nhóm bạn phụ trách",
            $"bởi {Actor}", "/Planning/ProjectLeads?as=me");

    /// <summary>Được chọn / không còn là Leader dự án → NV (người nhận thấy dự án ở "Thông tin dự án").</summary>
    public async Task ProjectLeadAsync(int emp, string project, bool chosen) =>
        await SendAsync([emp], NotifyType.ProjectLead, chosen ? $"Bạn được chọn làm Leader dự án {project}" : $"Bạn không còn là Leader dự án {project}",
            $"bởi {Actor}", "/Staff/Projects?as=me");

    /// <summary>Leader ghi nghỉ / bỏ nghỉ → NV.</summary>
    public async Task LeaveMarkedAsync(int emp, DateOnly d, bool on, string? reason = null) =>
        await SendAsync([emp], NotifyType.LeaveMarked, on ? $"Bạn được ghi nghỉ ngày {Fmt.Dm(d)}" : $"Đã bỏ ghi nghỉ ngày {Fmt.Dm(d)}", Body(reason), Q("/Staff/Month", d));

    /// <summary>Đăng ký làm thêm cần duyệt → Leader nhóm (NV đăng ký) / mọi TP (Leader đăng ký).</summary>
    public async Task OvertimeSubmittedAsync(int person, Role role, DateOnly d, string start, string end)
    {
        if (role == Role.Manager) return;
        var to = role == Role.Staff ? [await LeaderOf(person)] : await Managers();
        await SendAsync(to, NotifyType.OvertimeSubmitted, $"{await Name(person)} đăng ký làm thêm {Fmt.Dm(d)}", $"{start}–{end}", Q("/Overtime?tab=approve", d));
    }

    /// <summary>NV / Leader cập nhật kết quả làm thêm (giờ thực tế + công việc) → người duyệt (Leader nhóm / mọi TP).</summary>
    public async Task OvertimeDoneSubmittedAsync(int person, Role role, DateOnly d, string start, string end)
    {
        if (role == Role.Manager) return;
        var to = role == Role.Staff ? [await LeaderOf(person)] : await Managers();
        await SendAsync(to, NotifyType.OvertimeSubmitted, $"{await Name(person)} cập nhật kết quả làm thêm {Fmt.Dm(d)}", $"Giờ thực tế {start}–{end}, chờ bạn duyệt kết quả.", Q("/Overtime?tab=approve", d));
    }

    /// <summary>Hủy đăng ký làm thêm (kể cả đã duyệt) → người duyệt (Leader nhóm / mọi TP).</summary>
    public async Task OvertimeCancelledAsync(int person, Role role, DateOnly d, string start, string end, bool wasApproved)
    {
        if (role == Role.Manager) return;
        var to = role == Role.Staff ? [await LeaderOf(person)] : await Managers();
        await SendAsync(to, NotifyType.OvertimeCancelled, $"{await Name(person)} hủy đăng ký làm thêm {Fmt.Dm(d)}",
            $"{start}–{end}{(wasApproved ? " · đã duyệt trước đó" : "")}", Q("/Overtime?tab=approve", d));
    }

    /// <summary>NV đề xuất / sửa việc chuyển tiếp khi báo cáo cuối ngày đã được duyệt → Leader nhóm.</summary>
    public async Task CarryProposedAsync(int emp, DateOnly from, string title, DateOnly to) =>
        await SendAsync([await LeaderOf(emp)], NotifyType.CarryProposed, $"{await Name(emp)} đề xuất chuyển tiếp từ {Fmt.Dm(from)}",
            $"{title} · {Fmt.Short(to)}", Q("/Planning/Reports", from));

    /// <summary>Kết quả làm thêm (what: "đã duyệt", "từ chối", "được hoàn tác") → người đăng ký.</summary>
    public async Task OvertimeDecidedAsync(int person, DateOnly d, string what, string? note = null) =>
        await SendAsync([person], NotifyType.OvertimeDecided, $"Đăng ký làm thêm {Fmt.Dm(d)} {what}", Body(note), Q("/Overtime", d));

    /// <summary>Leader gửi báo cáo ngày → mọi TP.</summary>
    public async Task LeaderReportSentAsync(int leader, DateOnly d) =>
        await SendAsync(await Managers(), NotifyType.LeaderReportSent, $"{await Name(leader)} gửi báo cáo ngày {Fmt.Dm(d)}", null, Q("/Manager/Reports", d));

    string? Body(string? note) => string.IsNullOrWhiteSpace(note) ? $"bởi {Actor}" : $"{note.Trim()} — {Actor}";
}
