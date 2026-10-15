using QMDept.Data;

namespace QMDept.ViewModels;

/// <summary>Sheet "Đề xuất lịch" của NV ở lịch tháng (partial _ProposalSheet): công việc của mình hoặc người hỗ trợ cho dự án mình là Leader dự án.</summary>
public sealed class ProposalSheetVm
{
    public required string TemplateId { get; init; }
    /// <summary>true = đề xuất người hỗ trợ / Leader hỗ trợ (chọn người); false = công việc của chính mình.</summary>
    public required bool Support { get; init; }
    public required List<Project> Projects { get; init; }
    /// <summary>Người có thể được đề xuất hỗ trợ (NV + Leader, trừ mình).</summary>
    public List<Person> People { get; init; } = [];
    public required string Action { get; init; }
}
