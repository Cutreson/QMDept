using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace QMDept.Services;

/// <summary>
/// Form gửi bằng fetch (header <see cref="Header"/>, wwwroot/js/site.js "ajaxSend") — yêu cầu 2026-11-05: gửi không đủ điều kiện thì bảng đang nhập (sheet) PHẢI còn nguyên,
/// không được tải lại trang rồi bắt nhập lại từ đầu. Thay vì chuyển hướng, trả JSON:
/// <c>{ ok:false, error }</c> khi controller báo LỖI (<c>ToastErr</c> → TempData toastSticky; thông báo được lấy ra, không hiện lại ở trang sau) —
/// trình duyệt hiện thông báo và giữ nguyên bảng; <c>{ ok:true, redirect }</c> khi thành công — trình duyệt chuyển tới URL đó (thông báo thành công nằm trong TempData như cũ).
/// </summary>
public sealed class AjaxFormFilter : IAlwaysRunResultFilter
{
    public const string Header = "X-Qm-Ajax";

    public void OnResultExecuting(ResultExecutingContext context)
    {
        var http = context.HttpContext;
        if (!HttpMethods.IsPost(http.Request.Method) || http.Request.Headers[Header] != "1") return;
        var url = context.Result switch { RedirectResult r => r.Url, LocalRedirectResult l => l.Url, _ => null };
        if (url is null) return;   // kết quả khác (hiếm): để nguyên, trình duyệt tự xử lý

        var td = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
        if (td.Peek("toastSticky") as string == "1")
        {
            var msg = td["toast"] as string;
            td.Remove("toast"); td.Remove("toastSticky");
            context.Result = new JsonResult(new { ok = false, error = msg });
        }
        else context.Result = new JsonResult(new { ok = true, redirect = url });
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
