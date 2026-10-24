using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

/// <summary>Bảng tổng hợp thời gian làm thêm của toàn bộ nhân sự (trừ Trưởng phòng / Quản trị) trong một tháng — yêu cầu 2026-10-24.</summary>
public sealed class OtMonthVm
{
    public required DateOnly Month { get; init; }
    public List<Row> Rows { get; init; } = [];

    public sealed record Row(Person P, string Group, int Days, decimal RegHours, decimal CountedHours, int PendingReg, decimal PendingHours, int PendingResult, decimal PendingResultHours);

    public int Days => Rows.Sum(r => r.Days);
    public decimal RegHours => Rows.Sum(r => r.RegHours);
    public decimal CountedHours => Rows.Sum(r => r.CountedHours);

    public static async Task<OtMonthVm> LoadAsync(IDbContextFactory<AppDbContext> dbf, DataCache cache, DateOnly month)
    {
        var from = new DateOnly(month.Year, month.Month, 1); var to = from.AddMonths(1).AddDays(-1);
        await using var db = await dbf.CreateDbContextAsync();
        var ots = await db.Overtimes.AsNoTracking().Where(o => o.Date >= from && o.Date <= to && o.Status != OtStatus.Rejected).ToListAsync();
        var people = (await cache.GetBaseAsync()).People.Where(p => !p.IsDeleted && p.Role is Role.Staff or Role.Leader).ToList();
        string Group(Person p) => p.Role == Role.Leader ? "Leader" : people.Find(x => x.Id == p.LeaderId) is { } l ? "Nhóm " + l.Name : "Chưa xếp nhóm";
        var rows = people.Select(p =>
        {
            var mine = ots.Where(o => o.PersonId == p.Id).ToList();
            var ok = mine.Where(o => o.Status == OtStatus.Approved).ToList();
            var pend = mine.Where(o => o.Status is OtStatus.Pending or OtStatus.PendingManager).ToList();
            var resPend = ok.Where(o => o.Completion != OtCompletion.Approved).ToList();
            return new Row(p, Group(p), ok.Count, ok.Sum(o => o.Hours), ok.Where(o => o.Completion == OtCompletion.Approved).Sum(o => o.ActualHours ?? 0),
                pend.Count, pend.Sum(o => o.Hours), resPend.Count, resPend.Sum(o => o.Hours));
        }).OrderBy(r => r.P.Role == Role.Leader ? 0 : 1).ThenBy(r => r.Group, Fmt.ViCompare).ThenBy(r => r.P.Name, Fmt.ViCompare).ToList();
        return new OtMonthVm { Month = from, Rows = rows };
    }
}
