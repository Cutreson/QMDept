// QMDept — Nhân viên: Báo cáo đầu ngày (/Nv/DauNgay) và Báo cáo cuối ngày (/Nv/CuoiNgay).
// Port từ artifact: leaveBox, readLeave, openLeave, applyLeave, onWstChange, saveDraft/morningDraft, onMorningInput,
// kiểm tra planForm trước khi gửi, openUpdate (nxShow + tự tích đầu mục), kiểm tra eodForm / openExtra.
// Mọi kiểm tra ở đây chỉ để tiện (không phải gửi lên server rồi mới báo); server kiểm tra lại toàn bộ.
// Luật nghỉ (KL_FROM, KL_TO, LS) lấy từ C# (Services/Rules.cs → LeaveRules) qua <script id="nvCfg" type="application/json">.
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
  const LT = { NghiPhep: 'Phep', NghiKl: 'Kl' };
  const isLeave = s => !!LT[s];
  const isTrip = s => s === 'CtNgay' || s === 'CtDai';
  const SUP = ['Nhom', 'NhomKhac', 'PhongKhac'];
  const store = {
    get(k) { try { return JSON.parse(sessionStorage.getItem(k) || 'null'); } catch { return null; } },
    set(k, v) { try { sessionStorage.setItem(k, JSON.stringify(v)); } catch { /* bỏ qua */ } },
    del(k) { try { sessionStorage.removeItem(k); } catch { /* bỏ qua */ } },
  };

  // ======================= Báo cáo đầu ngày =======================
  const cfgEl = $('#nvCfg');
  let cfg = null;
  try { cfg = cfgEl ? JSON.parse(cfgEl.textContent) : null; } catch { cfg = null; }
  // Đã gửi kế hoạch (trang hiện trạng thái) → bỏ bản nháp (artifact: S.mDraft = null sau khi gửi / editPlan / cancelEdit).
  if (cfg && cfg.clear) store.del(cfg.key);
  const pf = $('#planForm');
  if (cfg && pf && pf.dataset.ro !== '1') morning(pf, cfg);

  function morning(f, cfg) {
    const LS = cfg.ls || {}, KL_FROM = cfg.klFrom || [], KL_TO = cfg.klTo || [];
    const wstSel = $('#pWst', f), mealSel = $('#pMeal', f), supSel = $('#pSup', f), provSel = $('#pProv', f);
    const hid = n => f.querySelector(`input[type=hidden][name=${n}]`);
    const picks = () => $$('input[name=pick]', f);
    const phep = s => { const [, a, b] = LS[s], full = s === 'CaNgay'; return { type: 'Phep', session: s, from: a, to: b, hours: full ? 8 : 4, days: full ? 1 : 0.5 }; };
    const leaveQty = l => l.type === 'Phep' ? `${num(l.days)} ngày phép` : `${num(l.hours)} giờ không lương`;

    function leaveFromForm() {
      const t = hid('leaveType').value, s = hid('leaveSession').value, a = hid('leaveFrom').value, b = hid('leaveTo').value;
      if (t === 'Phep' && LS[s]) return phep(s);
      if (t === 'Kl' && a && b && b > a && workMin(a, b) >= 60) return { type: 'Kl', from: a, to: b, hours: workMin(a, b) / 60 };
      return null;
    }

    // ---------- bản nháp (S.mDraft) ----------
    let dr = store.get(cfg.key);
    if (dr && typeof dr === 'object') {
      const setSel = (sel, v) => { if (sel && v != null) sel.value = v; };
      setSel(wstSel, dr.wst); setSel(mealSel, dr.meal); setSel(supSel, dr.sup || 'Khong'); setSel(provSel, dr.prov || '');
      $('#pNote', f).value = dr.note || ''; $('#pSupT', f).value = dr.supT || ''; $('#pCus', f).value = dr.cus || '';
      if (dr.ck) picks().forEach(x => { if (x.value in dr.ck) x.checked = !!dr.ck[x.value]; });
      dr.lvOff = dr.lvOff || [];
      if (dr.leave === undefined) dr.leave = leaveFromForm();
    } else dr = { leave: leaveFromForm(), lvOff: [] };
    let prevWst = wstSel.value || '';

    // Đọc giá trị form vào dr (không lưu) — render() dựa vào dr.wst nên phải gọi ngay khi tải trang,
    // nếu không trạng thái nghỉ / công tác của kế hoạch đang sửa bị ẩn mất.
    function readForm() {
      const ck = {}; picks().forEach(x => ck[x.value] = x.checked);
      Object.assign(dr, {
        wst: wstSel.value || '', meal: mealSel.value || '', note: $('#pNote', f).value, sup: supSel.value || 'Khong',
        prov: provSel ? provSel.value || '' : '', cus: $('#pCus', f).value, supT: $('#pSupT', f).value, ck,
      });
    }
    function saveDraft() { readForm(); store.set(cfg.key, dr); }
    readForm();
    const lvNow = () => isLeave(dr.wst) && dr.leave && dr.leave.type === LT[dr.wst] ? dr.leave : null;

    // leaveBox(wst, l) — bản C#: ViewModels/NvBaoCao.cs LeaveBox
    function leaveBox(wst, l) {
      if (!l) return `<div class="lv-box lv-miss"><div><div class="lv-t">Chưa chọn thời gian nghỉ</div><div class="note">${wst === 'NghiPhep' ? 'Nghỉ phép tối thiểu theo buổi' : 'Nghỉ không lương tối thiểu 1 giờ'}</div></div><button type="button" class="btn btn-sm btn-primary" data-lv-time>Chọn giờ nghỉ</button></div>`;
      return `<div class="lv-box"><div><div class="lv-t">${l.type === 'Phep' ? 'Nghỉ phép · ' + esc((LS[l.session] || [''])[0]) : 'Nghỉ không lương'}</div><div class="note"><b class="mono">${esc(l.from)}–${esc(l.to)}</b> · ${leaveQty(l)}</div></div><button type="button" class="btn btn-sm" data-lv-time>Đổi</button></div>`;
    }

    // Vẽ lại các phần phụ thuộc trạng thái (thay cho render() của artifact)
    function render() {
      const l = dr.leave;
      hid('leaveType').value = l ? l.type : ''; hid('leaveSession').value = l && l.session || '';
      hid('leaveFrom').value = l ? l.from : ''; hid('leaveTo').value = l ? l.to : '';
      const ln = lvNow(), lw = $('#lvWrap', f);
      lw.hidden = !isLeave(dr.wst);
      lw.innerHTML = lw.hidden ? '' : leaveBox(dr.wst, ln);
      $('#tripWrap', f).hidden = !isTrip(dr.wst);
      $('#supWrap', f).hidden = !SUP.includes(supSel.value);
      picks().forEach(x => {
        const m = x.closest('label').querySelector('[data-lvhit]');
        if (m) m.hidden = !(ln && x.dataset.start >= ln.from && x.dataset.end <= ln.to);
      });
      [wstSel, mealSel, provSel].forEach(s => s && s.classList.toggle('ph', !s.value));
    }

    // applyLeave(dr, leave): chọn lại việc đã bỏ chọn trước đó, bỏ chọn việc nằm trọn trong khung nghỉ, báo cơm theo số giờ nghỉ
    function applyLeave(leave) {
      const byId = {}; picks().forEach(x => byId[x.value] = x);
      let restored = 0, meal = false;
      (dr.lvOff || []).forEach(id => { const x = byId[id]; if (x && !x.checked) { x.checked = true; restored++; } });
      dr.lvOff = [];
      if (leave) picks().forEach(x => { if (x.dataset.start >= leave.from && x.dataset.end <= leave.to && x.checked) { x.checked = false; dr.lvOff.push(x.value); } });
      if (leave && leave.hours >= 8) { if (mealSel.value !== 'Cat') { dr.mealPrev = mealSel.value || ''; mealSel.value = 'Cat'; meal = true; } }
      else if ('mealPrev' in dr) { if (mealSel.value === 'Cat') mealSel.value = dr.mealPrev; delete dr.mealPrev; }
      dr.leave = leave;
      saveDraft();
      return { off: dr.lvOff.length, restored, meal };
    }

    // readLeave(lt, f)
    function readLeave(lt, sf) {
      if (lt === 'Phep') {
        const s = (sf.querySelector('input[name=ls]:checked') || {}).value;
        if (!LS[s]) return { err: 'Chọn buổi nghỉ.', wait: true };
        const v = phep(s), [l, a, b] = LS[s];
        return { v, html: `${esc(l)}: nghỉ <b>${a}–${b}</b> · tính <b>${num(v.days)}</b> ngày phép` };
      }
      const a = ($('#lvF', sf) || {}).value, b = ($('#lvT', sf) || {}).value;
      if (!a || !b) return { err: 'Chọn giờ bắt đầu và giờ kết thúc.', wait: true };
      if (b <= a) return { err: 'Giờ kết thúc phải sau giờ bắt đầu.' };
      const mins = workMin(a, b);
      if (mins < 60) return { err: `Nghỉ không lương tối thiểu 1 giờ (đang chọn ${mins} phút).` };
      const v = { type: 'Kl', from: a, to: b, hours: mins / 60 };
      return { v, html: `Nghỉ <b>${a}–${b}</b> · tính <b>${num(v.hours)}</b> giờ không lương${a < '13:00' && b > '12:00' ? ' (đã trừ nghỉ trưa)' : ''}` };
    }

    // openLeave(type, prevWst): prev === undefined → Hủy không đổi lại trạng thái
    function openLeave(type, prev) {
      saveDraft();
      const lt = LT[type], cur = dr.leave && dr.leave.type === lt ? dr.leave : null;
      const selectField = (id, arr, v, ph) => { const has = !!(v && arr.includes(v));
        return `<select class="in${has ? '' : ' ph'}" id="${id}"><option value="" disabled ${has ? '' : 'selected'}>${ph}</option>${arr.map(x => `<option value="${esc(x)}" ${x === v ? 'selected' : ''}>${esc(x)}</option>`).join('')}</select>`; };
      const body = `<p class="note" style="margin:2px 0 12px">${esc(cfg.day)} · ${lt === 'Phep' ? 'Nghỉ phép tính tối thiểu theo buổi (0,5 ngày).' : 'Nghỉ không lương tối thiểu 1 giờ, không tính nghỉ trưa 12:00–13:00.'}</p>`
        + (lt === 'Phep'
          ? `<div class="segr" role="radiogroup" aria-label="Buổi nghỉ">${Object.entries(LS).map(([k, [l]]) => `<label><input type="radio" name="ls" value="${esc(k)}" ${cur && cur.session === k ? 'checked' : ''}><span>${esc(l)}</span></label>`).join('')}</div>`
          : `<div class="pick2 eq"><div><label class="f" for="lvF" style="margin-top:0">Từ giờ</label>${selectField('lvF', KL_FROM, cur && cur.from, 'Từ…')}</div><div><label class="f" for="lvT" style="margin-top:0">Đến giờ</label>${selectField('lvT', KL_TO, cur && cur.to, 'Đến…')}</div></div>`)
        + `<div class="lv-sum" id="lvSum" aria-live="polite"></div>`;
      let done = false;
      const sf = window.qmOpenSheet(`<form id="sheetForm" aria-labelledby="shT"><h3 id="shT">${lt === 'Phep' ? 'Chọn thời gian nghỉ phép' : 'Chọn thời gian nghỉ không lương'}</h3>${body}`
        + `<div class="btn-row" style="margin-top:14px"><button type="button" class="btn" data-x="close">Hủy</button><button class="btn btn-primary" id="shOk">Xác nhận</button></div></form>`,
        { onClose: () => { if (done || prev === undefined) return; wstSel.value = prev; prevWst = prev; saveDraft(); render(); } });
      const sum = $('#lvSum', sf), ok = $('#shOk', sf);
      const upd = () => {
        $$('select.in', sf).forEach(s => s.classList.toggle('ph', !s.value));
        const r = readLeave(lt, sf);
        sum.classList.toggle('err', !r.v && !r.wait); sum.innerHTML = r.v ? r.html : esc(r.err); ok.disabled = !r.v;
      };
      const lvF = $('#lvF', sf);
      if (lvF) lvF.addEventListener('change', e => {
        const a = e.target.value, t = $('#lvT', sf);
        if (!t.value || t.value <= a || workMin(a, t.value) < 60) { const nx = KL_TO.find(b => b > a && workMin(a, b) >= 60); if (nx) t.value = nx; }
      });
      $$('input,select', sf).forEach(x => x.addEventListener('change', upd));
      upd();
      sf.addEventListener('submit', e => {
        e.preventDefault(); e.stopPropagation();
        const r = readLeave(lt, sf);
        if (!r.v) { toast(r.err); return; }
        done = true; window.qmCloseSheet();
        wstSel.value = type; prevWst = type;
        const x = applyLeave(r.v); render();
        toast(['Đã ghi thời gian nghỉ', x.off ? `bỏ chọn ${x.off} việc trùng giờ nghỉ` : '', x.meal ? 'báo cơm: Cắt cơm' : ''].filter(Boolean).join(' · '));
      });
    }

    // onWstChange(sel)
    function onWstChange() {
      const v = wstSel.value || '', prev = prevWst;
      saveDraft();
      if (isLeave(v)) { openLeave(v, prev); return; }
      prevWst = v;
      if (isLeave(prev) || dr.leave) { const r = applyLeave(null); render(); if (r.restored) toast(`Đã chọn lại ${r.restored} việc`); }
      else render();
    }

    // onMorningInput
    const draftFields = '[name=pick],#pMeal,#pNote,#pSup,#pSupT,#pProv,#pCus';
    f.addEventListener('input', e => { if (e.target.matches && e.target.matches(draftFields)) saveDraft(); });
    f.addEventListener('change', e => {
      const t = e.target;
      if (t === wstSel) { onWstChange(); return; }
      if (t.matches && t.matches(draftFields)) { saveDraft(); if (t === supSel) $('#supWrap', f).hidden = !SUP.includes(supSel.value); }
    });
    // leaveTime: nút "Chọn giờ nghỉ" / "Đổi"
    f.addEventListener('click', e => {
      if (!e.target.closest('[data-lv-time]')) return;
      e.preventDefault();
      saveDraft();
      if (isLeave(dr.wst)) openLeave(dr.wst);
    });
    // Lưu nháp trước khi mở bảng "Thêm công việc ngoài kế hoạch" (trang sẽ tải lại sau khi thêm)
    document.addEventListener('click', e => { if (e.target.closest('[data-sheet="tplAddOut"]')) saveDraft(); }, true);

    // submit planForm — cùng thứ tự kiểm tra và câu báo lỗi như artifact
    f.addEventListener('submit', e => {
      saveDraft();
      const stop = (msg, sel) => { e.preventDefault(); e.stopPropagation(); toast(msg); const el = sel && $(sel, f); if (el) el.focus(); };
      const wst = dr.wst, lv = isLeave(wst) ? dr.leave : null;
      if (!wst) return stop('Chọn trạng thái làm việc', '#pWst');
      if (isLeave(wst) && (!lv || lv.type !== LT[wst])) { stop('Chọn thời gian nghỉ'); openLeave(wst); return; }
      if (isTrip(wst) && !dr.prov) return stop('Chọn tỉnh / thành công tác', '#pProv');
      if (isTrip(wst) && !dr.cus.trim()) return stop('Nhập khách hàng', '#pCus');
      if (!dr.meal) return stop('Chọn báo cơm', '#pMeal');
      if (!picks().some(x => x.checked) && !(lv && lv.hours >= 8)) return stop('Chọn ít nhất 1 công việc');
      if (SUP.includes(dr.sup) && !dr.supT.trim()) return stop('Nhập nội dung cần hỗ trợ nhân sự', '#pSupT');
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
    const stOf = () => (f.querySelector('input[name=st]:checked') || {}).value || f.dataset.st;
    const nxShow = () => {
      const v = stOf(), reason = v !== 'HoanThanh';
      $('#nxWrap', f).hidden = !reason;
      $('#uNL', f).textContent = reason ? 'Lý do chưa hoàn thành (bắt buộc)' : 'Kết quả / ghi chú';
      $('#uN', f).placeholder = reason ? 'VD: Chờ vật tư, khách hàng chưa bàn giao mặt bằng…' : 'VD: Tín hiệu ổn định.';
    };
    $$('input[name=st]', f).forEach(r => r.addEventListener('change', () => {
      const c = $('#itDone', f); if (c) c.checked = r.value === 'HoanThanh' && r.checked && end >= '15:00';
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
      const s = (f.querySelector('input[name=st]:checked') || {}).value || f.dataset.st;
      if (s !== 'HoanThanh' && !$('#uN', f).value.trim()) return stop('Nhập lý do chưa hoàn thành');
      if (s !== 'HoanThanh') {
        if (!$('#nxT', f).value.trim()) return stop('Nhập nội dung kế hoạch tiếp theo');
        if ($('#nxE', f).value <= $('#nxS', f).value) return stop('Giờ kết thúc phải sau giờ bắt đầu');
      }
    } else if ((f.getAttribute('action') || '').indexOf('/Nv/ThemNgoaiKeHoach') === 0) {
      // readTaskFields('o'): nội dung hoặc đầu mục; kết thúc sau bắt đầu
      const oT = $('#oT', f), oI = $('#oI', f), oS = $('#oS', f), oE = $('#oE', f);
      if (oT && oI && !oT.value.trim() && !oI.value) return stop('Nhập nội dung hoặc chọn đầu mục');
      if (oS && oE && oE.value <= oS.value) return stop('Giờ kết thúc phải sau giờ bắt đầu');
    } else if ((f.getAttribute('action') || '').indexOf('/Nv/DeXuatPhatSinh') === 0) {
      // openExtra: kết thúc (nếu có) phải sau bắt đầu
      const xs = $('#xS', f), xf = $('#xF', f);
      if (xs && xf && xf.value && xf.value <= xs.value) return stop('Giờ kết thúc phải sau giờ bắt đầu');
    } else if (f.id === 'eodForm') {
      const n = +(f.dataset.miss || 0);
      if (n > 0) return stop(`Còn ${n} việc chưa hoàn thành chưa có kế hoạch tiếp theo`);
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
