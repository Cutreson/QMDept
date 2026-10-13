using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace QMDept.Data;

/// <summary>
/// DB thật là MariaDB 10.11: bảng lịch sử migration của MySql.EntityFrameworkCore lỗi khi lấy khóa trên MariaDB
/// ("Unable to cast DBNull to Int64" trong AcquireDatabaseLock) và lớp gốc là internal.
/// Lớp này thay thế toàn bộ: bảng `__EFMigrationsHistory` chuẩn + khóa GET_LOCK có timeout dương (chạy được MySQL lẫn MariaDB).
/// Đăng ký trong Program.cs: <c>ReplaceService&lt;IHistoryRepository, MariaDbHistoryRepository&gt;()</c>.
/// Không hỗ trợ script idempotent (dotnet ef migrations script --idempotent) — dùng script thường.
/// </summary>
public sealed class MariaDbHistoryRepository(HistoryRepositoryDependencies dependencies) : HistoryRepository(dependencies)
{
    const string LockName = "__QMDept_EFMigrationsLock";
    const int LockTimeoutSeconds = 600;

    string Table => SqlGenerationHelper.DelimitIdentifier(TableName);
    string IdCol => SqlGenerationHelper.DelimitIdentifier(MigrationIdColumnName);
    string VerCol => SqlGenerationHelper.DelimitIdentifier(ProductVersionColumnName);

    // Server có thể bật lower_case_table_names=1 (tên bảng lưu chữ thường) → so sánh không phân biệt hoa thường.
    protected override string ExistsSql =>
        $"SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND LOWER(table_name) = LOWER('{TableName}');";

    protected override bool InterpretExistsResult(object? value) => value is not null and not DBNull;

    public override string GetCreateIfNotExistsScript() =>
        $"CREATE TABLE IF NOT EXISTS {Table} ({IdCol} varchar(150) NOT NULL, {VerCol} varchar(32) NOT NULL, PRIMARY KEY ({IdCol})) CHARACTER SET=utf8mb4;";

    public override string GetBeginIfNotExistsScript(string migrationId) =>
        throw new NotSupportedException("Không hỗ trợ script migration idempotent trên MySQL/MariaDB.");
    public override string GetBeginIfExistsScript(string migrationId) =>
        throw new NotSupportedException("Không hỗ trợ script migration idempotent trên MySQL/MariaDB.");
    public override string GetEndIfScript() =>
        throw new NotSupportedException("Không hỗ trợ script migration idempotent trên MySQL/MariaDB.");

    /// <summary>GET_LOCK gắn với phiên kết nối → khóa nhả khi đóng kết nối.</summary>
    public override LockReleaseBehavior LockReleaseBehavior => LockReleaseBehavior.Connection;

    IRelationalCommand Cmd(string sql) => Dependencies.RawSqlCommandBuilder.Build(sql);
    RelationalCommandParameterObject Params() => new(Dependencies.Connection, null, null, Dependencies.CurrentContext.Context,
        Dependencies.CommandLogger, CommandSource.Migrations);

    static void Check(object? r)
    {
        if (r is null or DBNull || Convert.ToInt64(r) != 1)
            throw new TimeoutException("Không lấy được khóa migration (GET_LOCK) — có tiến trình khác đang migrate?");
    }

    public override IMigrationsDatabaseLock AcquireDatabaseLock()
    {
        Check(Cmd($"SELECT GET_LOCK('{LockName}', {LockTimeoutSeconds});").ExecuteScalar(Params()));
        return new Lock(this);
    }

    public override async Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
    {
        Check(await Cmd($"SELECT GET_LOCK('{LockName}', {LockTimeoutSeconds});").ExecuteScalarAsync(Params(), cancellationToken));
        return new Lock(this);
    }

    void Release() => Cmd($"SELECT RELEASE_LOCK('{LockName}');").ExecuteScalar(Params());
    Task ReleaseAsync() => Cmd($"SELECT RELEASE_LOCK('{LockName}');").ExecuteScalarAsync(Params());

    sealed class Lock(MariaDbHistoryRepository repo) : IMigrationsDatabaseLock
    {
        public IHistoryRepository HistoryRepository => repo;
        public void Dispose() => repo.Release();
        public async ValueTask DisposeAsync() => await repo.ReleaseAsync();
        public IMigrationsDatabaseLock ReacquireIfNeeded(bool connectionReopened, bool? transactionRestarted) =>
            connectionReopened ? repo.AcquireDatabaseLock() : this;
        public async Task<IMigrationsDatabaseLock> ReacquireIfNeededAsync(bool connectionReopened, bool? transactionRestarted, CancellationToken cancellationToken = default) =>
            connectionReopened ? await repo.AcquireDatabaseLockAsync(cancellationToken) : this;
    }
}
