using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Quy tắc tài khoản dùng chung (đăng nhập, TP quản lý nhân sự, Quản trị).
/// - Mã nhân viên lưu chữ thường, bỏ khoảng trắng hai đầu (quy tắc đặt tên của chủ dự án: tp, tp.leader1, tp.leader1.nv1…);
///   so khớp không phân biệt hoa thường. (Artifact dùng chữ HOA — đã đổi theo yêu cầu chủ dự án.)
/// - Mật khẩu tối thiểu 4 ký tự (như artifact). Băm PBKDF2 (PasswordHasher của ASP.NET Core Identity).
/// - Đổi mật khẩu / vai trò / mã ⇒ đổi SecurityStamp để đăng xuất các phiên cũ.
/// </summary>
public static class AccountRules
{
    public const int MinPassword = 4;
    public const int MaxCode = 20;

    public static string NormalizeCode(string? code) => (code ?? "").Trim().ToLowerInvariant();

    /// <summary>Kiểm tra mã: bắt buộc, ≤ 20 ký tự, không trùng (kể cả tài khoản đã xóa — DB có unique index).</summary>
    public static async Task ValidateCodeAsync(AppDbContext db, string code, int? exceptId = null)
    {
        if (code.Length == 0) throw new BizException("Nhập họ tên và mã nhân viên");
        if (code.Length > MaxCode) throw new BizException($"Mã nhân viên tối đa {MaxCode} ký tự");
        if (code.Any(char.IsWhiteSpace)) throw new BizException("Mã nhân viên không được có khoảng trắng");
        if (await db.People.AnyAsync(p => p.Code == code && p.Id != (exceptId ?? 0))) throw new BizException("Mã nhân viên đã tồn tại");
    }

    public static void ValidatePassword(string? password)
    {
        if ((password ?? "").Length < MinPassword) throw new BizException($"Mật khẩu tối thiểu {MinPassword} ký tự");
    }

    /// <summary>Đặt mật khẩu mới (null = để người dùng tự đặt ở lần đăng nhập tới) và đổi SecurityStamp.</summary>
    public static void SetPassword(Person p, string? password, IPasswordHasher<Person> hasher)
    {
        p.PasswordHash = password is null ? null : hasher.HashPassword(p, password);
        p.SecurityStamp = NewStamp();
    }

    public static string NewStamp() => Guid.NewGuid().ToString("N");
}
