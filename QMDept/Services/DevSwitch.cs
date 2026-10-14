using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// CHỈ DÙNG KHI CHẠY THỬ TRÊN MÁY (yêu cầu chủ dự án 2026-10-14, "không đẩy lên web"): bỏ màn hình đăng nhập và hiện thanh
/// chọn giao diện Nhân viên / Leader / Trưởng phòng / Quản trị. Bật khi VÀ CHỈ KHI môi trường là Development
/// và <c>DevSwitch:Enabled = true</c> (appsettings.Development.json). Bản publish (Production) không bao giờ bật.
/// </summary>
public sealed class DevSwitch(IWebHostEnvironment env, IConfiguration cfg, DataCache cache)
{
    public bool Enabled => env.IsDevelopment() && cfg.GetValue<bool>("DevSwitch:Enabled");

    /// <summary>Tài khoản đang hoạt động, theo vai trò rồi tên.</summary>
    public async Task<List<Person>> PeopleAsync() =>
        (await cache.GetBaseAsync()).People.Where(p => !p.IsDeleted)
            .OrderBy(p => p.Role).ThenBy(p => p.SortOrder).ThenBy(p => p.Name, Fmt.ViCompare).ToList();
}
