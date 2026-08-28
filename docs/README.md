# docs/

Tài liệu thiết kế (design docs) cho `revit-mcp-plugin` + `revit-mcp-server`.

| Thư mục | Nội dung |
|---|---|
| [revit-command-driver/](revit-command-driver/) | **Revit Command Driver (RCD)** — cho AI điều khiển Revit qua `PostCommand` + thao tác chuột/phím như một modeler, phủ toàn bộ lệnh built-in mà không phải dev lại từng tool. |

## Revit Command Driver

| File | Mục đích |
|---|---|
| [00-brainstorm.md](revit-command-driver/00-brainstorm.md) | Ý tưởng, ràng buộc của `PostCommand`, so sánh phương án, nguyên tắc Hybrid, rủi ro |
| [01-spec.md](revit-command-driver/01-spec.md) | Kiến trúc, threading model, thành phần C#, contract JSON-RPC `rcd_*`, MCP tools, recipe format, mã lỗi |
| [02-plan.md](revit-command-driver/02-plan.md) | Phase 0–4, spikes S1–S9, task list, acceptance, test plan, DoD |
| [03-playbook.md](revit-command-driver/03-playbook.md) | Hướng dẫn cho AI agent: vòng lặp chuẩn, đọc status bar, recipes mẫu, xử lý sự cố |
