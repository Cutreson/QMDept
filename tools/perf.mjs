// Đo thời gian tải trang (chạy ở thư mục gốc repo): QM_PW_USER=… QM_PW_ADMIN=… node tools/perf.mjs [base] [rounds]
// Đo thời gian tải trang: node perf.mjs [base] [rounds]. Mật khẩu qua biến môi trường QM_PW_USER / QM_PW_ADMIN.
const BASE = process.argv[2] || 'http://localhost:5125';
const ROUNDS = +(process.argv[3] || 3);
const jar = () => ({ c: new Map() });
const cookieHdr = j => [...j.c].map(([k, v]) => `${k}=${v}`).join('; ');
function keep(j, res) {
  for (const sc of res.headers.getSetCookie?.() ?? []) {
    const [kv] = sc.split(';'); const i = kv.indexOf('='); const k = kv.slice(0, i), v = kv.slice(i + 1);
    if (v === '' || /expires=Thu, 01 Jan 1970/i.test(sc)) j.c.delete(k); else j.c.set(k, v);
  }
}
async function get(j, path) {
  const t0 = performance.now();
  const res = await fetch(BASE + path, { headers: { cookie: cookieHdr(j) }, redirect: 'manual' });
  const body = await res.text(); keep(j, res);
  return { ms: performance.now() - t0, status: res.status, bytes: Buffer.byteLength(body), body };
}
async function login(code, pw) {
  const j = jar();
  const p = await get(j, '/Account/Login');
  const tok = /name="__RequestVerificationToken" type="hidden" value="([^"]+)"/.exec(p.body)?.[1];
  const res = await fetch(BASE + '/Account/Login', { method: 'POST', redirect: 'manual', headers: { cookie: cookieHdr(j), 'content-type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ code, password: pw, __RequestVerificationToken: tok }) });
  keep(j, res); await res.text();
  if (res.status !== 302) throw new Error(`login ${code} -> ${res.status}`);
  return j;
}
const plan = [
  ['tp.leader1.nv1', process.env.QM_PW_USER, ['/Staff/Month', '/Staff/Morning', '/Staff/Evening', '/Staff/Projects', '/Overtime']],
  ['tp.leader1', process.env.QM_PW_USER, ['/Leader/Overview', '/Planning/Approvals', '/Planning/Reports', '/Planning/Week', '/Leader/Tracking', '/Leader/Reports', '/Staff/Month?as=all']],
  ['tp', process.env.QM_PW_USER, ['/Manager/Overview', '/Manager/Approvals', '/Manager/Projects', '/Manager/Reports', '/Overtime?tab=summary']],
  ['admin', process.env.QM_PW_ADMIN, ['/Manager/Overview', '/Admin/Accounts']],
];
const rows = [];
for (const [code, pw, pages] of plan) {
  const j = await login(code, pw);
  for (const path of pages) {
    const times = [];
    let last;
    for (let i = 0; i < ROUNDS; i++) { last = await get(j, path); times.push(Math.round(last.ms)); }
    rows.push({ code, path, status: last.status, kb: (last.bytes / 1024).toFixed(1), first: times[0], rest: times.slice(1).join('/') });
  }
}
console.table(rows);
const firsts = rows.map(r => r.first), rests = rows.flatMap(r => r.rest.split('/').map(Number)).filter(Boolean);
const avg = a => Math.round(a.reduce((s, x) => s + x, 0) / Math.max(1, a.length));
console.log(`avg first=${avg(firsts)}ms  avg repeat=${avg(rests)}ms`);
