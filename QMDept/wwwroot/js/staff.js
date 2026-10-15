// QMDept — Nhân viên: Báo cáo đầu ngày (/Staff/Morning) và Báo cáo cuối ngày (/Staff/Evening).
// Port từ artifact: leaveBox, readLeave, openLeave, applyLeave, onWstChange (onStatusChange), saveDraft/morningDraft, onMorningInput,
// kiểm tra planForm trước khi gửi, openUpdate (nxShow + tự tích đầu mục), kiểm tra eodForm / openExtra.
// Mọi kiểm tra ở đây chỉ để tiện (không phải gửi lên server rồi mới báo); server kiểm tra lại toàn bộ.
// Luật nghỉ (unpaidFrom, unpaidTo, sessions) lấy từ C# (Services/Rules.cs → LeaveRules) qua <script id="staffCfg" type="application/json">.
// Giá trị trạng thái / báo cơm / hỗ trợ / loại nghỉ = tên enum C# (WorkStatus, Meal, SupportScope, LeaveType, LeaveSession, TaskState).
// Dùng các hàm chung của site.js: qmToast, qmOpenSheet, qmCloseSheet; sự kiện 'qm:sheet-open'.
(function () {
  'use strict';
  const $ = (s, r) => (r || document).querySelector(s);
  const $$ = (s, r) => [...(r || document).querySelectorAll(s)];
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const toast = t => window.qmToast && window.qmToast(t);
  const num = n => String(Math.round(n * 100) / 100).replace('.', ',');
  const hmMin = s => +s.slice(0, 2) * 60 + +s.slice(3, 5);
  // workMin(a,b): phút làm việc trong [a,b], bỏ nghỉ trưa 12:00–13:00
  function workMin(a, b) { const ov = (x, y) => Math.max(0, Math.min(hmMin(b), y) - Math.max(hmMin(a), x)); return ov(480, 720) + ov(780, 1020); }
  // LT của artifact: trạng thái làm việc (WorkStatus) → loại nghỉ (LeaveType)
  const LEAVE_TYPE = { PaidLeave: 'Paid', UnpaidLeave: 'Unpaid' };
  const isLeave = s => !!LEAVE_TYPE[s];
  const isTrip = s => s === 'DayTrip' || s === 'LongTrip';
  const SUPPORT_SCOPES = ['Team', 'OtherTeam', 'OtherDept'];
  const store = {
    get(k) { try { return JSON.parse(sessionStorage.getItem(k) || 'null'); } catch { return null; } },
    set(k, v) { try { sessionStorage.setItem(k, JSON.stringify(v)); } catch { /* bỏ qua */ } },
    del(k) { try { sessionStorage.removeItem(k); } catch { /* bỏ qua */ } },
  };

  // ======================= Báo cáo đầu ngày =======================
  const cfgEl = $('#staffCfg');
  let cfg = null;
  try { cfg = cfgEl ? JSON.parse(cfgEl.textContent) : null; } catch { cfg = null; }
  // Đã gửi kế hoạch (trang hiện trạng thái) → bỏ bản nháp (artifact: S.mDraft = null sau khi gửi / editPlan / cancelEdit).
  if (cfg && cfg.clear) store.del(cfg.key);
  const pf = $('#planForm');
  if (cfg && pf && pf.dataset.ro !== '1') morning(pf, cfg);

  function morning(f, cfg) {
    const SESSIONS = cfg.sessions || {}, UNPAID_FROM = cfg.unpaidFrom || [], UNPAID_TO = cfg.unpaidTo || [];
    const statusSel = $('#pStatus', f), mealSel = $('#pMeal', f), supportSel = $('#pSupport', f), provinceSel = $('#pProvince', f);
    const hid = n => f.querySelector(`input[type=hidden][name=${n}]`);
    const picks = () => $$('input[name=picks]', f);
    const paidLeave = s => { const [, a, b] = SESSIONS[s], full = s === 'FullDay'; return { type: 'Paid', session: s, from: a, to: b, hours: full ? 8 : 4, days: full ? 1 : 0.5 }; };
    const leaveQty = l => l.type === 'Paid' ? `${num(l.days)} ngày phép` : `${num(l.hours)} giờ không lương`;

    function leaveFromForm() {
      const t = hid('leaveType').value, s = hid('leaveSession').value, a = hid('leaveFrom').value, b = hid('leaveTo').value;
      if (t === 'Paid' && SESSIONS[s]) return paidLeave(s);
      if (t === 'Unpaid' && a && b && b > a && workMin(a, b) >= 60) return { type: 'Unpaid', from: a, to: b, hours: workMin(a, b) / 60 };
      return null;
    }

    // ---------- bản nháp (S.mDraft) ----------
    let dr = store.get(cfg.key);
    if (dr && typeof dr === 'object') {
      const setSel = (sel, v) => { if (sel && v != null) sel.value = v; };
      setSel(statusSel, dr.status); setSel(mealSel, dr.meal); setSel(supportSel, dr.support || 'None'); setSel(provinceSel, dr.province || '');
      $('#pNote', f).value = dr.note || ''; $('#pSupportText', f).value = dr.supportText || ''; $('#pCustomer', f).value = dr.customer || '';
      if (dr.checks) picks().forEach(x => { if (x.value in dr.checks) x.checked = !!dr.checks[x.value]; });
      dr.leaveOff = dr.leaveOff || [];
      if (dr.leave === undefined) dr.leave = leaveFromForm();
    } else dr = { leave: leaveFromForm(), leaveOff: [] };
    let prevStatus = statusSel.value || '';

    // Đọc giá trị form vào dr (không lưu) — render() dựa vào dr.status nên phải gọi ngay khi tải trang,
    // nếu không trạng thái nghỉ / công tác của kế hoạch đang sửa bị ẩn mất.
    function readForm() {
      const checks = {}; picks().forEach(x => checks[x.value] = x.checked);
      Object.assign(dr, {
        status: statusSel.value || '', meal: mealSel.value || '', note: $('#pNote', f).value, support: supportSel.value || 'None',
        province: provinceSel ? provinceSel.value || '' : '', customer: $('#pCustomer', f).value, supportText: $('#pSupportText', f).value, checks,
      });
    }
    function saveDraft() { readForm(); store.set(cfg.key, dr); }
    readForm();
    const leaveNow = () => isLeave(dr.status) && dr.leave && dr.leave.type === LEAVE_TYPE[dr.status] ? dr.leave : null;

    // leaveBox(status, l) — bản C#: ViewModels/StaffReports.cs LeaveBox
    function leaveBox(status, l) {
      if (!l) return `<div class="leave-box leave-miss"><div><div class="leave-t">Chưa chọn thời gian nghỉ</div><div class="note">${status === 'PaidLeave' ? 'Nghỉ phép tối thiểu theo buổi' : 'Nghỉ không lương tối thiểu 1 giờ'}</div></div><button type="button" class="btn btn-sm btn-primary" data-leave-time>Chọn giờ nghỉ</button></div>`;
      return `<div class="leave-box"><div><div class="leave-t">${l.type === 'Paid' ? 'Nghỉ phép · ' + esc((SESSIONS[l.session] || [''])[0]) : 'Nghỉ không lương'}</div><div class="note"><b class="mono">${esc(l.from)}–${esc(l.to)}</b> · ${leaveQty(l)}</div></div><button type="button" class="btn btn-sm" data-leave-time>Đổi</button></div>`;
    }

    // Vẽ lại các phần phụ thuộc trạng thái (thay cho render() của artifact)
    function render() {
      const l = dr.leave;
      hid('leaveType').value = l ? l.type : ''; hid('leaveSession').value = l && l.session || '';
      hid('leaveFrom').value = l ? l.from : ''; hid('leaveTo').value = l ? l.to : '';
      const ln = leaveNow(), wrap = $('#leaveWrap', f);
      wrap.hidden = !isLeave(dr.status);
      wrap.innerHTML = wrap.hidden ? '' : leaveBox(dr.status, ln);
      $('#tripWrap', f).hidden = !isTrip(dr.status);
      $('#supportWrap', f).hidden = !SUPPORT_SCOPES.includes(supportSel.value);
      picks().forEach(x => {
        const m = x.closest('label').querySelector('[data-leave-hit]');
        if (m) m.hidden = !(ln && x.dataset.start >= ln.from && x.dataset.end <= ln.to);
      });
      [statusSel, mealSel, provinceSel].forEach(s => s && s.classList.toggle('ph', !s.value));
    }

    // applyLeave(dr, leave): chọn lại việc đã bỏ chọn trước đó, bỏ chọn việc nằm trọn trong khung nghỉ, báo cơm theo số giờ nghỉ
    function applyLeave(leave) {
      const byId = {}; picks().forEach(x => byId[x.value] = x);
      let restored = 0, meal = false;
      (dr.leaveOff || []).forEach(id => { const x = byId[id]; if (x && !x.checked) { x.checked = true; restored++; } });
      dr.leaveOff = [];
      if (leave) picks().forEach(x => { if (x.dataset.start >= leave.from && x.dataset.end <= leave.to && x.checked) { x.checked = false; dr.leaveOff.push(x.value); } });
      // Nghỉ cả ngày → báo cơm "Cắt cơm" (Meal.None)
      if (leave && leave.hours >= 8) { if (mealSel.value !== 'None') { dr.mealPrev = mealSel.value || ''; mealSel.value = 'None'; meal = true; } }
      else if ('mealPrev' in dr) { if (mealSel.value === 'None') mealSel.value = dr.mealPrev; delete dr.mealPrev; }
      dr.leave = leave;
      saveDraft();
      return { off: dr.leaveOff.length, restored, meal };
    }

    // readLeave(leaveType, f)
    function readLeave(leaveType, sf) {
      if (leaveType === 'Paid') {
        const s = (sf.querySelector('input[name=session]:checked') || {}).value;
        if (!SESSIONS[s]) return { err: 'Chọn buổi nghỉ.', wait: true };
        const v = paidLeave(s), [l, a, b] = SESSIONS[s];
        return { v, html: `${esc(l)}: nghỉ <b>${a}–${b}</b> · tính <b>${num(v.days)}</b> ngày phép` };
      }
      const a = ($('#leaveFromSel', sf) || {}).value, b = ($('#leaveToSel', sf) || {}).value;
      if (!a || !b) return { err: 'Chọn giờ bắt đầu và giờ kết thúc.', wait: true };
      if (b <= a) return { err: 'Giờ kết thúc phải sau giờ bắt đầu.' };
      const mins = workMin(a, b);
      if (mins < 60) return { err: `Nghỉ không lương tối thiểu 1 giờ (đang chọn ${mins} phút).` };
      const v = { type: 'Unpaid', from: a, to: b, hours: mins / 60 };
      return { v, html: `Nghỉ <b>${a}–${b}</b> · tính <b>${num(v.hours)}</b> giờ không lương${a < '13:00' && b > '12:00' ? ' (đã trừ nghỉ trưa)' : ''}` };
    }

    // openLeave(status, prevStatus): prev === undefined → Hủy không đổi lại trạng thái
    function openLeave(status, prev) {
      saveDraft();
      const leaveType = LEAVE_TYPE[status], cur = dr.leave && dr.leave.type === leaveType ? dr.leave : null;
      const selectField = (id, arr, v, ph) => { const has = !!(v && arr.includes(v));
        return `<select class="in${has ? '' : ' ph'}" id="${id}"><option value="" disabled ${has ? '' : 'selected'}>${ph}</option>${arr.map(x => `<option value="${esc(x)}" ${x === v ? 'selected' : ''}>${esc(x)}</option>`).join('')}</select>`; };
      const body = `<p class="note" style="margin:2px 0 12px">${esc(cfg.day)} · ${leaveType === 'Paid' ? 'Nghỉ phép tính tối thiểu theo buổi (0,5 ngày).' : 'Nghỉ không lương tối thiểu 1 giờ, không tính nghỉ trưa 12:00–13:00.'}</p>`
        + (leaveType === 'Paid'
          ? `<div class="segr" role="radiogroup" aria-label="Buổi nghỉ">${Object.entries(SESSIONS).map(([k, [l]]) => `<label><input type="radio" name="session" value="${esc(k)}" ${cur && cur.session === k ? 'checked' : ''}><span>${esc(l)}</span></label>`).join('')}</div>`
          : `<div class="pick2 eq"><div><label class="f" for="leaveFromSel" style="margin-top:0">Từ giờ</label>${selectField('leaveFromSel', UNPAID_FROM, cur && cur.from, 'Từ…')}</div><div><label class="f" for="leaveToSel" style="margin-top:0">Đến giờ</label>${selectField('leaveToSel', UNPAID_TO, cur && cur.to, 'Đến…')}</div></div>`)
        + `<div class="result-box" id="leaveSum" aria-live="polite"></div>`;
      let done = false;
      const sf = window.qmOpenSheet(`<form id="sheetForm" aria-labelledby="shT"><h3 id="shT">${leaveType === 'Paid' ? 'Chọn thời gian nghỉ phép' : 'Chọn thời gian nghỉ không lương'}</h3>${body}`
        + `<div class="btn-row" style="margin-top:14px"><button type="button" class="btn" data-x="close">Hủy</button><button class="btn btn-primary" id="shOk">Xác nhận</button></div></form>`,
        { onClose: () => { if (done || prev === undefined) return; statusSel.value = prev; prevStatus = prev; saveDraft(); render(); } });
      const sum = $('#leaveSum', sf), ok = $('#shOk', sf);
      const upd = () => {
        $$('select.in', sf).forEach(s => s.classList.toggle('ph', !s.value));
        const r = readLeave(leaveType, sf);
        sum.classList.toggle('err', !r.v && !r.wait); sum.innerHTML = r.v ? r.html : esc(r.err); ok.disabled = !r.v;
      };
      const fromSel = $('#leaveFromSel', sf);
      if (fromSel) fromSel.addEventListener('change', e => {
        const a = e.target.value, t = $('#leaveToSel', sf);
        if (!t.value || t.value <= a || workMin(a, t.value) < 60) { const nx = UNPAID_TO.find(b => b > a && workMin(a, b) >= 60); if (nx) t.value = nx; }
      });
      $$('input,select', sf).forEach(x => x.addEventListener('change', upd));
      upd();
      sf.addEventListener('submit', e => {
        e.preventDefault(); e.stopPropagation();
        const r = readLeave(leaveType, sf);
        if (!r.v) { toast(r.err); return; }
        done = true; window.qmCloseSheet();
        statusSel.value = status; prevStatus = status;
        const x = applyLeave(r.v); render();
        toast(['Đã ghi thời gian nghỉ', x.off ? `bỏ chọn ${x.off} việc trùng giờ nghỉ` : '', x.meal ? 'báo cơm: Cắt cơm' : ''].filter(Boolean).join(' · '));
      });
    }

    // onWstChange(sel) của artifact
    function onStatusChange() {
      const v = statusSel.value || '', prev = prevStatus;
      saveDraft();
      if (isLeave(v)) { openLeave(v, prev); return; }
      prevStatus = v;
      if (isLeave(prev) || dr.leave) { const r = applyLeave(null); render(); if (r.restored) toast(`Đã chọn lại ${r.restored} việc`); }
      else render();
    }

    // onMorningInput
    const draftFields = '[name=picks],#pMeal,#pNote,#pSupport,#pSupportText,#pProvince,#pCustomer';
    f.addEventListener('input', e => { if (e.target.matches && e.target.matches(draftFields)) saveDraft(); });
    f.addEventListener('change', e => {
      const t = e.target;
      if (t === statusSel) { onStatusChange(); return; }
      if (t.matches && t.matches(draftFields)) { saveDraft(); if (t === supportSel) $('#supportWrap', f).hidden = !SUPPORT_SCOPES.includes(supportSel.value); }
    });
    // leaveTime: nút "Chọn giờ nghỉ" / "Đổi"
    f.addEventListener('click', e => {
      if (!e.target.closest('[data-leave-time]')) return;
      e.preventDefault();
      saveDraft();
      if (isLeave(dr.status)) openLeave(dr.status);
    });
    // Lưu nháp trước khi mở bảng "Thêm công việc ngoài kế hoạch" (trang sẽ tải lại sau khi thêm)
    document.addEventListener('click', e => { if (e.target.closest('[data-sheet="tplAddOut"]')) saveDraft(); }, true);

    // submit planForm — cùng thứ tự kiểm tra và câu báo lỗi như artifact
    f.addEventListener('submit', e => {
      saveDraft();
      const stop = (msg, sel) => { e.preventDefault(); e.stopPropagation(); toast(msg); const el = sel && $(sel, f); if (el) el.focus(); };
      const status = dr.status, leave = isLeave(status) ? dr.leave : null;
      if (!status) return stop('Chọn trạng thái làm việc', '#pStatus');
      if (isLeave(status) && (!leave || leave.type !== LEAVE_TYPE[status])) { stop('Chọn thời gian nghỉ'); openLeave(status); return; }
      if (isTrip(status) && !dr.province) return stop('Chọn tỉnh / thành công tác', '#pProvince');
      if (isTrip(status) && !dr.customer.trim()) return stop('Nhập khách hàng', '#pCustomer');
      if (!dr.meal) return stop('Chọn báo cơm', '#pMeal');
      if (!picks().some(x => x.checked) && !(leave && leave.hours >= 8)) return stop('Chọn ít nhất 1 công việc');
      if (SUPPORT_SCOPES.includes(dr.support) && !dr.supportText.trim()) return stop('Nhập nội dung cần hỗ trợ nhân sự', '#pSupportText');
      render();
    });

    render();
  }

  // ======================= Báo cáo cuối ngày =======================

  // openUpdate: ẩn "Kế hoạch tiếp theo" khi Hoàn thành; đổi nhãn/gợi ý ghi chú; tự tích đầu mục khi Hoàn thành và kết thúc ≥ 15:00
  document.addEventListener('qm:sheet-open', e => {
    const f = e.target;
    if (!f.matches || !f.matches('form[data-upd]')) return;
    const end = f.dataset.end || '';
    const stateOf = () => (f.querySelector('input[name=state]:checked') || {}).value || f.dataset.state;
    const nxShow = () => {
      const v = stateOf(), reason = v !== 'Done';
      const nx = $('#nxWrap', f); if (nx) nx.hidden = !reason;
      $('#uNL', f).textContent = reason ? 'Lý do chưa hoàn thành (bắt buộc)' : 'Kết quả / ghi chú';
    };
    $$('input[name=state]', f).forEach(r => r.addEventListener('change', () => {
      const c = $('#itDone', f); if (c) c.checked = r.value === 'Done' && r.checked && end >= '15:00';
      nxShow();
    }));
    nxShow();
  });

  // Kiểm tra trước khi gửi (pha capture: chạy trước site.js để không khóa nút khi còn lỗi)
  document.addEventListener('submit', e => {
    const f = e.target;
    if (!(f instanceof HTMLFormElement)) return;
    const stop = msg => { e.preventDefault(); e.stopPropagation(); toast(msg); };
    if (f.matches('form[data-upd]')) {
      const ws = $('#wS', f), we = $('#wE', f);
      if (ws && we && we.value <= ws.value) return stop('Giờ kết thúc phải sau giờ bắt đầu');
      const s = (f.querySelector('input[name=state]:checked') || {}).value || f.dataset.state;
      if (s !== 'Done' && !$('#uN', f).value.trim()) return stop('Nhập lý do chưa hoàn thành');
      if (s !== 'Done' && $('#nxT', f)) {
        if (!$('#nxT', f).value.trim()) return stop('Nhập nội dung kế hoạch tiếp theo');
        if ($('#nxE', f).value <= $('#nxS', f).value) return stop('Giờ kết thúc phải sau giờ bắt đầu');
      }
    } else if ((f.getAttribute('action') || '').indexOf('/Staff/AddUnplannedTask') === 0) {
      // readTaskFields('o'): nội dung hoặc đầu mục; kết thúc sau bắt đầu
      const oT = $('#oT', f), oI = $('#oI', f), oS = $('#oS', f), oE = $('#oE', f);
      if (oT && !oT.value.trim() && !(oI && oI.value)) return stop(oI ? 'Nhập nội dung hoặc chọn đầu mục' : 'Nhập nội dung công việc');
      if (oS && oE && oE.value <= oS.value) return stop('Giờ kết thúc phải sau giờ bắt đầu');
    } else if ((f.getAttribute('action') || '').indexOf('/Staff/ProposeExtra') === 0) {
      // openExtra: kết thúc (nếu có) phải sau bắt đầu
      const xs = $('#xS', f), xf = $('#xF', f);
      if (xs && xf && xf.value && xf.value <= xs.value) return stop('Giờ kết thúc phải sau giờ bắt đầu');
    } else if (f.id === 'eodForm') {
      const n = +(f.dataset.miss || 0), nt = +(f.dataset.missTeam || 0), ns = +(f.dataset.missSup || 0);
      if (n > 0) return stop(`Còn ${n} việc chưa hoàn thành chưa có kế hoạch tiếp theo`);
      if (nt > 0) return stop(`Còn ${nt} việc của dự án bạn phụ trách chưa báo hoàn thành hay kế hoạch tiếp theo`);
      if (ns > 0) return stop(`Còn ${ns} việc hỗ trợ chưa tích “Đã hỗ trợ”`);
    }
  }, true);

  // "Chi tiết": mở/đóng tại chỗ (không có JS thì link ?open= vẫn hoạt động)
  document.addEventListener('click', e => {
    const a = e.target.closest('[data-detail]'); if (!a) return;
    const d = document.getElementById(a.dataset.detail); if (!d) return;
    e.preventDefault();
    d.hidden = !d.hidden;
    a.setAttribute('aria-expanded', String(!d.hidden));
  });
})();
