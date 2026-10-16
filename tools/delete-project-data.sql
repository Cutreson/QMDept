-- XÓA TOÀN BỘ DỮ LIỆU DỰ ÁN + DỮ LIỆU GIAO VIỆC của mọi nhân sự trên DB thật (yêu cầu chủ dự án 2026-10-16). KHÔNG THỂ HOÀN TÁC.
-- Xóa: đề xuất lịch, phát sinh, việc trong lịch (và liên kết kế hoạch ngày), kế hoạch đầu ngày, báo cáo cuối ngày, báo cáo của Leader,
--      nhu cầu nhân sự, phân công, đầu mục, dự án.
-- Giữ nguyên: tài khoản, báo nghỉ, đăng ký làm thêm, cài đặt thông báo.
-- Chạy:  dotnet run tools/dbquery.cs tools/delete-project-data.sql   (mỗi câu một dòng)
-- Sau khi chạy phải chạy lại web:  bash tools/web.sh start   (bộ nhớ đệm ~60 giây)
DELETE FROM calendar_proposal_people
DELETE FROM calendar_proposals
DELETE FROM extras
DELETE FROM day_plan_tasks
DELETE FROM day_plans
DELETE FROM day_reports
DELETE FROM leader_reports
DELETE FROM work_tasks
DELETE FROM project_needs
DELETE FROM assignments
DELETE FROM plan_items
DELETE FROM projects
-- còn lại
SELECT COUNT(*) AS du_an FROM projects
SELECT COUNT(*) AS viec FROM work_tasks
SELECT COUNT(*) AS phan_cong FROM assignments
SELECT COUNT(*) AS de_xuat FROM calendar_proposals
SELECT COUNT(*) AS ke_hoach_ngay FROM day_plans
SELECT COUNT(*) AS bao_cao_cuoi_ngay FROM day_reports
SELECT COUNT(*) AS tai_khoan FROM people WHERE IsDeleted = 0
