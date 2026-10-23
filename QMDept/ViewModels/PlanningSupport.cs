using QMDept.Data;

namespace QMDept.ViewModels;

/// <summary>Model của /Planning/Support (Leader nhóm được xin hỗ trợ — yêu cầu 2026-10-23).</summary>
public sealed class PlanningSupportVm
{
    /// <summary>pending | done.</summary>
    public required string Filter { get; init; }
    public List<SupportRequest> Requests { get; init; } = [];
    public int PendingTotal { get; init; }
    public int DoneTotal { get; init; }
    /// <summary>Yêu cầu còn chờ ở ngày khác (liên kết sang ngày đó).</summary>
    public List<(DateOnly Date, int Count)> OtherPending { get; init; } = [];
}
