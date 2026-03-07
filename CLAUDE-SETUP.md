# Cấu hình Claude để dùng DeepBim-MCP

## Bước 1: Build MCP Server

```bash
cd server
npm install
npm run build
```

## Bước 2: Cấu hình Claude Desktop

1. Mở **Claude Desktop** → **Settings** (⚙️) → **Developer** → **Edit Config**
2. Hoặc mở file trực tiếp: `%APPDATA%\Claude\claude_desktop_config.json`

3. Thêm cấu hình MCP server (sửa đường dẫn cho đúng với máy bạn):

```json
{
  "mcpServers": {
    "deepbim-mcp-server": {
      "command": "node",
      "args": [
        "E:\\C# Tool Revit\\revit-mcp\\mcp-addin\\revit-mcp-plugin\\server\\build\\index.js"
      ]
    }
  }
}
```

**Lưu ý:** Dùng đường dẫn tuyệt đối, dấu `\` phải escape thành `\\` trong JSON.

4. **Đóng hoàn toàn** Claude Desktop và mở lại (không chỉ đóng cửa sổ chat).

## Bước 3: Khởi động Revit trước

**Quan trọng:** Trước khi gọi tool từ Claude:

1. Mở **Revit 2025**
2. Mở một project bất kỳ
3. Vào tab **Add-Ins** → chọn **DeepBim-MCP** → nhấn **Start** (nếu có MCP Status Window)
4. Đảm bảo plugin đang chạy và lắng nghe trên port 8080–8099

## Bước 4: Kiểm tra

1. Mở Claude Desktop
2. Kiểm tra biểu tượng 🔨 (hammer) bên cạnh ô nhập chat – nếu có nghĩa MCP đã load
3. Gõ: *"Gọi tool say_hello với name là Test"* hoặc *"Use say_hello tool"*

## Xử lý lỗi

### "No matching tools found"
- Claude chưa load MCP server → kiểm tra lại `claude_desktop_config.json` và restart Claude
- Đường dẫn `args` sai → dùng đường dẫn tuyệt đối tới `server\build\index.js`

### "No DeepBim-MCP server found on ports 8080-8099"
- Revit chưa mở hoặc plugin chưa Start
- Mở Revit, load add-in DeepBim-MCP và nhấn Start

### Tool không phản hồi
- Kiểm tra Revit có đang mở và plugin đã Start
- Xem log tại `%APPDATA%\DeepBim-MCP\Logs\` (nếu có)
