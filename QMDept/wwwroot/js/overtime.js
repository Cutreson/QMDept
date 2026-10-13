// overtime.js — xem trước mức tăng ca trên form đăng ký làm thêm (#otForm, Views/Overtime/_Mine.cshtml).
// Port nguyên văn otRule / otMealOpts / otLive của artifact. Bản C# tương ứng: Services/Rules.cs (OtRules) và
// ViewModels/Overtime.cs (RuleBox, MealNote) — sửa luật / câu chữ phải sửa cả hai.
// Giá trị option (= OvertimeUi.DayKey / MealKey): loại ngày 'workday' | 'holiday'; báo cơm 'rice' | 'snack' | 'none'.
// Kiểm tra khi gửi chỉ để tiện (giữ nguyên dữ liệu đã nhập); server kiểm tra lại toàn bộ.
(function () {
  'use strict';
  const f = document.getElementById('otForm'); if (!f) return;

  function num(n){ return String(Math.round(n*100)/100).replace('.',',') }
  function hmMin(s){ return +s.slice(0,2)*60 + +s.slice(3,5) }
  function otRule(dayType, a, b){
    if(!a||!b) return {err:'Chọn giờ bắt đầu và giờ kết thúc.'};
    if(hmMin(a)%30||hmMin(b)%30) return {err:'Giờ làm thêm theo bước 30 phút.'};
    if(b<=a) return {err:'Giờ kết thúc phải sau giờ bắt đầu.'};
    if(dayType!=='holiday' && a<'17:00' && b>'08:00') return {err:'Ngày làm việc chỉ đăng ký ngoài giờ hành chính: kết thúc trước 08:00 hoặc bắt đầu từ 17:00.'};
    const lunch = dayType==='holiday' ? Math.max(0, Math.min(hmMin(b),780)-Math.max(hmMin(a),720)) : 0, h=(hmMin(b)-hmMin(a)-lunch)/60; let r;
    if(h<=0) return {err:'Khoảng giờ đã chọn nằm trọn trong nghỉ trưa — không có thời gian làm thêm.'};
    if(h<2) r={tier:`Làm thêm ${num(h)} giờ`, meal:'none', cost:0, allow:0};
    else if(h<3) r={tier:'Tăng ca 2 giờ', meal:'snack', cost:15000, allow:0};
    else if(h<4) r={tier:'Tăng ca 3 đến dưới 4 giờ', meal:'rice', cost:35000, allow:0};
    else r={tier:'Tăng ca từ 4 giờ trở lên', meal:'rice', cost:35000, allow:20000};
    const breaks=[]; if(lunch) breaks.push('12:00–13:00 (nghỉ trưa)');
    if(h>=3 && a<='17:30' && b>='18:00') breaks.push('17:30–18:00 (1 lần)');
    else if(h>=2 && h<3){ if(a==='17:00') breaks.push('17:00–17:10'); else if(a<='07:35' && b>='07:45') breaks.push('07:35–07:45'); }
    return {...r, breaks:breaks.join(', ')||'Theo bố trí của bộ phận', start:a, end:b, hours:h, lunch:!!lunch, dayType};
  }
  // Lựa chọn báo cơm tăng ca theo mức: từ 3 giờ bắt buộc suất (cơm/bún); 2 giờ: ăn nhẹ hoặc cắt cơm; dưới 2 giờ: không có chế độ ăn.
  function otMealOpts(r){ return r.err ? [] : r.meal==='rice' ? [['rice','Cơm'],['none','Không đăng ký']] : r.meal==='snack' ? [['snack','Ăn nhẹ'],['none','Không đăng ký']] : [['none','Không đăng ký']] }
  function otLive(){
    const r=otRule(f.querySelector('#otDay').value, f.querySelector('#otS').value, f.querySelector('#otE').value), box=f.querySelector('#otRule');
    const m=f.querySelector('#otM'), want=m.options.length ? m.value : m.dataset.init, opts=otMealOpts(r);
    m.innerHTML = opts.map(([k,l])=>`<option value="${k}" ${k===want?'selected':''}>${l}</option>`).join(''); m.disabled = opts.length<2; m.dataset.init = m.value;
    f.querySelector('#otMealNote').textContent = r.err ? '' : r.meal==='rice' ? 'Tăng ca từ 3 giờ (đến 20:00 trở đi): cơm.' : r.meal==='snack' ? 'Tăng ca 2 giờ (17:00–19:00): ăn nhẹ, thực đơn cố định theo ngày.' : 'Làm thêm dưới 2 giờ không có chế độ suất ăn.';
    box.classList.toggle('err', !!r.err);
    box.innerHTML = r.err ? r.err : `<b>${r.start}–${r.end}</b> · ${num(r.hours)} giờ${r.lunch?' (đã trừ nghỉ trưa)':''} · ${r.tier}<br>Nghỉ giải lao: ${r.breaks}<br>Suất ăn: ${r.meal==='rice'?'<b>cơm</b>':r.meal==='snack'?'ăn nhẹ, thực đơn cố định theo ngày':'không có'}${r.allow?'<br>Phụ cấp thêm <b>20.000đ</b>, trả vào lương':''}`;
  }

  otLive();
  // artifact: mọi thay đổi trong #otForm → otLive()
  f.addEventListener('change', () => otLive());
  // otForm submit (phần kiểm tra phía trình duyệt, cùng câu thông báo với artifact)
  f.addEventListener('submit', e => {
    const r=otRule(f.querySelector('#otDay').value, f.querySelector('#otS').value, f.querySelector('#otE').value);
    const picked=f.querySelectorAll('input[name=task]:checked').length, extra=f.querySelector('#otC').value.trim();
    const msg = r.err ? r.err : (!picked && !extra) ? 'Tích việc chưa xong cần làm tiếp hoặc nhập nội dung khác' : '';
    if(msg){ e.preventDefault(); e.stopPropagation(); if(window.qmToast) window.qmToast(msg); }
  });
})();
