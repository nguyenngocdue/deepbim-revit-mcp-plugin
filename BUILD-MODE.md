# build-mode.txt

File **build-mode.txt** (ở thư mục gốc repo) có **một dòng**: `Dev` hoặc `Deploy`.

| Giá trị   | Khi Build |
|-----------|-----------|
| **Dev**   | Chỉ copy vào `plugin\bin\AddIn 2025 Debug\...` (không đụng AppData). |
| **Deploy**| Copy vào bin **và** copy thêm vào `%APPDATA%\Autodesk\Revit\Addins\2025\`. |

**Cách dùng:** Mở `build-mode.txt`, sửa nội dung thành `Dev` hoặc `Deploy` (đúng một dòng), rồi Build Solution.
