// ot.js — xem trước mức tăng ca trên form đăng ký làm thêm (#otForm, Views/LamThem/_Mine.cshtml).
// Port nguyên văn otRule / otMealOpts / otLive của artifact. Bản C# tương ứng: Services/Rules.cs (OtRules) và
// ViewModels/LamThem.cs (RuleBox, MealNote) — sửa luật / câu chữ phải sửa cả hai.
// Giá trị option giữ khóa của artifact: loại ngày 'lv' | 'nghi'; báo cơm 'com' | 'nhe' | 'khong'.
// Kiểm tra khi gửi chỉ để tiện (giữ nguyên dữ liệu đã nhập); server kiểm tra lại toàn bộ.
(function () {
  'use strict';
  const f = document.getElementById('otForm'); if (!f) return;

  function num(n){ return String(Math.round(n*100)/100).replace('.',',') }
  function hmMin(s){ return +s.slice(0,2)*60 + +s.slice(3,5) }
  function otRule(dayType, a, b){
    if(!a||!b) return {err:'Chọn giờ bắt đầu và giờ kết thúc.'};
    if(b<=a) return {err:'Giờ kết thúc phải sau giờ bắt đầu.'};
    if(dayType!=='nghi' && a<'17:00' && b>'08:00') return {err:'Ngày làm việc chỉ đăng ký ngoài giờ hành chính: kết thúc trước 08:00 hoặc bắt đầu từ 17:00.'};
    const lunch = dayType==='nghi' ? Math.max(0, Math.min(hmMin(b),780)-Math.max(hmMin(a),720)) : 0, h=(hmMin(b)-hmMin(a)-lunch)/60; let r;
    if(h<2) r={tier:`Làm thêm ${num(h)} giờ`, meal:'khong', cost:0, allow:0};
    else if(h<3) r={tier:'Tăng ca 2 giờ', meal:'nhe', cost:15000, allow:0};
    else if(h<4) r={tier:'Tăng ca 3 đến dưới 4 giờ', meal:'com', cost:35000, allow:0};
    else r={tier:'Tăng ca từ 4 giờ trở lên', meal:'com', cost:35000, allow:20000};
    const br=[]; if(lunch) br.push('12:00–13:00 (nghỉ trưa)');
    if(h>=3 && a<='17:30' && b>='18:00') br.push('17:30–18:00 (1 lần)');
    else if(h>=2 && h<3){ if(a==='17:00') br.push('17:00–17:10'); else if(a<='07:35' && b>='07:45') br.push('07:35–07:45'); }
    return {...r, brk:br.join(', ')||'Theo bố trí của bộ phận', start:a, end:b, hours:h, lunch:!!lunch, dayType};
  }
  // Lựa chọn báo cơm tăng ca theo mức: từ 3 giờ bắt buộc suất (cơm/bún); 2 giờ: ăn nhẹ hoặc cắt cơm; dưới 2 giờ: không có chế độ ăn.
  function otMealOpts(r){ return r.err ? [] : r.meal==='com' ? [['com','Cơm'],['khong','Không đăng ký']] : r.meal==='nhe' ? [['nhe','Ăn nhẹ'],['khong','Không đăng ký']] : [['khong','Không đăng ký']] }
  function otLive(){
    const r=otRule(f.querySelector('#otK').value, f.querySelector('#otS').value, f.querySelector('#otE').value), box=f.querySelector('#otRule');
    const m=f.querySelector('#otM'), want=m.options.length ? m.value : m.dataset.init, opts=otMealOpts(r);
    m.innerHTML = opts.map(([k,l])=>`<option value="${k}" ${k===want?'selected':''}>${l}</option>`).join(''); m.disabled = opts.length<2; m.dataset.init = m.value;
    f.querySelector('#otMealNote').textContent = r.err ? '' : r.meal==='com' ? 'Tăng ca từ 3 giờ (đến 20:00 trở đi): cơm.' : r.meal==='nhe' ? 'Tăng ca 2 giờ (17:00–19:00): ăn nhẹ, thực đơn cố định theo ngày.' : 'Làm thêm dưới 2 giờ không có chế độ suất ăn.';
    box.classList.toggle('err', !!r.err);
    box.innerHTML = r.err ? r.err : `<b>${r.start}–${r.end}</b> · ${num(r.hours)} giờ${r.lunch?' (đã trừ nghỉ trưa)':''} · ${r.tier}<br>Nghỉ giải lao: ${r.brk}<br>Suất ăn: ${r.meal==='com'?'<b>cơm</b>':r.meal==='nhe'?'ăn nhẹ, thực đơn cố định theo ngày':'không có'}${r.allow?'<br>Phụ cấp thêm <b>20.000đ</b>, trả vào lương':''}`;
  }

  otLive();
  // artifact: mọi thay đổi trong #otForm → otLive()
  f.addEventListener('change', () => otLive());
  // otForm submit (phần kiểm tra phía trình duyệt, cùng câu thông báo với artifact)
  f.addEventListener('submit', e => {
    const r=otRule(f.querySelector('#otK').value, f.querySelector('#otS').value, f.querySelector('#otE').value);
    const picked=f.querySelectorAll('input[name=task]:checked').length, extra=f.querySelector('#otC').value.trim();
    const msg = r.err ? r.err : (!picked && !extra) ? 'Tích việc chưa xong cần làm tiếp hoặc nhập nội dung khác' : '';
    if(msg){ e.preventDefault(); e.stopPropagation(); if(window.qmToast) window.qmToast(msg); }
  });
})();
