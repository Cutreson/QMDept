using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace QMDept.Services;

/// <summary>
/// Gửi form mà mã chống giả mạo hết hạn (trang mở quá lâu trên điện thoại, máy chủ khởi động lại) → trước đây trả 400 rỗng, trình duyệt điện thoại
/// hiện trang trắng + tải file "Decide.txt". Nay: báo nhẹ "Trang đã hết hạn" và quay lại trang vừa xem (yêu cầu 2026-10-23).
/// </summary>
public sealed class AntiforgeryFailureFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!(context.Result?.GetType().Name.Contains("AntiforgeryValidationFailed", StringComparison.Ordinal) ?? false)) return;   // kiểu kết quả lỗi chống giả mạo (400)
        var req = context.HttpContext.Request;
        var back = "/";
        if (Uri.TryCreate(req.Headers.Referer.ToString(), UriKind.Absolute, out var r)) back = r.PathAndQuery;
        // Form gửi bằng fetch (bảng đang nhập): trả lỗi để trình duyệt hiện thông báo và giữ nguyên bảng
        if (req.Headers[AjaxFormFilter.Header] == "1")
        {
            context.Result = new JsonResult(new { ok = false, error = "Trang đã hết hạn — thao tác chưa được lưu. Hãy chép lại nội dung đang nhập rồi tải lại trang" });
            return;
        }
        // Bộ lọc chạy trước khi controller được tạo → lấy TempData từ factory
        var td = context.HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>().GetTempData(context.HttpContext);
        td["toast"] = "Trang đã hết hạn — thao tác chưa được lưu, vui lòng làm lại";
        td.Save();
        context.Result = new RedirectResult(back);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
