# Môi trường Dev / Deploy (deepbim-mcp.env.json)

Plugin đọc file **deepbim-mcp.env.json** (cùng thư mục với RevitMCPPlugin.dll) để chọn thư mục gốc (Commands, registry).

---

## Mode được xác định thế nào?

| Bạn làm gì | Mode | Ai tạo/sửa deepbim-mcp.env.json? |
|------------|------|-----------------------------------|
| **Build Solution với Configuration = Debug** | **dev** | Project tự copy `env.dev.json` → output thành `deepbim-mcp.env.json` (trong `AddIn 2025 Debug\revit_mcp_plugin\`). Bạn không cần set gì. |
| **Chạy setup-revit-addin.ps1** (deploy) | **deploy** | Script ghi `deepbim-mcp.env.json` (mode=deploy) vào `%APPDATA%\...\revit_mcp_plugin\`. |

**Khi đang code (dev):** Chỉ cần để **Debug** trên toolbar Visual Studio rồi Build Solution. Mode = dev đã được set tự động qua file copy trong project.

---

## Hành vi theo mode

| mode | Thư mục plugin dùng |
|------|----------------------|
| **dev** | Thư mục chứa DLL (vd: `...\AddIn 2025 Debug\revit_mcp_plugin`) |
| **deploy** | `%APPDATA%\Autodesk\Revit\Addins\2025\revit_mcp_plugin` |

## File trong repo

- **env.dev.json** – Nội dung cho dev. Chỉ khi build **Debug** thì mới được copy ra thành `deepbim-mcp.env.json` trong output.
- **env.deploy.json** – Mẫu cho deploy; script ghi nội dung tương tự khi chạy setup.

## Dev: Tìm Command set khi chạy từ bin

Khi bạn load add-in từ **Add-in Manager** (trỏ tới `bin\AddIn 2025 Debug`), Revit đôi khi không trả đường dẫn DLL đúng → Settings báo "No command sets". Để tránh:

- Mỗi lần build **Debug**, project ghi đường dẫn thư mục **Commands** (bin) vào `%APPDATA%\DeepBim-MCP\dev-commands-path.txt`.
- Khi mở Settings, plugin thử lần lượt: assembly path → **path trong file trên** → Revit Addins → mặc định. Nhờ đó dev (chạy từ bin) vẫn tìm được command set.
- Deploy không dùng file này (chạy từ AppData, đường dẫn ổn định).
