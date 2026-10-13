-- XÓA TOÀN BỘ DỮ LIỆU TEST trên DB thật (chủ dự án đã đồng ý 2026-10-13 — tại thời điểm đó mọi dữ liệu nghiệp vụ đều là dữ liệu test
-- E2E: dự án/việc có tiền tố "[E2E-…]" / "[E2E2-…]", tài khoản "zz.*").
-- GIỮ 5 tài khoản chính Id 1–5: tp, tp.leader1, tp.leader1.nv1, tp.leader1.nv2, admin.
-- KIỂM TRA TRƯỚC KHI CHẠY: nếu đã có dữ liệu thật (người dùng đã dùng web qlcl.runasp.net), KHÔNG chạy file này mà xóa có chọn lọc
-- theo tiền tố "[E2E" (projects.Name, work_tasks.Title, …) và people.Code LIKE 'zz.%'.
-- Chạy: dotnet run tools/dbquery.cs tools/cleanup-test-data.sql   (mỗi câu một dòng)
-- trước khi xóa: số dòng hiện có
SELECT table_name, table_rows FROM information_schema.tables WHERE table_schema = DATABASE() ORDER BY table_name
SELECT Id, Name FROM projects ORDER BY Id
SELECT Id, Code, IsDeleted FROM people ORDER BY Id
SET FOREIGN_KEY_CHECKS = 0
DELETE FROM overtime_tasks
DELETE FROM overtimes
DELETE FROM ot_sends
DELETE FROM leader_reports
DELETE FROM extras
DELETE FROM project_needs
DELETE FROM leaves
DELETE FROM day_reports
DELETE FROM day_plan_tasks
DELETE FROM day_plans
DELETE FROM work_tasks
DELETE FROM assignments
DELETE FROM plan_items
DELETE FROM projects
DELETE FROM notify_settings
DELETE FROM people WHERE Id > 5
SET FOREIGN_KEY_CHECKS = 1
-- sau khi xóa
SELECT COUNT(*) FROM people
SELECT COUNT(*) FROM work_tasks
