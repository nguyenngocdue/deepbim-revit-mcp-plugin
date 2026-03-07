# Kết nối Claude ↔ DeepBim-MCP (Revit)

## Sơ đồ kết nối

```
Claude Desktop  →  MCP Server (Node.js)  →  TCP localhost:8080  →  Revit Plugin (C#)
     │                    │                        │                      │
  Gọi tool          Kết nối TCP              Revit phải              SocketService
  say_hello         tới Revit                 LISTENING               phải Start
```

**Thứ tự bắt buộc:** Revit phải Start trước → Claude mới gọi tool được.

---

## Bước 1: Khởi động Revit Plugin (BẮT BUỘC)

1. Mở **Revit 2025**
2. Mở một project (hoặc New)
3. Vào tab **Add-Ins** trên ribbon
4. Tìm panel **DeepBim-MCP**
5. Click nút **MCP Switch** → cửa sổ "DeepBim-MCP Server Control" mở ra
6. Click nút **Start**
7. Kiểm tra: Status = **Running**, Port = **8080** (hoặc 8081, 8082...)

Nếu không thấy nút Start hoặc báo lỗi → add-in chưa load đúng. Chạy `setup-revit-addin.bat` rồi restart Revit.

---

## Bước 2: Cấu hình Claude Desktop

1. Mở file: `%APPDATA%\Claude\claude_desktop_config.json`
   - Hoặc: Claude → Settings → Developer → Edit Config

2. Thêm (sửa đường dẫn cho đúng):

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

3. Đóng **hoàn toàn** Claude Desktop (File → Exit hoặc thoát process) rồi mở lại.

---

## Bước 3: Kiểm tra kết nối

1. **Revit:** Cửa sổ MCP Status phải hiển thị "Running" trên port 8080
2. **Claude:** Có biểu tượng 🔨 bên ô chat = MCP đã load
3. **Test:** Trong Claude gõ: *"Gọi tool say_hello với name Test"*

---

## Checklist nhanh

| # | Việc cần làm | Cách kiểm tra |
|---|--------------|---------------|
| 1 | Revit đang mở | Có cửa sổ Revit |
| 2 | Add-in DeepBim-MCP đã load | Thấy tab Add-Ins → DeepBim-MCP |
| 3 | Đã click MCP Switch | Cửa sổ Control mở |
| 4 | Đã click Start | Status = Running, Port = 8080 |
| 5 | Claude config đúng | File JSON có deepbim-mcp-server |
| 6 | Claude đã restart | Có icon 🔨 |
| 7 | MCP Server đã build | Có file `server\build\index.js` |

---

## Lỗi thường gặp

### "No DeepBim-MCP server found on ports 8080-8099"
→ **Revit chưa Start.** Làm Bước 1, click Start trong MCP Status Window.

### "No matching tools found"
→ **Claude chưa load MCP.** Kiểm tra config, restart Claude, xem có icon 🔨.

### Tool gọi nhưng không phản hồi
→ **Revit đã tắt hoặc Stop.** Mở Revit, click Start lại.

### netstat thấy LISTENING nhưng Claude vẫn lỗi
→ Kiểm tra đường dẫn `args` trong config có đúng `server\build\index.js` không.
