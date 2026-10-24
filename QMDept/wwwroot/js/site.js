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
  function toast(t, sticky) {
    if (!toastEl || !t) return;
    toastEl.textContent = t; toastEl.hidden = false; toastEl.classList.toggle('sticky', !!sticky);
    clearTimeout(toast.t);
    if (sticky) { // lỗi / thiếu điều kiện: giữ lại tới khi người dùng bấm vào màn hình
      const off = () => { toastEl.hidden = true; toastEl.classList.remove('sticky'); document.removeEventListener('pointerdown', off, true); document.removeEventListener('keydown', off, true); };
      setTimeout(() => { document.addEventListener('pointerdown', off, true); document.addEventListener('keydown', off, true); }, 400);
    } else toast.t = setTimeout(() => toastEl.hidden = true, 3200);
  }
  window.qmToast = toast;
  if (toastEl && toastEl.dataset.msg) toast(toastEl.dataset.msg, toastEl.dataset.sticky === '1');

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
  // Bàn phím ảo: bảng trượt luôn nằm gọn trong phần nhìn thấy (visualViewport) — bảng nở lên phía trên bàn phím, không bị che (yêu cầu 2026-10-16).
  const vv = window.visualViewport;
  const fitViewport = () => {
    const r = document.documentElement.style;
    if (vv) { r.setProperty('--vvh', vv.height + 'px'); r.setProperty('--vvt', vv.offsetTop + 'px'); }
    window.qmRevealOpen && window.qmRevealOpen();
  };
  if (vv) { vv.addEventListener('resize', fitViewport); vv.addEventListener('scroll', fitViewport); fitViewport(); }
  // Cuộn để phần đáy của el (danh sách đang mở) nằm trên bàn phím nhưng không đẩy ô nhập khỏi màn hình.
  window.qmReveal = (el, anchor) => {
    const top0 = vv ? vv.offsetTop : 0, bottom = top0 + (vv ? vv.height : innerHeight);
    let over = el.getBoundingClientRect().bottom - bottom + 8; if (over <= 0) return;
    over = Math.min(over, Math.max(anchor.getBoundingClientRect().top - top0 - 8, 0));
    const sc = el.closest('.dlg>.card'); if (sc) sc.scrollTop += over; else window.scrollBy(0, over);
  };
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
        if (req && !v) { toast('Nhập ' + (f.dataset.promptLabel || 'nội dung').toLowerCase(), true); return; }
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
    conn.on('notify', n => { window.qmShowNotif(n); const bn = document.querySelector('.bell .bn'); if (bn) { bn.textContent = String((parseInt(bn.textContent, 10) || 0) + 1); bn.hidden = false; } });
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
    if (sel.dataset.searchOn || sel.multiple || (sel.options.length < MIN_SEARCH && !sel.hasAttribute('data-combo'))) return;
    // select tự gửi (data-autosubmit) chỉ có ô tìm khi gắn data-combo (bộ lọc dự án / người: gõ chữ, chọn, tự lọc — như "Giao thêm việc")
    if (sel.matches('.mono,[data-nosearch],[data-pick-into]') || (sel.matches('[data-autosubmit]') && !sel.hasAttribute('data-combo')) || /^(nextDate|date|d|m)$/.test(sel.name || '')) return;
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
    const reveal = () => { if (!list.hidden && window.qmReveal) window.qmReveal(list, input); };
    const open = q => { render(q); list.hidden = false; input.setAttribute('aria-expanded', 'true'); window.qmRevealOpen = reveal; requestAnimationFrame(reveal); setTimeout(reveal, 350); };
    const close = () => { if (window.qmRevealOpen === reveal) window.qmRevealOpen = null; list.hidden = true; input.setAttribute('aria-expanded', 'false'); sync(); };
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
  // Ô nhập tự do kiểu combo (Khách hàng / Địa điểm của dự án): gõ chữ → gợi ý mục đã có (không phân biệt hoa / dấu), bấm chọn hoặc cứ gõ mới.
  // Nguồn gợi ý = <datalist> gắn qua list=; bỏ list để không hiện gợi ý gốc của trình duyệt.
  function enhanceFree(inp) {
    if (inp.dataset.searchOn || !inp.getAttribute('list')) return;
    const dl = document.getElementById(inp.getAttribute('list')); if (!dl) return;
    inp.dataset.searchOn = '1'; inp.removeAttribute('list');
    const items = [...dl.options].map(o => o.value).filter(Boolean);
    const wrap = document.createElement('div'); wrap.className = 'combo';
    inp.parentNode.insertBefore(wrap, inp); wrap.appendChild(inp);
    const list = document.createElement('div'); list.className = 'combo-list'; list.hidden = true; list.setAttribute('role', 'listbox'); wrap.appendChild(list);
    let active = -1, shown = [];
    const render = () => {
      const q = fold(inp.value.trim());
      shown = items.filter(v => !q || fold(v).includes(q)).slice(0, 60);
      list.innerHTML = shown.map((v, i) => `<div class="combo-item" role="option" data-i="`+i+`">`+esc(v)+`</div>`).join('');
      list.hidden = shown.length === 0; active = -1;
    };
    const mark = () => [...list.children].forEach((c, i) => c.classList.toggle('on', i === active));
    inp.addEventListener('focus', render); inp.addEventListener('input', render);
    inp.addEventListener('blur', () => setTimeout(() => { list.hidden = true; }, 120));
    inp.addEventListener('keydown', e => {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); if (list.hidden) render(); active = (active + (e.key === 'ArrowDown' ? 1 : -1) + shown.length) % Math.max(shown.length, 1); mark(); }
      else if (e.key === 'Enter' && !list.hidden && active >= 0) { e.preventDefault(); inp.value = shown[active]; list.hidden = true; }
      else if (e.key === 'Escape') list.hidden = true;
    });
    list.addEventListener('mousedown', e => { const it = e.target.closest('.combo-item'); if (!it) return; e.preventDefault(); inp.value = shown[+it.dataset.i]; list.hidden = true; });
  }
  function enhanceSearch(root) {
    $$('select.in', root).forEach(enhanceSelect);
    $$('input[data-combo-free]', root).forEach(enhanceFree);
    $$('.optbox', root).forEach(enhanceList);
  }
  enhanceSearch(document);
  document.addEventListener('qm:sheet-open', e => enhanceSearch(e.target));
})();

// ---------- ô nhập ngày dd/MM/yyyy (Ui.DateInput) ----------
// Luôn hiển thị dd/MM/yyyy (không phụ thuộc ngôn ngữ trình duyệt): ô chữ tự chèn dấu "/", nút lịch mở bộ chọn ngày gốc rồi ghi lại theo dd/MM/yyyy.
(function () {
  function mask(el) {
    const v = el.value.replace(/\D/g, '').slice(0, 8);
    el.value = v.length > 4 ? v.slice(0, 2) + '/' + v.slice(2, 4) + '/' + v.slice(4) : v.length > 2 ? v.slice(0, 2) + '/' + v.slice(2) : v;
  }
  document.addEventListener('input', e => { const t = e.target; if (t.matches && t.matches('input[data-date]') && !(e.inputType || '').startsWith('delete')) mask(t); });
  document.addEventListener('click', e => {
    const b = e.target.closest && e.target.closest('[data-date-pick]'); if (!b) return;
    const wrap = b.closest('.dateinp'), txt = wrap.querySelector('input[data-date]'), nat = wrap.querySelector('.dp-native');
    const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(txt.value); nat.value = m ? `${m[3]}-${m[2]}-${m[1]}` : '';
    if (nat.showPicker) { try { nat.showPicker(); return; } catch (err) { /* rơi xuống focus */ } }
    nat.focus(); nat.click();
  });
  document.addEventListener('change', e => {
    const t = e.target; if (!(t.matches && t.matches('.dp-native')) || !t.value) return;
    const [y, mo, d] = t.value.split('-'); const txt = t.closest('.dateinp').querySelector('input[data-date]');
    txt.value = `${d}/${mo}/${y}`; txt.dispatchEvent(new Event('change', { bubbles: true }));
  });
})();

// ---------- dòng "Leader dự án" dưới ô chọn dự án (Ui.ProjectLeadHint) ----------
(function () {
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  function paint(h, sel) {
    let m = {}; try { m = JSON.parse(h.dataset.map) || {}; } catch (e) { return; }
    const n = sel ? m[sel.value] : '';
    h.innerHTML = n ? 'Leader dự án: <b>' + esc(n) + '</b>' : 'Chưa có Leader dự án';
  }
  const init = root => (root || document).querySelectorAll('.pj-lead[data-pj-lead-for]').forEach(h => {
    const f = h.closest('form') || document; paint(h, f.querySelector('#' + CSS.escape(h.dataset.pjLeadFor)));
  });
  document.addEventListener('change', e => {
    const t = e.target; if (!t.id || !t.matches || !t.matches('select')) return;
    const f = t.closest('form') || document;
    f.querySelectorAll('.pj-lead[data-pj-lead-for="' + CSS.escape(t.id) + '"]').forEach(h => paint(h, t));
  });
  document.addEventListener('qm:sheet-open', e => init(e.target));
  init(document);
})();

// ---------- sơ đồ nhân sự: kéo thả nhân viên sang nhóm khác (TP; Planning/People) ----------
// Thả thẻ .org-card[draggable] vào cột .org-col[data-leader] → gửi form #orgMove (POST /Planning/MoveToTeam); ô chọn nhóm trên thẻ làm cùng việc cho điện thoại.
(function () {
  const root = document.querySelector('.org[data-org="edit"]'); if (!root) return;
  let dragEmp = null;
  root.addEventListener('dragstart', e => {
    const c = e.target.closest && e.target.closest('.org-card[draggable=true]'); if (!c) return;
    dragEmp = c.dataset.emp; c.classList.add('dragging'); e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', dragEmp);
  });
  root.addEventListener('dragend', e => { dragEmp = null; root.querySelectorAll('.dragging,.over').forEach(x => x.classList.remove('dragging', 'over')); });
  root.addEventListener('dragover', e => { const col = e.target.closest('.org-col'); if (!col || dragEmp === null) return; e.preventDefault(); col.classList.add('over'); });
  root.addEventListener('dragleave', e => { const col = e.target.closest('.org-col'); if (col && !col.contains(e.relatedTarget)) col.classList.remove('over'); });
  root.addEventListener('drop', e => {
    const col = e.target.closest('.org-col'); if (!col || dragEmp === null) return;
    e.preventDefault(); col.classList.remove('over');
    const card = root.querySelector('.org-card[data-emp="' + dragEmp + '"]');
    if (!card || card.closest('.org-col') === col) return;   // thả lại đúng nhóm cũ → không làm gì
    const f = document.getElementById('orgMove'); if (!f) return;
    f.elements.emp.value = dragEmp; f.elements.leaderId.value = col.dataset.leader || '';
    f.requestSubmit ? f.requestSubmit() : f.submit();
  });
})();

// ---------- Đề xuất công việc / xin nghỉ gộp một sheet (Views/Shared/_ProposalSheet, _LeaveFields, Staff/Month ownTaskTpl) ----------
// Ô "Loại" = Xin nghỉ → hiện phần nghỉ (lý do bắt buộc); nghỉ nửa ngày / vài giờ → hiện thêm phần nội dung công việc; nghỉ cả ngày → ẩn phần công việc.
(function () {
  const sync = f => {
    const k = f.querySelector('[data-kind]'); if (!k) return;
    const leave = k.value === 'leave';
    const lf = f.querySelector('[data-leave-fields]'), wb = f.querySelector('[data-work-block]');
    let partial = false;
    if (lf) {
      lf.hidden = !leave;
      const t = f.querySelector('[data-leave-type]'), paid = !t || t.value === 'Paid';
      const p = lf.querySelector('[data-leave-paid]'), u = lf.querySelector('[data-leave-unpaid]');
      if (p) p.hidden = !paid; if (u) u.hidden = paid;
      const r = lf.querySelector('textarea'); if (r) r.required = leave;
      const s = lf.querySelector('[data-leave-session]'), a = lf.querySelector('[data-leave-from]'), b = lf.querySelector('[data-leave-to]');
      partial = paid ? !!s && s.value !== 'FullDay' : !!a && !!b && !(a.value === '08:00' && b.value === '17:00');
    }
    if (wb) {
      wb.hidden = leave && !partial;
      const h = wb.querySelector('[data-work-title]'); if (h) h.hidden = !leave;
    }
    const ok = f.querySelector('[data-lbl-leave]');
    if (ok) { if (!ok.dataset.lblWork) ok.dataset.lblWork = ok.textContent; ok.textContent = leave ? ok.dataset.lblLeave : ok.dataset.lblWork; }
  };
  document.addEventListener('change', e => { const t = e.target; if (t.matches && t.matches('[data-kind],[data-leave-type],[data-leave-session],[data-leave-from],[data-leave-to]') && t.form) sync(t.form); });
  document.addEventListener('qm:sheet-open', e => { if (e.target.querySelector && e.target.querySelector('[data-kind]')) sync(e.target); });
})();

// ---------- Sao chép báo cáo tổng hợp (Views/Staff/Evening): [data-copy="id1 id2 …"] ghép văn bản các <pre> theo thứ tự rồi chép vào bộ nhớ tạm ----------
(function () {
  const fallback = text => { const t = document.createElement('textarea'); t.value = text; t.style.position = 'fixed'; t.style.opacity = '0'; document.body.appendChild(t); t.select(); let ok = false; try { ok = document.execCommand('copy'); } catch (e) { } t.remove(); return ok; };
  document.addEventListener('click', e => {
    const b = e.target.closest('[data-copy]'); if (!b) return;
    e.preventDefault();
    const text = b.dataset.copy.split(/\s+/).map(id => { const el = document.getElementById(id); return el ? el.textContent.trim() : ''; }).filter(Boolean).join('\n\n');
    const done = ok => { const old = b.dataset.label || b.textContent; b.dataset.label = old; b.textContent = ok ? 'Đã sao chép ✓' : 'Không sao chép được — hãy bôi đen và copy'; clearTimeout(b._t); b._t = setTimeout(() => { b.textContent = old; }, 1800); };
    if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(text).then(() => done(true), () => done(fallback(text)));
    else done(fallback(text));
  });
})();

// ---------- nhập nhiều công việc trong một bảng + chống trùng giờ (yêu cầu 2026-10-24) ----------
(function () {
  const min = hhmm => { const [h, m] = (hhmm || '0:0').split(':'); return (+h) * 60 + (+m); };
  const rowsOf = box => Array.from(box.querySelectorAll('.tl-row'));
  const val = (r, n) => (r.querySelector('[name=' + n + ']') || {}).value || '';
  // trả về thông báo lỗi trùng giờ giữa các dòng (và đánh dấu đỏ), '' nếu ổn
  function check(box) {
    const rs = rowsOf(box); rs.forEach(r => r.classList.remove('bad'));
    const used = rs.map((r, i) => ({ r, i, t: val(r, 'ltitle').trim(), a: min(val(r, 'lstart')), b: min(val(r, 'lend')) })).filter(x => x.t);
    let msg = '';
    used.forEach(x => { if (x.b <= x.a) { x.r.classList.add('bad'); msg = msg || `Công việc ${x.i + 1}: giờ kết thúc phải sau giờ bắt đầu`; } });
    for (let i = 0; i < used.length; i++) for (let j = i + 1; j < used.length; j++) {
      const p = used[i], q = used[j];
      if (p.a < q.b && q.a < p.b) { p.r.classList.add('bad'); q.r.classList.add('bad'); msg = msg || `Trùng khung giờ: công việc ${p.i + 1} (${val(p.r, 'lstart')}–${val(p.r, 'lend')}) và công việc ${q.i + 1} (${val(q.r, 'lstart')}–${val(q.r, 'lend')}) — chỉnh lại giờ`; }
    }
    const w = box.querySelector('.tl-warn'); if (w) { w.textContent = msg; w.hidden = !msg; }
    return msg;
  }
  const setSel = (sel, v) => { if (Array.from(sel.options).some(o => o.value === v)) sel.value = v; };
  const hhmm = m => String(Math.floor(m / 60)).padStart(2, '0') + ':' + String(m % 60).padStart(2, '0');
  document.addEventListener('click', e => {
    const add = e.target.closest('[data-tl-add]'), del = e.target.closest('[data-tl-del]');
    if (!add && !del) return;
    const box = e.target.closest('[data-tl]'); if (!box) return;
    e.preventDefault();
    if (add) {
      const rs = rowsOf(box), last = rs[rs.length - 1], row = last.cloneNode(true);
      row.classList.remove('bad'); row.querySelector('[name=ltitle]').value = '';
      // dòng mới bắt đầu ngay sau giờ kết thúc của dòng trước (tối đa 1 giờ, không quá 24:00)
      const s = Math.min(min(val(last, 'lend')), 23 * 60), en = Math.min(s + 60, 24 * 60);
      setSel(row.querySelector('[name=lstart]'), hhmm(s)); setSel(row.querySelector('[name=lend]'), hhmm(en));
      last.after(row); row.querySelector('[name=ltitle]').focus(); check(box);
    } else {
      const rs = rowsOf(box);
      if (rs.length > 1) del.closest('.tl-row').remove(); else del.closest('.tl-row').querySelector('[name=ltitle]').value = '';
      check(box);
    }
  });
  document.addEventListener('change', e => { const box = e.target.closest && e.target.closest('[data-tl]'); if (box) check(box); });
  document.addEventListener('input', e => { const box = e.target.closest && e.target.closest('[data-tl]'); if (box && e.target.name === 'ltitle') check(box); });
  // chặn gửi khi còn trùng giờ (chạy trước các xử lý submit khác)
  document.addEventListener('submit', e => {
    const f = e.target; if (!(f instanceof HTMLFormElement)) return;
    for (const box of f.querySelectorAll('[data-tl]')) {
      const msg = check(box);
      if (msg) { e.preventDefault(); e.stopImmediatePropagation(); window.qmToast && window.qmToast(msg, true); return; }
    }
  }, true);
})();
