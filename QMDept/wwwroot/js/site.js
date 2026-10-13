// QMDept — hành vi dùng chung cho mọi trang (không cần thư viện ngoài).
// Quy ước data-attribute (dùng trong Razor):
//   data-autosubmit                 trên <select>/<input>: đổi giá trị → submit form chứa nó.
//   form[data-confirm="câu hỏi"]    hỏi xác nhận trước khi gửi; data-confirm-label = nhãn nút (mặc định "Đồng ý").
//   form[data-prompt="Tiêu đề"]     mở hộp nhập văn bản; data-prompt-label, data-prompt-value, data-prompt-required,
//                                   data-prompt-name (tên field ẩn nhận giá trị, mặc định "text").
//   [data-sheet="templateId"]       bấm → mở <template id="templateId"> (chứa 1 <form>) trong bảng trượt.
//   input[type=file][data-shrink]   ảnh được thu nhỏ ≤1600px JPEG trước khi gửi (giống shrink() của artifact).
//   select[data-items-for="id"]     đổi dự án → nạp lại select #id bằng window.QM_ITEMS[projectId] = [[id,title],...].
//   select[data-show-target="#sel"][data-show-values="A,B"]  hiện phần tử #sel khi giá trị thuộc danh sách.
//   form[data-once]                 khóa nút submit sau khi gửi (mặc định cho mọi form post).
(function () {
  'use strict';
  const $ = (s, r) => (r || document).querySelector(s);
  const $$ = (s, r) => [...(r || document).querySelectorAll(s)];
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

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
  // Khóa nút submit sau khi gửi (tránh bấm 2 lần); với form có ảnh, thu nhỏ ảnh trước.
  function lockSubmit(f, e) {
    if ((f.method || '').toLowerCase() !== 'post') return;
    const files = $$('input[type=file][data-shrink]', f).filter(i => i.files && i.files.length);
    if (files.length && !f.dataset.shrunk) {
      e.preventDefault();
      Promise.all(files.map(shrinkInput)).finally(() => { f.dataset.shrunk = '1'; f.dataset.qmPassed = '1'; if (f.requestSubmit) f.requestSubmit(); else f.submit(); });
      return;
    }
    const btn = e.submitter || f.querySelector('button:not([type=button])');
    if (btn) { setTimeout(() => { btn.disabled = true; btn.dataset.label = btn.textContent; btn.textContent = 'Đang lưu…'; }, 0); }
  }

  // ---------- thu nhỏ ảnh ----------
  async function shrink(file) {
    try {
      const bmp = await createImageBitmap(file);
      const k = Math.min(1, 1600 / Math.max(bmp.width, bmp.height));
      const c = document.createElement('canvas'); c.width = Math.round(bmp.width * k); c.height = Math.round(bmp.height * k);
      c.getContext('2d').drawImage(bmp, 0, 0, c.width, c.height);
      const blob = await new Promise(r => c.toBlob(b => r(b), 'image/jpeg', .82));
      return blob ? new File([blob], (file.name || 'anh').replace(/\.\w+$/, '') + '.jpg', { type: 'image/jpeg' }) : file;
    } catch { return file; }
  }
  async function shrinkInput(input) {
    const files = [...input.files].slice(0, 4);
    const out = await Promise.all(files.map(shrink));
    try { const dt = new DataTransfer(); out.forEach(f => dt.items.add(f)); input.files = dt.files; } catch { /* trình duyệt cũ: gửi ảnh gốc */ }
  }

  // ---------- change handlers ----------
  document.addEventListener('change', e => {
    const t = e.target;
    if (t.matches && t.matches('[data-autosubmit]')) { const f = t.form; if (f) { if (f.requestSubmit) f.requestSubmit(); else f.submit(); } }
    if (t.matches && t.matches('select[data-items-for]')) fillItems(t);
    if (t.matches && t.matches('select[data-show-target]')) showWhen(t);
    if (t.matches && t.matches('select.in')) t.classList.toggle('ph', !t.value);
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
  document.addEventListener('qm:sheet-open', e => initShow(e.target));
})();
