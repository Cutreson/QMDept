// File migration do MySql.EntityFrameworkCore sinh ra gọi MySQLModelBuilderExtensions mà không kèm using
// → khai báo global để mọi migration (kể cả sinh sau này) biên dịch được mà không phải sửa tay.
global using MySql.EntityFrameworkCore.Extensions;
