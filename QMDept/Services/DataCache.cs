using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Bộ nhớ đệm dữ liệu đọc dùng chung giữa các request (tối ưu tốc độ tải trang — DB đặt xa máy chủ web, mỗi truy vấn tốn
/// một vòng mạng). Giữ ảnh chụp các bảng nhỏ (people, projects, plan_items, assignments, notify_settings) và dữ liệu theo
/// khoảng ngày mà <see cref="Dept"/> nạp.
///
/// Đúng đắn: mọi lệnh ghi đi qua EF (SaveChanges / ExecuteUpdate) → <see cref="DataCacheInterceptor"/> tăng <see cref="Version"/>
/// sau mỗi lệnh không phải SELECT và sau mỗi COMMIT ⇒ ảnh chụp cũ bị bỏ ngay lần đọc kế tiếp. Ảnh chụp được gắn phiên bản
/// đọc TRƯỚC khi truy vấn nên dữ liệu nạp giữa chừng một lần ghi luôn bị coi là cũ. Thêm TTL ngắn để an toàn khi DB bị sửa
/// từ ngoài ứng dụng. Giả định chạy một tiến trình web (nhiều tiến trình ⇒ mỗi tiến trình chỉ thấy lệnh ghi của mình, trễ tối đa TTL).
///
/// Thực thể trong cache dùng chung giữa các request: CHỈ ĐỌC (giống quy ước của Dept). Không sửa thuộc tính / danh sách của chúng.
/// </summary>
public sealed class DataCache(IDbContextFactory<AppDbContext> factory)
{
    static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    const int MaxRanges = 64;
    /// <summary>Số kết nối DB tối đa dùng đồng thời để nạp cache (DB dùng chung của nhà cung cấp hosting).</summary>
    static readonly SemaphoreSlim Gate = new(8);

    long _version;
    public long Version => Interlocked.Read(ref _version);
    public void Invalidate() => Interlocked.Increment(ref _version);

    public sealed record BaseSnap(long Version, DateTime At, List<Person> People, List<Project> Projects, List<PlanItem> Items,
        List<Assignment> Assigns, Dictionary<int, NotifySetting> Notify, List<CalendarProposal> PendingProposals);

    public sealed record RangeSnap(long Version, DateTime At, List<WorkTask> Tasks, List<DayPlan> Plans, List<DayReport> Reports,
        List<Leave> Leaves, List<ProjectNeed> Needs, List<Extra> Extras, List<Overtime> Ots);

    BaseSnap? _base;
    readonly ConcurrentDictionary<(DateOnly From, DateOnly To), RangeSnap> _ranges = new();
    // Một lần nạp cho mỗi khóa: các request đồng thời cùng thiếu cache chờ chung một lần nạp.
    readonly ConcurrentDictionary<object, Lazy<Task<object>>> _loading = new();

    bool Fresh(long v, DateTime at) => v == Version && DateTime.UtcNow - at < Ttl;

    /// <summary>Chạy một truy vấn trên DbContext + kết nối riêng (để nạp song song).</summary>
    async Task<T> Q<T>(Func<AppDbContext, Task<T>> q)
    {
        await Gate.WaitAsync();
        try { await using var db = await factory.CreateDbContextAsync(); return await q(db); }
        finally { Gate.Release(); }
    }

    async Task<T> Once<T>(object key, Func<Task<T>> load) where T : class
    {
        var lazy = _loading.GetOrAdd(key, _ => new Lazy<Task<object>>(async () => await load()));
        try { return (T)await lazy.Value; }
        finally { _loading.TryRemove(new KeyValuePair<object, Lazy<Task<object>>>(key, lazy)); }
    }

    /// <summary>Ảnh chụp các bảng nhỏ (gồm cả người đã xóa).</summary>
    public async Task<BaseSnap> GetBaseAsync()
    {
        var b = _base;
        if (b is not null && Fresh(b.Version, b.At)) return b;
        var v = Version;
        return await Once(("base", v), async () =>
        {
            var at = DateTime.UtcNow;
            var people = Q(db => db.People.AsNoTracking().ToListAsync());
            var projects = Q(db => db.Projects.AsNoTracking().ToListAsync());
            var items = Q(db => db.PlanItems.AsNoTracking().ToListAsync());
            var assigns = Q(db => db.Assignments.AsNoTracking().ToListAsync());
            var notify = Q(db => db.NotifySettings.AsNoTracking().ToListAsync());
            // Đề xuất lịch đang chờ duyệt (hiện huy hiệu / thẻ chờ duyệt cho Leader).
            var proposals = Q(db => db.CalendarProposals.AsNoTracking().Include(p => p.People).Where(p => p.Status == ExtraStatus.Pending).ToListAsync());
            // Chờ đủ mọi truy vấn (một truy vấn lỗi không để các truy vấn còn lại chạy không ai chờ).
            await Task.WhenAll(people, projects, items, assigns, notify, proposals);
            var snap = new BaseSnap(v, at, await people, await projects, await items, await assigns, (await notify).ToDictionary(x => x.PersonId), await proposals);
            // Không để lần nạp cũ hơn (xong muộn) ghi đè ảnh chụp mới hơn.
            BaseSnap? cur;
            do { cur = _base; if (cur is not null && cur.Version > v) break; }
            while (Interlocked.CompareExchange(ref _base, snap, cur) != cur);
            return snap;
        });
    }

    /// <summary>Dữ liệu theo ngày trong [from, to] (kèm việc chuyển tiếp được tham chiếu nằm ngoài khoảng).</summary>
    public async Task<RangeSnap> GetRangeAsync(DateOnly from, DateOnly to)
    {
        if (_ranges.TryGetValue((from, to), out var r) && Fresh(r.Version, r.At)) return r;
        var v = Version;
        return await Once(("range", v, from, to), async () =>
        {
            var at = DateTime.UtcNow;
            var tasksT = Q(async db =>
            {
                var tasks = await db.WorkTasks.AsNoTracking()
                    .Where(t => (t.Date >= from && t.Date <= to) || (t.FromDate != null && t.FromDate >= from && t.FromDate <= to))
                    .ToListAsync();
                var have = tasks.Select(t => t.Id).ToHashSet();
                var refIds = tasks.Where(t => t.CarriedToId != null).Select(t => t.CarriedToId!.Value)
                    .Concat(tasks.Where(t => t.CarriedFromId != null).Select(t => t.CarriedFromId!.Value))
                    .Where(id => !have.Contains(id)).Distinct().ToList();
                if (refIds.Count > 0) tasks.AddRange(await db.WorkTasks.AsNoTracking().Where(t => refIds.Contains(t.Id)).ToListAsync());
                return tasks;
            });
            var plans = Q(db => db.DayPlans.AsNoTracking().Include(p => p.PlanTasks).Where(p => p.Date >= from && p.Date <= to).ToListAsync());
            var reports = Q(db => db.DayReports.AsNoTracking().Where(p => p.Date >= from && p.Date <= to).ToListAsync());
            var leaves = Q(db => db.Leaves.AsNoTracking().Where(p => p.Date >= from && p.Date <= to).ToListAsync());
            var needs = Q(db => db.ProjectNeeds.AsNoTracking().Where(p => p.Date >= from && p.Date <= to).ToListAsync());
            var extras = Q(db => db.Extras.AsNoTracking().Where(p => p.Date >= from && p.Date <= to).ToListAsync());
            var ots = Q(db => db.Overtimes.AsNoTracking().Include(o => o.Items).Where(p => p.Date >= from && p.Date <= to).ToListAsync());
            await Task.WhenAll(tasksT, plans, reports, leaves, needs, extras, ots);
            var snap = new RangeSnap(v, at, await tasksT, await plans, await reports, await leaves, await needs, await extras, await ots);
            // Bỏ các khoảng đã cũ / giới hạn số khoảng giữ lại.
            foreach (var kv in _ranges) if (!Fresh(kv.Value.Version, kv.Value.At)) _ranges.TryRemove(kv.Key, out _);
            if (_ranges.Count >= MaxRanges) _ranges.Clear();
            _ranges.AddOrUpdate((from, to), snap, (_, old) => old.Version > v ? old : snap);
            return snap;
        });
    }
}

/// <summary>
/// Tăng phiên bản <see cref="DataCache"/> sau mỗi lệnh ghi (không phải SELECT) và sau mỗi COMMIT.
/// Lấy <see cref="DataCache"/> chậm (lần dùng đầu): DataCache cần IDbContextFactory ⇐ cấu hình DbContext cần interceptor này —
/// inject trực tiếp sẽ thành vòng phụ thuộc và DI bị deadlock lúc khởi động.
/// </summary>
public sealed class DataCacheInterceptor(IServiceProvider services) : DbCommandInterceptor, IDbTransactionInterceptor
{
    DataCache? _cache;
    DataCache Cache => _cache ??= services.GetRequiredService<DataCache>();

    void After(DbCommand cmd)
    {
        var t = cmd.CommandText.AsSpan().TrimStart();
        if (!t.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) Cache.Invalidate();
    }

    public override int NonQueryExecuted(DbCommand c, CommandExecutedEventData e, int r) { After(c); return r; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand c, CommandExecutedEventData e, int r, CancellationToken ct = default) { After(c); return ValueTask.FromResult(r); }
    public override DbDataReader ReaderExecuted(DbCommand c, CommandExecutedEventData e, DbDataReader r) { After(c); return r; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand c, CommandExecutedEventData e, DbDataReader r, CancellationToken ct = default) { After(c); return ValueTask.FromResult(r); }
    public override object? ScalarExecuted(DbCommand c, CommandExecutedEventData e, object? r) { After(c); return r; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand c, CommandExecutedEventData e, object? r, CancellationToken ct = default) { After(c); return ValueTask.FromResult(r); }

    public void TransactionCommitted(DbTransaction t, TransactionEndEventData e) => Cache.Invalidate();
    public Task TransactionCommittedAsync(DbTransaction t, TransactionEndEventData e, CancellationToken ct = default) { Cache.Invalidate(); return Task.CompletedTask; }
    // Rollback: ảnh chụp đọc trong giao dịch (thấy dữ liệu chưa commit) phải bị bỏ.
    public void TransactionRolledBack(DbTransaction t, TransactionEndEventData e) => Cache.Invalidate();
    public Task TransactionRolledBackAsync(DbTransaction t, TransactionEndEventData e, CancellationToken ct = default) { Cache.Invalidate(); return Task.CompletedTask; }
}

/// <summary>
/// Giữ kết nối DB mở đến hết vòng đời DbContext (một request) thay vì EF mở / đóng quanh từng truy vấn:
/// DB ở xa (~250 ms mỗi vòng mạng) và mỗi lần lấy lại kết nối từ pool của MySql.Data tốn thêm ~2 vòng (~500 ms).
/// Kết nối vẫn được trả về pool khi DbContext bị dispose (cuối request).
/// </summary>
public sealed class KeepOpenInterceptor : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionClosing(DbConnection c, ConnectionEventData e, InterceptionResult r) => InterceptionResult.Suppress();
    public override ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection c, ConnectionEventData e, InterceptionResult r) => ValueTask.FromResult(InterceptionResult.Suppress());
}
