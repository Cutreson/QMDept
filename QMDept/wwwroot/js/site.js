// QMDept — hành vi dùng chung cho mọi trang (không cần thư viện ngoài).
// Quy ước data-attribute (dùng trong Razor):
//   data-autosubmit                 trên <select>/<input>: đổi giá trị → submit form chứa nó.
//   form[data-confirm="câu hỏi"]    hỏi xác nhận trước khi gửi; data-confirm-label = nhãn nút (mặc định "Đồng ý").
//   form[data-prompt="Tiêu đề"]     mở hộp nhập văn bản; data-prompt-label, data-prompt-value, data-prompt-required,
//                                   data-prompt-name (tên field ẩn nhận giá trị, mặc định "text").
//   [data-sheet="templateId"]       bấm → mở <template id="templateId"> (chứa 1 <form>) trong bảng trượt.
//   select[data-items-for="id"]     đổi dự án → nạp lại select #id bằng window.QM_ITEMS[projectId] = [[id,title],...].
//   select[data-show-target="#sel"][data-show-values="A,B"]  hiện phần tử #sel khi giá trị thuộc danh sách.
//   form[data-once]                 khóa nút submit sau khi gửi (mặc định cho mọi form post).
(function () {
  'use strict';
  const $ = (s, r) => (r || document).querySelector(s);
  const $$ = (s, r) => [...(r || document).querySelectorAll(s)];
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  // ---------- chiều cao header → biến CSS --hdr-h (menu dọc bên trái trên máy tính bám ngay dưới header) ----------
  const hdr = $('header.top');
  if (hdr) {
    const setH = () => document.documentElement.style.setProperty('--hdr-h', hdr.offsetHeight + 'px');
    setH();
    if (window.ResizeObserver) new ResizeObserver(setH).observe(hdr); else window.addEventListener('resize', setH);
  }

  // ---------- chế độ chỉ xem (đang xem màn hình cấp dưới) ----------
  if (document.body.classList.contains('readonly')) {
    $$('#app form').forEach(f => {
      if ((f.getAttribute('method') || '').toLowerCase() !== 'post' || f.classList.contains('ro-ok')) return;
      $$('input:not([type=hidden]),select,textarea', f).forEach(x => x.disabled = true);
    });
  }

  // ---------- toast ----------
  const toastEl = $('#toast');
  function toast(t) {
    if (!toastEl || !t) return;
    toastEl.textContent = t; toastEl.hidden = false;
    clearTimeout(toast.t); toast.t = setTimeout(() => toastEl.hidden = true, 3200);
  }
  window.qmToast = toast;
  if (toastEl && toastEl.dataset.msg) toast(toastEl.dataset.msg);

  // ---------- bảng trượt (sheet) ----------
  const dlg = $('#dlg');
  let onClose = null;
  function closeSheet() { if (!dlg || dlg.hidden) return; dlg.hidden = true; dlg.innerHTML = ''; const c = onClose; onClose = null; c && c(); }
  window.qmCloseSheet = closeSheet;
  // Mở sheet với nội dung HTML là MỘT <form>; trả về form đó.
  function openSheet(html, opts) {
    opts = opts || {};
    dlg.innerHTML = html;
    const form = dlg.firstElementChild;
    form.classList.add('card'); form.setAttribute('role', 'dialog'); form.setAttribute('aria-modal', 'true');
    dlg.hidden = false; onClose = opts.onClose || null;
    $$('[data-x=close]', form).forEach(b => b.addEventListener('click', e => { e.preventDefault(); closeSheet(); }));
    const f = form.querySelector('input:not([type=hidden]):not([type=radio]):not([type=checkbox]),textarea,select');
    f && f.focus();
    form.dispatchEvent(new CustomEvent('qm:sheet-open', { bubbles: true }));
    return form;
  }
  window.qmOpenSheet = openSheet;
  if (dlg) {
    dlg.addEventListener('click', e => { if (e.target === dlg) closeSheet(); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') closeSheet(); });
  }
  const sheetButtons = (okLabel, okClass) =>
    `<div class="btn-row" style="margin-top:14px"><button type="button" class="btn" data-x="close">Hủy</button><button class="btn ${okClass || 'btn-primary'}" data-ok>${esc(okLabel)}</button></div>`;

  // [data-sheet]: mở template
  document.addEventListener('click', e => {
    const b = e.target.closest('[data-sheet]'); if (!b) return;
    e.preventDefault();
    const tpl = document.getElementById(b.dataset.sheet); if (!tpl) return;
    const wrap = document.createElement('div'); wrap.appendChild(tpl.content.cloneNode(true));
    openSheet(wrap.innerHTML);
  });

  // form[data-confirm] / form[data-prompt]: chặn submit lần đầu, hỏi trong sheet rồi gửi thật
  document.addEventListener('submit', e => {
    const f = e.target;
    if (!(f instanceof HTMLFormElement)) return;
    if (e.defaultPrevented) return; // script của trang đã chặn submit (vd. kiểm tra phía client) → không làm gì
    if (f.dataset.qmPassed === '1') { f.dataset.qmPassed = ''; lockSubmit(f, e); return; }
    if (f.dataset.confirm !== undefined) {
      e.preventDefault();
      const s = openSheet(`<form><h3>Xác nhận</h3><p style="margin:6px 0 0">${esc(f.dataset.confirm)}</p>${sheetButtons(f.dataset.confirmLabel || 'Đồng ý', 'btn-no')}</form>`);
      s.addEventListener('submit', ev => { ev.preventDefault(); closeSheet(); passSubmit(f, e.submitter); });
      return;
    }
    if (f.dataset.prompt !== undefined) {
      e.preventDefault();
      const req = f.dataset.promptRequired !== undefined;
      const s = openSheet(`<form><h3>${esc(f.dataset.prompt)}</h3><label class="f" for="qmPrompt">${esc(f.dataset.promptLabel || 'Nội dung')}${req ? '' : ' <span class="note">(không bắt buộc)</span>'}</label><textarea class="in" id="qmPrompt" ${req ? 'required' : ''}>${esc(f.dataset.promptValue || '')}</textarea>${sheetButtons(f.dataset.promptOk || 'Lưu', f.dataset.promptClass)}</form>`);
      s.addEventListener('submit', ev => {
        ev.preventDefault();
        const v = $('#qmPrompt', s).value.trim();
        if (req && !v) { toast('Nhập ' + (f.dataset.promptLabel || 'nội dung').toLowerCase()); return; }
        const name = f.dataset.promptName || 'text';
        let h = f.querySelector(`input[type=hidden][name="${name}"]`);
        if (!h) { h = document.createElement('input'); h.type = 'hidden'; h.name = name; f.appendChild(h); }
        h.value = v; closeSheet(); passSubmit(f, e.submitter);
      });
      return;
    }
    lockSubmit(f, e);
  });
  function passSubmit(f, submitter) {
    f.dataset.qmPassed = '1';
    if (submitter && submitter.name) { const h = document.createElement('input'); h.type = 'hidden'; h.name = submitter.name; h.value = submitter.value; f.appendChild(h); }
    if (f.requestSubmit) f.requestSubmit(); else f.submit();
  }
  // Khóa nút submit sau khi gửi (tránh bấm 2 lần).
  function lockSubmit(f, e) {
    if ((f.method || '').toLowerCase() !== 'post') return;
    const btn = e.submitter || f.querySelector('button:not([type=button])');
    if (btn) { setTimeout(() => { btn.disabled = true; btn.dataset.label = btn.textContent; btn.textContent = 'Đang lưu…'; }, 0); }
  }

  // ---------- thông báo đẩy (SignalR /hubs/notify) — bổ sung, không có trong artifact ----------
  // body[data-notify="1"] khi tài khoản bật thông báo (Cài đặt). Mỗi sự kiện: thẻ thông báo trên trang + thông báo của
  // hệ điều hành nếu trình duyệt đã cho phép và tab đang ẩn. Payload: { type, label, title, body, url, at }.
  const notifBox = $('#notifs');
  function showNotif(n) {
    if (!notifBox) return;
    const el = document.createElement('div');
    el.className = 'notif card';
    el.innerHTML = `<div class="notif-h"><span class="pill p-info">${esc(n.label || 'Thông báo')}</span><span class="note mono">${esc(n.at || '')}</span><button type="button" class="notif-x" aria-label="Đóng">×</button></div>
      <div class="notif-t">${esc(n.title)}</div>${n.body ? `<div class="note">${esc(n.body)}</div>` : ''}
      ${n.url ? `<a class="link" href="${esc(n.url)}">Xem ›</a>` : ''}`;
    el.querySelector('.notif-x').onclick = () => el.remove();
    notifBox.prepend(el);
    while (notifBox.children.length > 4) notifBox.lastElementChild.remove();
    setTimeout(() => el.remove(), 15000);
  }
  function osNotif(n) {
    try {
      if (!('Notification' in window) || Notification.permission !== 'granted') return;
      const x = new Notification(n.title, { body: [n.body, n.label].filter(Boolean).join(' · '), icon: '/icon-192.png', tag: n.type + '|' + n.title });
      x.onclick = () => { window.focus(); if (n.url) location.href = n.url; x.close(); };
    } catch { /* trình duyệt chặn: bỏ qua */ }
  }
  window.qmShowNotif = n => { showNotif(n); if (document.hidden) osNotif(n); };
  if (document.body.dataset.notify === '1' && window.signalR) {
    const conn = new signalR.HubConnectionBuilder().withUrl('/hubs/notify').withAutomaticReconnect([0, 2000, 5000, 10000, 30000]).build();
    conn.on('notify', n => window.qmShowNotif(n));
    const start = () => conn.start().catch(() => setTimeout(start, 15000));
    conn.onclose(() => setTimeout(start, 15000));
    start();
  }
  // Trang Cài đặt: quyền thông báo hệ điều hành, gửi thử, chọn / bỏ chọn tất cả.
  const osStatus = $('[data-os-status]');
  function paintOs() {
    if (!osStatus) return;
    const p = !('Notification' in window) ? 'unsupported' : Notification.permission;
    const map = { granted: ['Đã cho phép', 'p-ok'], denied: ['Trình duyệt đang chặn — mở cài đặt trang web để cho phép', 'p-bad'], default: ['Chưa cho phép', 'p-warn'], unsupported: ['Trình duyệt không hỗ trợ', 'p-idle'] }[p];
    osStatus.textContent = map[0]; osStatus.className = 'pill ' + map[1];
  }
  paintOs();
  document.addEventListener('click', e => {
    if (e.target.closest('[data-os-notify]')) { if ('Notification' in window) Notification.requestPermission().then(paintOs); }
    if (e.target.closest('[data-test-notify]')) {
      const n = { type: 'Test', label: 'Thử', title: 'Thông báo thử', body: 'Thông báo đẩy đang hoạt động trên thiết bị này.', at: new Date().toTimeString().slice(0, 5) };
      showNotif(n); osNotif(n);
    }
    const checkAll = e.target.closest('[data-check-all]');
    if (checkAll) $$('[data-notify-types] input[type=checkbox]').forEach(x => x.checked = checkAll.dataset.checkAll === '1');
  });

  // ---------- change handlers ----------
  document.addEventListener('change', e => {
    const t = e.target;
    if (t.matches && t.matches('[data-autosubmit]')) { const f = t.form; if (f) { if (f.requestSubmit) f.requestSubmit(); else f.submit(); } }
    if (t.matches && t.matches('select[data-items-for]')) fillItems(t);
    if (t.matches && t.matches('select[data-lead-for]')) applyLead(t);
    if (t.matches && t.matches('select[data-led]')) toggleLed(t);
    if (t.matches && t.matches('select[data-nav]') && t.value) location.href = t.value;
    if (t.matches && t.matches('select[data-pick-into]')) pickPerson(t);
    if (t.matches && t.matches('select[data-show-target]')) showWhen(t);
    if (t.matches && t.matches('select.in')) t.classList.toggle('ph', !t.value);
  });
  // Giao việc ở lịch nhóm: chọn dự án → nhãn vai trò tự đổi (Leader dự án / Hỗ trợ, bị khóa); ô tích người tham gia để trắng, Leader tự tích (form[data-leads] = {projectId: leaderId}).
  function applyLead(sel) {
    const f = sel.form; if (!f || !f.dataset.leads) return;
    let m = {}; try { m = JSON.parse(f.dataset.leads) || {}; } catch (e) { return; }
    const lead = m[sel.value];
    f.querySelectorAll('[data-role-tag]:not([data-group-leader])').forEach(s => {
      const on = lead != null && String(lead) === s.dataset.roleTag;
      s.textContent = on ? 'Leader dự án' : 'Hỗ trợ';
      s.classList.toggle('p-mgr', on); s.classList.toggle('p-info', !on);
    });
  }
  // Đề xuất công việc: ô chọn người hỗ trợ chỉ hiện khi dự án chọn là dự án mình làm Leader dự án (select[data-led="id,id"][data-led-box="idBox"]).
  function toggleLed(sel) {
    const box = document.getElementById(sel.dataset.ledBox); if (!box) return;
    const on = (sel.dataset.led || '').split(',').includes(sel.value);
    box.hidden = !on;
    if (!on) { clearPicks(box.querySelector('.pick-chips')); box.querySelectorAll('input[name=self]').forEach(c => { c.checked = true; }); const qr = box.querySelector('[data-pending]'); if (qr) qr.hidden = true; }
  }
  // Chọn nhiều người bằng ô chọn mở ra như ô chọn dự án: chọn một người → hiện thành thẻ có nút ✕ (input ẩn name="emp"), người đó bị ẩn khỏi danh sách.
  function pickPerson(sel) {
    const box = document.getElementById(sel.dataset.pickInto); if (!box || !sel.value) return;
    const val = sel.value, text = sel.options[sel.selectedIndex].textContent;
    if (/^q[dg]:/.test(val)) {
      // Phòng ban / nhóm khác: xin theo số lượng — hiện ô số lượng + nút Thêm (addQty), xong lại chọn tiếp ở ô chọn.
      const row = document.getElementById(sel.dataset.qtyRow);
      if (row) {
        row.hidden = false; row.dataset.pending = val;
        row.querySelector('.qty-name').textContent = text;
        const n = row.querySelector('input'); n.value = 1; n.focus();
      }
      sel.value = '';
      return;
    }
    // Xóa chữ đang lọc trước (danh sách được dựng lại), rồi mới ẩn người vừa chọn.
    const q = sel.previousElementSibling;
    if (q && q.classList.contains('sel-search') && q.value) { q.value = ''; q.dispatchEvent(new Event('input', { bubbles: true })); }
    addChip(box, sel, val, esc(text), 'emp', val);
    sel.value = '';
  }
  function addChip(box, sel, val, label, field, fieldVal) {
    const chip = document.createElement('span');
    chip.className = 'pick-chip';
    chip.dataset.value = val;
    chip.innerHTML = `${label}<input type="hidden" name="${esc(field)}" value="${esc(fieldVal)}"><button type="button" aria-label="Bỏ">✕</button>`;
    box.appendChild(chip);
    const opt = [...sel.options].find(x => x.value === val);
    if (opt) { opt.disabled = true; opt.hidden = true; }
  }
  // Nút "Thêm" của hàng số lượng: tạo thẻ "Phòng Thiết kế cơ ×2" (input ẩn qd_<chỉ số> / qg_<Id Leader> = số lượng).
  document.addEventListener('click', e => {
    const b = e.target.closest('[data-qty-add]'); if (!b) return;
    const row = b.closest('[data-pending], .row') || b.parentNode;
    const val = row.dataset.pending; if (!val) return;
    const qty = parseInt(row.querySelector('input').value, 10);
    if (!(qty > 0)) return;
    const sel = document.querySelector('select[data-qty-row="' + row.id + '"]');
    const box = sel && document.getElementById(sel.dataset.pickInto); if (!box) return;
    const name = row.querySelector('.qty-name').textContent;
    addChip(box, sel, val, `${esc(name)} ×${qty}`, (val.startsWith('qd:') ? 'qd_' : 'qg_') + val.slice(3), String(Math.min(qty, 50)));
    row.hidden = true; row.dataset.pending = '';
  });
  function clearPicks(box) {
    if (!box) return;
    const sel = document.querySelector('select[data-pick-into="' + box.id + '"]');
    box.querySelectorAll('.pick-chip').forEach(c => {
      const o = sel && [...sel.options].find(x => x.value === c.dataset.value);
      if (o) { o.disabled = false; o.hidden = false; }
      c.remove();
    });
  }
  document.addEventListener('click', e => {
    const b = e.target.closest('.pick-chip button'); if (!b) return;
    const chip = b.parentNode, box = chip.parentNode;
    const sel = document.querySelector('select[data-pick-into="' + box.id + '"]');
    const o = sel && [...sel.options].find(x => x.value === chip.dataset.value);
    if (o) { o.disabled = false; o.hidden = false; }
    chip.remove();
  });
  function fillItems(sel) {
    const target = document.getElementById(sel.dataset.itemsFor); if (!target) return;
    const items = (window.QM_ITEMS || {})[sel.value] || [];
    target.innerHTML = `<option value="">— Việc khác (tự nhập) —</option>` + items.map(([id, t]) => `<option value="${esc(id)}">${esc(t)}</option>`).join('');
  }
  function showWhen(sel) {
    const el = document.querySelector(sel.dataset.showTarget); if (!el) return;
    el.hidden = !(sel.dataset.showValues || '').split(',').includes(sel.value);
  }
  // khởi tạo trạng thái hiện/ẩn khi tải trang và khi mở sheet
  const initShow = root => $$('select[data-show-target]', root).forEach(showWhen);
  initShow(document);
  document.addEventListener('qm:sheet-open', e => { initShow(e.target); $$('select[data-led]', e.target).forEach(toggleLed); });

  // ---------- tìm nhanh trong danh sách chọn (yêu cầu 2026-10-15) ----------
  // Danh sách dài (≥ 7 mục: dự án, nhân viên, tỉnh…) có ô gõ chữ cái ở trên: gõ → chỉ còn mục chứa chữ đó (không phân biệt hoa / dấu).
  // Áp cho <select class="in"> và danh sách tích chọn .optbox; bỏ qua select giờ (.mono), tự gửi (data-autosubmit) hoặc data-nosearch.
  const fold = s => String(s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase();
  const MIN_SEARCH = 7;
  function searchBox(label) {
    const b = document.createElement('input');
    b.type = 'search'; b.className = 'in sel-search'; b.autocomplete = 'off';
    b.setAttribute('aria-label', label);
    return b;
  }
  // Ô chọn có tìm kiếm trong CHÍNH ô chọn (một ô duy nhất, không còn ô lọc riêng phía trên — yêu cầu 2026-10-16): <select> gốc vẫn nằm trong form
  // (ẩn, giữ giá trị / kiểm tra required); người dùng gõ chữ cái vào ô → danh sách mục chứa chữ đó, chọn bằng chuột / ↑ ↓ Enter.
  function enhanceSelect(sel) {
    if (sel.dataset.searchOn || sel.multiple || sel.options.length < MIN_SEARCH) return;
    if (sel.matches('.mono,[data-autosubmit],[data-nosearch],[data-pick-into]') || /^(nextDate|date|d|m)$/.test(sel.name || '')) return;
    sel.dataset.searchOn = '1';
    const wrap = document.createElement('div'); wrap.className = 'combo';
    sel.parentNode.insertBefore(wrap, sel); wrap.appendChild(sel);
    sel.classList.add('combo-native'); sel.tabIndex = -1;
    const input = document.createElement('input');
    input.type = 'text'; input.className = 'in combo-input'; input.autocomplete = 'off';
    input.setAttribute('role', 'combobox'); input.setAttribute('aria-expanded', 'false');
    const lbl = sel.id && document.querySelector('label[for="' + sel.id + '"]');
    input.setAttribute('aria-label', (lbl && lbl.textContent.trim()) || 'Chọn');
    input.disabled = sel.disabled;
    const list = document.createElement('div'); list.className = 'combo-list'; list.hidden = true; list.setAttribute('role', 'listbox');
    wrap.insertBefore(input, sel); wrap.appendChild(list);
    let active = -1, shown = [];
    const label = () => { const o = sel.options[sel.selectedIndex]; return o ? o.textContent.trim() : ''; };
    const sync = () => { input.value = label(); input.classList.toggle('ph', !sel.value); };
    // Gán sel.value bằng mã (staff.js khôi phục bản nháp…) cũng làm ô hiển thị đổi theo.
    const desc = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value');
    Object.defineProperty(sel, 'value', { configurable: true, get() { return desc.get.call(this); }, set(v) { desc.set.call(this, v); sync(); } });
    const render = q => {
      shown = [...sel.options].filter(o => !o.disabled && !o.hidden && (!q || fold(o.textContent).includes(q)));
      list.innerHTML = shown.length
        ? shown.map((o, i) => `<div class="combo-item${o.value === sel.value ? ' sel' : ''}" role="option" data-i="${i}">${esc(o.textContent.trim())}</div>`).join('')
        : '<div class="combo-empty">Không có mục phù hợp</div>';
      active = -1;
    };
    const open = q => { render(q); list.hidden = false; input.setAttribute('aria-expanded', 'true'); };
    const close = () => { list.hidden = true; input.setAttribute('aria-expanded', 'false'); sync(); };
    const choose = o => {
      sel.value = o.value; list.hidden = true; input.setAttribute('aria-expanded', 'false');
      sel.dispatchEvent(new Event('change', { bubbles: true }));
    };
    const mark = () => [...list.children].forEach((c, i) => c.classList.toggle('on', i === active));
    input.addEventListener('focus', () => { input.select(); open(''); });
    input.addEventListener('click', () => { if (list.hidden) open(''); });
    input.addEventListener('input', () => open(fold(input.value.trim())));
    input.addEventListener('blur', () => setTimeout(close, 120));
    input.addEventListener('keydown', e => {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault(); if (list.hidden) open('');
        active = (active + (e.key === 'ArrowDown' ? 1 : -1) + shown.length) % Math.max(shown.length, 1); mark();
        const el = list.children[active]; if (el && el.scrollIntoView) el.scrollIntoView({ block: 'nearest' });
      } else if (e.key === 'Enter') { if (!list.hidden) { e.preventDefault(); choose(shown[active >= 0 ? active : 0] || shown[0]); } }
      else if (e.key === 'Escape') { close(); }
    });
    list.addEventListener('mousedown', e => {
      const it = e.target.closest('.combo-item'); if (!it) return;
      e.preventDefault(); choose(shown[+it.dataset.i]);
    });
    sync();
  }
  function enhanceList(box) {
    if (box.dataset.searchOn) return;
    const rows = [...box.children].filter(x => x.matches('.trow'));
    if (rows.length < MIN_SEARCH) return;
    box.dataset.searchOn = '1';
    const input = searchBox('Gõ chữ cái để lọc danh sách');
    box.parentNode.insertBefore(input, box);
    input.addEventListener('input', () => {
      const q = fold(input.value.trim());
      rows.forEach(r => { r.hidden = !!q && !fold(r.textContent).includes(q); });
    });
  }
  function enhanceSearch(root) {
    $$('select.in', root).forEach(enhanceSelect);
    $$('.optbox', root).forEach(enhanceList);
  }
  enhanceSearch(document);
  document.addEventListener('qm:sheet-open', e => enhanceSearch(e.target));
})();
