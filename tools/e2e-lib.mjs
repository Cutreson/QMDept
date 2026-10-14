// Thư viện E2E dùng chung (Node 18+, không phụ thuộc gói ngoài). Chạy ở thư mục gốc repo:
//   import { Client, vnDate, text, toast, forms, sql } from './e2e-lib.mjs'   (hoặc '../tools/e2e-lib.mjs')
//   const tp = new Client('tp'); await tp.login();                // mật khẩu: admin → QM_PW_ADMIN, còn lại QM_PW_USER
//   const r = await tp.get('/Manager/Overview');                  // { status, location, body, text, toast }
//   const r2 = await tp.post('/Planning/SaveProject?d=2026-10-15', { name: '[E2E3] ...', need: 2 });  // tự lấy antiforgery token
//   forms(r.body)                                                 // liệt kê form + field (đoán tên field POST)
//   await sql('SELECT Id, Name FROM projects')                    // chạy SQL trên DB thật qua tools/dbquery.cs (chậm ~10 s/lần)
// QUY TẮC: chạy TUẦN TỰ (await từng yêu cầu, không Promise.all) — DB thật chặn IP khi có quá nhiều kết nối đồng thời.
// Mật khẩu chỉ lấy từ biến môi trường QM_PW_USER / QM_PW_ADMIN (đừng in ra).
import { execFileSync } from 'node:child_process';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

export const BASE = process.env.QM_BASE || 'http://localhost:5125';

/** Ngày hôm nay theo giờ Việt Nam (UTC+7) + offset ngày, dạng yyyy-MM-dd. */
export function vnDate(offsetDays = 0) {
  return new Date(Date.now() + 7 * 3600e3 + offsetDays * 86400e3).toISOString().slice(0, 10);
}
/** Giờ Việt Nam hiện tại dạng HH:mm. */
export function vnTime() { return new Date(Date.now() + 7 * 3600e3).toISOString().slice(11, 16); }

const decode = s => s.replace(/&quot;/g, '"').replace(/&#39;|&#x27;/g, "'").replace(/&lt;/g, '<').replace(/&gt;/g, '>')
  .replace(/&#x([0-9a-f]+);/gi, (_, h) => String.fromCodePoint(parseInt(h, 16))).replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(+d))
  .replace(/&amp;/g, '&');

/** HTML → văn bản thuần (bỏ script/style/thẻ, gộp khoảng trắng). */
export function text(html) {
  return decode(html.replace(/<(script|style)[\s\S]*?<\/\1>/gi, ' ').replace(/<\/(p|div|li|tr|h\d|section|button)>/gi, '\n').replace(/<[^>]+>/g, ' '))
    .replace(/[ \t\r\f\v]+/g, ' ').replace(/ ?\n ?/g, '\n').replace(/\n{2,}/g, '\n').trim();
}
/** Nội dung toast (TempData) hiển thị trong trang, '' nếu không có. */
export function toast(html) {
  const m = /<div id="toast"[^>]*data-msg="([^"]*)"/.exec(html);
  return m ? decode(m[1]) : '';
}
/** Phân tích các <form>: [{ action, method, fields: [{ name, type, value, options? }] }]. */
export function forms(html) {
  const out = [];
  for (const fm of html.matchAll(/<form\b([^>]*)>([\s\S]*?)<\/form>/gi)) {
    const attr = n => new RegExp(`\\b${n}="([^"]*)"`, 'i').exec(fm[1])?.[1];
    const fields = [];
    for (const im of fm[2].matchAll(/<input\b([^>]*)>/gi)) {
      const a = n => new RegExp(`\\b${n}="([^"]*)"`, 'i').exec(im[1])?.[1];
      if (a('name')) fields.push({ name: a('name'), type: a('type') || 'text', value: decode(a('value') ?? '') });
    }
    for (const tm of fm[2].matchAll(/<textarea\b([^>]*)>([\s\S]*?)<\/textarea>/gi)) {
      const n = /\bname="([^"]*)"/i.exec(tm[1])?.[1]; if (n) fields.push({ name: n, type: 'textarea', value: decode(tm[2]) });
    }
    for (const sm of fm[2].matchAll(/<select\b([^>]*)>([\s\S]*?)<\/select>/gi)) {
      const n = /\bname="([^"]*)"/i.exec(sm[1])?.[1];
      if (n) fields.push({ name: n, type: 'select', value: '', options: [...sm[2].matchAll(/<option\b([^>]*)>/gi)].map(o => /\bvalue="([^"]*)"/i.exec(o[1])?.[1] ?? '') });
    }
    out.push({ action: decode(attr('action') ?? ''), method: (attr('method') || 'get').toLowerCase(), fields });
  }
  return out;
}

export class Client {
  constructor(code) { this.code = code; this.jar = new Map(); }
  #cookie() { return [...this.jar].map(([k, v]) => `${k}=${v}`).join('; '); }
  #keep(res) {
    for (const sc of res.headers.getSetCookie?.() ?? []) {
      const [kv] = sc.split(';'); const i = kv.indexOf('='); const k = kv.slice(0, i), v = kv.slice(i + 1);
      if (v === '' || /expires=Thu, 01 Jan 1970/i.test(sc)) this.jar.delete(k); else this.jar.set(k, v);
    }
  }
  async #wrap(res) {
    this.#keep(res);
    const body = await res.text();
    return { status: res.status, location: res.headers.get('location') || '', body, text: text(body), toast: toast(body) };
  }
  /** GET (không tự theo chuyển hướng — dùng follow()). */
  async get(path) {
    return this.#wrap(await fetch(BASE + path, { headers: { cookie: this.#cookie() }, redirect: 'manual' }));
  }
  /** GET trang đích của chuyển hướng (PRG) — toast của lần POST hiện ở đây. */
  async follow(r) { return r.location ? this.get(r.location.replace(BASE, '')) : r; }
  /** POST form (urlencoded). fields: object hoặc mảng [[k,v],...] (khóa lặp). Tự lấy antiforgery token từ tokenPage. */
  async post(path, fields = {}, { tokenPage = '/Settings', token } = {}) {
    token ??= /name="__RequestVerificationToken" type="hidden" value="([^"]+)"/.exec((await this.get(tokenPage)).body)?.[1];
    if (!token) throw new Error(`Không lấy được antiforgery token từ ${tokenPage} (chưa đăng nhập?)`);
    const body = new URLSearchParams();
    for (const [k, v] of Array.isArray(fields) ? fields : Object.entries(fields)) body.append(k, String(v ?? ''));
    body.append('__RequestVerificationToken', token);
    return this.#wrap(await fetch(BASE + path, { method: 'POST', redirect: 'manual',
      headers: { cookie: this.#cookie(), 'content-type': 'application/x-www-form-urlencoded' }, body }));
  }
  /** POST rồi theo chuyển hướng: trả về trang đích (có toast). */
  async postFollow(path, fields, opt) { return this.follow(await this.post(path, fields, opt)); }
  async login(password) {
    password ??= this.code === 'admin' ? process.env.QM_PW_ADMIN : process.env.QM_PW_USER;
    if (!password) throw new Error('Thiếu QM_PW_USER / QM_PW_ADMIN trong môi trường');
    this.jar.clear();
    const r = await this.post('/Account/Login', { code: this.code, password }, { tokenPage: '/Account/Login' });
    if (r.status !== 302 || /\/Account\/Login/i.test(r.location)) throw new Error(`Đăng nhập ${this.code} thất bại: ${r.status} ${r.text.slice(0, 200)}`);
    return r;
  }
}

/** Chạy một hoặc nhiều câu SQL trên DB thật (mỗi câu một dòng) qua tools/dbquery.cs; trả về mảng dòng kết quả. Chậm (~10 s). */
export function sql(...stmts) {
  const dir = mkdtempSync(join(tmpdir(), 'qmsql-'));
  const f = join(dir, 'q.sql');
  writeFileSync(f, stmts.map(s => s.replace(/\s*\n\s*/g, ' ')).join('\n') + '\n', 'utf8');
  const out = execFileSync('dotnet', ['run', 'tools/dbquery.cs', f], {
    encoding: 'utf8', timeout: 240000,
    env: { ...process.env, DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK: '1', DOTNET_NOLOGO: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1' },
  });
  return out.split(/\r?\n/).filter(Boolean);
}
