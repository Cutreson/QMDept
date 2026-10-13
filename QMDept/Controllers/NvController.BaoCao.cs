using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Nhân viên → Báo cáo đầu ngày (nvMorning) và Báo cáo cuối ngày (nvEvening) cùng các thao tác ghi của chúng
/// (submit planForm, addOut, openUpdate, openExtra, submit eodForm). Mọi POST: chỉ NV trên trang của chính mình (RequireSelfNv).
/// </summary>
public partial class NvController
{
    // ======================= Báo cáo đầu ngày =======================

    /// <summary>nvMorning. ?edit=yyyy-MM-dd (= ngày đang xem) → sửa kế hoạch đã gửi (S.editPlan).</summary>
    [HttpGet]
    public async Task<IActionResult> DauNgay()
    {
        await D.EnsureAsync(Date, Date.AddDays(14));
        var me = Scope.Me;
        var plan = me is null ? null : D.PlanOf(Date, me.Id);
        // artifact chỉ có nút "Sửa kế hoạch và gửi lại" khi kế hoạch CHƯA được duyệt → ?edit= bị bỏ qua khi đã duyệt.
        var editing = plan is not null && Dept.PlanSt(plan) != PlanState.DaDuyet && Request.Query["edit"] == Fmt.Key(Date);
        var showForm = plan is null || editing;
        // Dữ liệu vừa gửi bị lỗi kiểm tra (GuiKeHoach) → điền lại form, không mất dữ liệu đã nhập.
        var draft = NvBaoCao.ReadDraft(TempData[NvBaoCao.DraftKey] as string, Date);
        return View(new NvMorningVm
        {
            Me = me,
            Plan = plan,
            ShowForm = showForm,
            Editing = editing,
            Form = draft is not null && showForm && !Scope.IsReadOnly ? NvMorningForm.FromInput(draft) : NvMorningForm.FromPlan(plan),
            List = me is null ? [] : D.TasksOn(Date, me.Id).Where(t => t.Source != TaskSource.ChuyenTiep || D.OkCal(t)).ToList(),
        });
    }

    /// <summary>submit planForm: kiểm tra theo đúng thứ tự artifact rồi ghi đè kế hoạch ngày (xóa các quyết định duyệt cũ).</summary>
    [HttpPost]
    public async Task<IActionResult> GuiKeHoach(NvPlanInput input)
    {
        try
        {
            RequireSelfNv();
            var me = Scope.Me!;
            var plan = await Db.DayPlans.Include(p => p.PlanTasks).FirstOrDefaultAsync(p => p.Date == Date && p.EmployeeId == me.Id);
            NvBaoCao.RequireEditable(plan);
            var wst = NvBaoCao.ParseEnum<WorkStatus>(input.Wst) ?? throw new BizException("Chọn trạng thái làm việc");
            var lv = LeaveRules.TypeOf(wst) is LeaveType lt ? NvBaoCao.ReadLeave(lt, input) : null;
            string? prov = null, cus = null;
            if (LeaveRules.IsTrip(wst))
            {
                prov = input.Prov?.Trim();
                if (string.IsNullOrEmpty(prov) || !Fmt.Provinces.Contains(prov)) throw new BizException("Chọn tỉnh / thành công tác");
                cus = NvBaoCao.Clip(input.Cus, 200) ?? throw new BizException("Nhập khách hàng");
            }
            var meal = NvBaoCao.ParseEnum<Meal>(input.Meal) ?? throw new BizException("Chọn báo cơm");

            var all = await Db.WorkTasks.Where(t => t.Date == Date && t.EmployeeId == me.Id).ToListAsync();
            // Chỉ nhận việc của chính NV trong ngày, đang hiện trên form (việc chuyển tiếp phải đã được duyệt).
            var shown = all.Where(t => t.Source != TaskSource.ChuyenTiep || t.CarryOk).Select(t => t.Id).ToHashSet();
            var picks = input.Pick.Where(shown.Contains).ToHashSet();
            if (picks.Count == 0 && !(lv is not null && lv.Hours >= 8)) throw new BizException("Chọn ít nhất 1 công việc");

            var sup = NvBaoCao.ParseEnum<SupportScope>(input.Sup) ?? SupportScope.Khong;
            var supText = sup == SupportScope.Khong ? null : NvBaoCao.Clip(input.SupText, 2000);
            if (sup != SupportScope.Khong && supText is null) throw new BizException("Nhập nội dung cần hỗ trợ nhân sự");
            var note = NvBaoCao.Clip(input.Note, 2000);

            foreach (var t in all) t.Selected = picks.Contains(t.Id);

            if (plan is null)
            {
                plan = new DayPlan { Date = Date, EmployeeId = me.Id };
                Db.DayPlans.Add(plan);
            }
            plan.Status = wst;
            plan.Meal = meal;
            plan.LeaveType = lv?.Type;
            plan.LeaveSession = lv?.Session;
            plan.LeaveFrom = lv?.From;
            plan.LeaveTo = lv?.To;
            plan.LeaveHours = lv?.Hours;
            plan.LeaveDays = lv?.Days;
            plan.TripProvince = prov;
            plan.TripCustomer = cus;
            plan.SupportScope = sup;
            plan.SupportText = supText;
            plan.Note = note;
            plan.SentAt = VnTime.UtcNow;
            // artifact .set() thay cả bản ghi → mọi quyết định duyệt / góp ý cũ bị xóa
            plan.LeaderDecision = null; plan.LeaderBy = null; plan.LeaderAt = null; plan.LeaderNote = null;
            plan.TpDecision = null; plan.TpBy = null; plan.TpAt = null; plan.TpNote = null;
            // taskIds = picks
            var have = plan.PlanTasks.Select(x => x.WorkTaskId).ToHashSet();
            foreach (var x in plan.PlanTasks.Where(x => !picks.Contains(x.WorkTaskId)).ToList())
            {
                plan.PlanTasks.Remove(x);
                Db.DayPlanTasks.Remove(x);
            }
            foreach (var id in picks.Where(id => !have.Contains(id))) plan.PlanTasks.Add(new DayPlanTask { WorkTaskId = id });

            await Db.SaveChangesAsync();
        }
        catch (BizException ex)
        {
            Toast(ex.Message);
            if (Scope.IsSelf && !Scope.IsReadOnly && NvBaoCao.WriteDraft(input, Date) is { } draft) TempData[NvBaoCao.DraftKey] = draft;
            return Back();
        }
        Toast("Đã gửi kế hoạch đầu ngày");
        return Redirect(Ui.To(Request, "/Nv/DauNgay", ("d", Fmt.Key(Date))));
    }

    /// <summary>addOut: thêm việc ngoài kế hoạch (đã chọn) vào ngày đang xem.</summary>
    [HttpPost]
    public async Task<IActionResult> ThemNgoaiKeHoach(TaskFieldsInput input) => await Do(async () =>
    {
        RequireSelfNv();
        // Nút "Thêm công việc ngoài kế hoạch" chỉ có trên form kế hoạch (chưa gửi / đang sửa, chưa được duyệt).
        NvBaoCao.RequireEditable(await Db.DayPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Date == Date && p.EmployeeId == Scope.Me!.Id));
        await NvBaoCao.RequireActiveProjectAsync(Db, input.ProjectId);
        var (title, item) = await input.ResolveAsync(Db);
        NvBaoCao.Clip(title, 300);
        Db.WorkTasks.Add(new WorkTask
        {
            Date = Date, EmployeeId = Scope.Me!.Id, ProjectId = input.ProjectId, PlanItemId = item?.Id, Title = title,
            Start = input.Start, End = input.End, Status = TaskState.ChuaLam, Source = TaskSource.NgoaiKeHoach,
            Selected = true, CreatedAt = VnTime.UtcNow,
        });
        await Db.SaveChangesAsync();
    }, "Đã thêm việc ngoài kế hoạch");

    // ======================= Báo cáo cuối ngày =======================

    /// <summary>nvEvening. ?open=1,2 → mở "Chi tiết" các việc (S.open).</summary>
    [HttpGet]
    public async Task<IActionResult> CuoiNgay()
    {
        await D.EnsureAsync(Date, Date.AddDays(14));
        return View(new NvEveningVm { Me = Scope.Me, Open = NvBaoCao.OpenIds(Request) });
    }

    /// <summary>openUpdate → Lưu cập nhật: trạng thái, ghi chú, ảnh, kế hoạch tiếp theo (việc chuyển tiếp), đầu mục.</summary>
    [HttpPost]
    public async Task<IActionResult> CapNhatViec(NvUpdateInput input) => await Do(async () =>
    {
        RequireSelfNv();
        var me = Scope.Me!;
        var t = await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == input.TaskId && x.EmployeeId == me.Id && x.Selected)
            ?? throw new BizException("Không tìm thấy công việc");
        var s = NvBaoCao.ParseEnum<TaskState>(input.St) ?? t.Status;
        var note = NvBaoCao.Clip(input.Note, 2000);
        if (s != TaskState.HoanThanh && note is null) throw new BizException("Nhập lý do chưa hoàn thành");
        var cur = t.CarriedToId is int cid ? await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == cid) : null;

        DateOnly nd = default;
        string ns = "", ne = "", nt = "";
        if (s != TaskState.HoanThanh)
        {
            nt = NvBaoCao.Clip(input.NextTitle, 300) ?? throw new BizException("Nhập nội dung kế hoạch tiếp theo");
            ns = input.NextStart ?? ""; ne = input.NextEnd ?? "";
            if (!Fmt.IsHhmm(ns) || !Fmt.IsHhmm(ne) || ne.Le(ns)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
            if (Fmt.ParseKey(input.NextDate) is not DateOnly d0 || !NvBaoCao.NextDateOptions(t.Date, cur?.Date).Contains(d0))
                throw new BizException("Chọn ngày chuyển sang");
            nd = d0;
        }
        var newPhotos = await Photos.SaveAsync(input.Photos, Scope.Account.Id);
        try
        {
            await using var tx = await Db.Database.BeginTransactionAsync();
            if (s != TaskState.HoanThanh)
            {
                if (cur is not null)
                {
                    if (!(cur.Date == nd && cur.Start == ns && cur.End == ne && cur.Title == nt))
                    {
                        cur.Date = nd; cur.Start = ns; cur.End = ne; cur.Title = nt;
                        cur.CarryOk = false; cur.EditedBy = null;
                    }
                    t.CarriedToId = cur.Id; t.CarryDate = nd;
                }
                else
                {
                    var n = new WorkTask
                    {
                        Date = nd, EmployeeId = t.EmployeeId, ProjectId = t.ProjectId, PlanItemId = t.PlanItemId, Title = nt,
                        Start = ns, End = ne, Status = TaskState.ChuaLam, Source = TaskSource.ChuyenTiep, CarryOk = false,
                        CarriedFromId = t.Id, FromDate = t.Date, Selected = false, CreatedAt = VnTime.UtcNow,
                    };
                    Db.WorkTasks.Add(n);
                    await Db.SaveChangesAsync();
                    t.CarriedToId = n.Id; t.CarryDate = nd;
                }
            }
            else if (cur is not null)
            {
                // Đổi lại thành Hoàn thành: xóa việc chuyển tiếp nếu nó còn "Chưa làm".
                if (cur.Status == TaskState.ChuaLam) await Flow.DeleteTaskAsync(cur.Id);
                t.CarriedToId = null; t.CarryDate = null;
            }

            t.Status = s;
            t.Note = note;
            t.UpdatedAt = VnTime.UtcNow;
            foreach (var p in newPhotos) { p.TaskId = t.Id; Db.Photos.Add(p); }

            if (t.PlanItemId is int iid && await Db.PlanItems.FirstOrDefaultAsync(i => i.Id == iid) is { } it)
            {
                if (s == TaskState.HoanThanh && input.ItemDone && it.Status != TaskState.HoanThanh)
                {
                    it.Status = TaskState.HoanThanh; it.DoneDate = t.Date; it.DoneBy = me.Name;
                }
                else if (s is TaskState.DangLam or TaskState.HoanThanh or TaskState.Cham && it.Status == TaskState.ChuaLam)
                    it.Status = TaskState.DangLam;
            }
            await Db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            // Ghi DB thất bại (giao dịch tự rollback) → không để lại file ảnh mồ côi trên đĩa.
            foreach (var p in newPhotos) Photos.Delete(p.StoredName);
            throw;
        }
    }, "Đã cập nhật");

    /// <summary>openExtra(false): NV đề xuất phát sinh (chờ Leader duyệt).</summary>
    [HttpPost]
    public async Task<IActionResult> DeXuatPhatSinh(ExtraInput input) => await Do(async () =>
    {
        RequireSelfNv();
        await NvBaoCao.RequireActiveProjectAsync(Db, input.ProjectId);
        NvBaoCao.Clip(input.Title, 300);
        NvBaoCao.Clip(input.Desc, 2000);
        await Flow.CreateExtraAsync(Date, Scope.Me!.Id, input.ProjectId, input.Title ?? "", input.Desc,
            string.IsNullOrEmpty(input.Start) ? null : input.Start, input.End, input.Photos, Scope.Account.Id,
            byLeader: false, Scope.ActorName, Role.Nv);
    }, "Đã gửi đề xuất phát sinh");

    /// <summary>submit eodForm: chặn nếu còn việc chưa xong chưa có kế hoạch tiếp; gửi lại = Leader duyệt lại.</summary>
    [HttpPost]
    public async Task<IActionResult> GuiBaoCao(NvEodInput input) => await Do(async () =>
    {
        RequireSelfNv();
        var me = Scope.Me!.Id;
        var miss = await Db.WorkTasks.CountAsync(t => t.Date == Date && t.EmployeeId == me && t.Selected
            && t.Status != TaskState.HoanThanh && t.CarriedToId == null);
        if (miss > 0) throw new BizException($"Còn {miss} việc chưa hoàn thành chưa có kế hoạch tiếp theo");
        var note = NvBaoCao.Clip(input.Note, 2000);
        var r = await Db.DayReports.FirstOrDefaultAsync(x => x.Date == Date && x.EmployeeId == me);
        if (r is null)
        {
            r = new DayReport { Date = Date, EmployeeId = me };
            Db.DayReports.Add(r);
        }
        r.Blocked = input.Blocked == "co";
        r.Note = note;
        r.SentAt = VnTime.UtcNow;
        r.LeaderDecision = null; r.LeaderBy = null; r.LeaderAt = null; r.LeaderNote = null;
        await Db.SaveChangesAsync();
    }, "Đã gửi báo cáo cuối ngày");
}
