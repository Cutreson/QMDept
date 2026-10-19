using QMDept.Data;

namespace QMDept.ViewModels;

/// <summary>Sheet "Đề xuất công việc" của NV ở lịch tháng (partial _ProposalSheet): việc của mình, kèm chọn người hỗ trợ khi dự án chọn là dự án mình làm Leader dự án.</summary>
public sealed class ProposalSheetVm
{
    public required string TemplateId { get; init; }
    public required List<Project> Projects { get; init; }
    /// <summary>Người có thể được đề xuất hỗ trợ (NV + Leader, trừ mình). Rỗng nếu NV không là Leader dự án nào.</summary>
    public List<Person> People { get; init; } = [];
    /// <summary>Các dự án NV làm Leader dự án — chỉ khi chọn các dự án này mới hiện ô chọn người hỗ trợ.</summary>
    public List<int> LedProjectIds { get; init; } = [];
    /// <summary>Các nhóm khác (Leader của nhóm đó) — chỉ xin hỗ trợ theo số lượng, không chỉ định người.</summary>
    public List<Person> OtherGroups { get; init; } = [];
    public required string Action { get; init; }
    /// <summary>Có ô "Loại đề xuất" (Đề xuất công việc / Xin nghỉ) — sheet gộp đề xuất công việc và xin nghỉ (yêu cầu 2026-10-20).</summary>
    public bool AllowLeave { get; init; }
    /// <summary>Có phần đề xuất công việc (NV là Leader dự án của ít nhất một dự án). False = sheet chỉ xin nghỉ.</summary>
    public bool AllowWork { get; init; } = true;
}
