# Revit Command Driver (RCD) — Brainstorm

> Mục tiêu: cho AI **điều khiển Revit như một modeler** bằng chính các lệnh built-in của Revit
> (Wall, Door, Dimension, Tag, Align, Trim…), thay vì phải dev lại từng tool bằng Revit API.

---

## 1. Ý tưởng gốc

Revit có sẵn ~900 lệnh. Mỗi lệnh có một `RevitCommandId` và có thể kích hoạt bằng
`UIApplication.PostCommand(RevitCommandId)` — tương đương user gõ shortcut `WA`, `L`, `DI`…

Hiện tại mỗi khả năng của AI = 1 tool tự viết (`create_line_based_element`, `create_dimensions`,
`tag_walls`…). Cách này:

- Tốn công: mỗi lệnh Revit → 1 Command + 1 EventHandler + 1 Model + 1 tool TS.
- Không bao giờ đuổi kịp Revit (900 lệnh, mỗi năm thêm).
- Phải tự tái hiện logic mà Revit đã làm rất tốt (auto-join wall, host door vào wall, snap,
  phase, workset, warning resolution…) → dễ bug.

**RCD đổi cách tiếp cận**: AI không gọi API tạo phần tử; AI *bật lệnh của Revit* rồi *thao tác
như người* (click điểm, gõ số, Enter, Esc) và *đọc lại kết quả*. Revit lo phần còn lại.

Lợi ích:

| | Tool viết tay bằng API | RCD |
|---|---|---|
| Độ phủ | Vài chục lệnh | Toàn bộ PostableCommand + lệnh add-in ngoài |
| Hành vi | Do dev tự viết | Giống hệt user (join, host, snap, warning) |
| Undo | Phải tự nhóm transaction | Undo/Redo tự nhiên theo lệnh Revit |
| Thêm khả năng mới | Code C# + TS + build | Thêm 1 "recipe" JSON hoặc chỉ cần mô tả trong playbook |
| Rủi ro | Sai logic API | Sai click / lệch snap / dialog bất ngờ |

---

## 2. Sự thật kỹ thuật về `PostCommand` (ràng buộc thiết kế)

1. **Chỉ xếp hàng, không chạy ngay.** Lệnh chạy khi API context kết thúc — với add-in là ngay sau khi
   `IExternalEventHandler.Execute()` return. Kết quả trả về cho MCP chỉ có nghĩa là "đã post".
2. **Mỗi API context chỉ post được 1 lệnh.** Post lệnh thứ 2 khi lệnh trước chưa chạy →
   `InvalidOperationException`. Cần serialize nghiêm ngặt.
3. **Không truyền được tham số.** `PostCommand` chỉ *khởi động* lệnh. Lệnh tương tác (Wall, Line,
   Door…) đứng chờ user click/gõ. Đây là khoảng trống RCD phải lấp.
4. **Không có API "lệnh nào đang chạy".** Phải suy ra từ status bar, cửa sổ đang mở, và
   `DocumentChanged`.
5. **Trong lúc lệnh tương tác đang active, nhiều khả năng ExternalEvent/Idling không được xử lý**
   (cần spike xác nhận — xem §6). Modal dialog thì chắc chắn block ExternalEvent.
6. `UIApplication.CanPostCommand(id)` cho biết lệnh có khả dụng ở ngữ cảnh hiện tại (VD: đang ở
   sheet thì không vẽ Wall).
7. Lệnh add-in bên thứ 3 cũng post được: `RevitCommandId.LookupCommandId("CustomCtrl_%CustomCtrl_%Tab%Panel%Button")`
   (id lấy từ journal file).

**Hệ quả kiến trúc quan trọng nhất:** phần "tay" (click/gõ phím) **không được đi qua ExternalEvent**.
Nó phải chạy từ background thread bằng Win32 (`SendInput` / `PostMessage`) — không cần API context.
Revit API chỉ được dùng ở các **điểm chụp (snapshot)** *trước* khi post và *sau* khi lệnh kết thúc.

---

## 3. Các phương án lấp khoảng trống "sau PostCommand"

| # | Phương án | Ưu | Nhược | Kết luận |
|---|---|---|---|---|
| A | Chỉ `PostCommand` + pre-select bằng API | Ổn định 100%, không chiếm chuột | Chỉ phủ lệnh không cần input thêm: Delete, Pin/Unpin, Hide/Isolate, Undo/Redo, Save/Sync, view commands, mở dialog | **MVP — làm trước** |
| B | Journal playback (`Jrn.Command`, `Jrn.MouseMove`) | Script đầy đủ | Chỉ replay khi khởi động Revit, không dùng được live | Loại |
| C | `SendInput` chuột/phím + map toạ độ model→pixel qua `UIView.GetZoomCorners()` | Phủ mọi lệnh vẽ 2D, giống user thật | Chiếm chuột, cần foreground, DPI, 3D khó | **Core của driver** |
| D | `PostMessage(WM_LBUTTONDOWN…)` thẳng vào HWND của view | Không chiếm cursor, không cần foreground | Revit có thể bỏ qua (hover/hit-test state) | Spike; fallback cho C |
| E | UI Automation (UIA) cho Options Bar / Type Selector / dialog | Set Height, Offset, Chain, chọn type như user | Fragile theo version, AutomationId | Phase 3, best-effort |
| F | **Hybrid: API chuẩn bị → UI thao tác → API sửa sau** | Chính xác: type/level/param set bằng API, gesture chỉ cho phần bắt buộc | Cần 2 lớp phối hợp | **Nguyên tắc xuyên suốt** |
| G | Vision loop (screenshot view → AI nhìn → sửa) | AI tự sửa khi lệch | Chậm, tốn token | Phase 3 optional |

---

## 4. Nguyên tắc Hybrid: Prepare → Post → Gesture → Verify → Fix-up

```
 API  (ExternalEvent #1)              UI  (background thread, Win32)         API  (ExternalEvent #2)
 ────────────────────────             ───────────────────────────────         ────────────────────────
 • uidoc.ActiveView = planView        • wait status "Click to enter          • đọc ChangeTracker buffer
 • ZoomAndCenterRectangle(bbox)         wall start point"                     → elementIds mới tạo
 • SetDefaultElementTypeId(WallType)  • click(start)  click(end)             • set Height / Base Level /
 • Selection.SetElementIds(pre)       • type "4000" ↵                          Comments… bằng API
 • tính mapping model→screen          • Esc Esc                              • validate; sai → PostCommand(Undo)
 • PostCommand(cmd)
```

Quy tắc: **thứ gì chuẩn bị được bằng API thì dùng API** (default type, selection, active view, zoom,
default level của view). **Chỉ gesture cho phần bắt buộc phải tương tác** (điểm, đường, pick host,
pick reference cho dimension). **Sau đó sửa tham số bằng API** thay vì đánh vật với Options Bar.

Ví dụ: vẽ wall type "Generic 200" cao 3000 từ A→B:
1. API: `doc.SetDefaultElementTypeId(ElementTypeGroup.WallType, id_Generic200)`; zoom về bbox(A,B).
2. Post `ArchitecturalWall`.
3. UI: click A, click B, Esc Esc.
4. API: đọc wall mới từ ChangeTracker → set `WALL_USER_HEIGHT_PARAM = 3000mm`.

---

## 5. Kỹ thuật cụ thể đã xác định

| Vấn đề | Kỹ thuật |
|---|---|
| Model → pixel | `UIView.GetWindowRectangle()` (rect màn hình của view) + `UIView.GetZoomCorners()` (2 góc model của vùng nhìn) → affine 2D. Áp dụng cho Plan / Ceiling / Section / Elevation / Drafting / Legend. 3D không hỗ trợ v1. |
| Nhập số chính xác | Sau click điểm đầu, di chuột theo hướng → gõ số → Enter (listening dimension). Giữ `Shift` để ortho. Gõ `SO` (Snap Override → Snaps Off) trước 1 click để bỏ snap. `SE/SM/SI/SC` ép snap endpoint/midpoint/intersection/center. |
| Biết lệnh đang chờ gì | Đọc **status bar** Revit: child window `msctls_statusbar32` của `UIApplication.MainWindowHandle` → `SendMessage(SB_GETTEXT)`. Add-in cùng process nên buffer hợp lệ. Prompt "Click to enter wall start point." là đèn tín hiệu từng bước. |
| Dialog bất ngờ | `UIApplication.DialogBoxShowing` → `OverrideResult()` theo policy (VD: TaskDialog warning → OK). Dialog không bắt được → `EnumWindows` owner = Revit, click button theo caption (Win32 `BM_CLICK` / UIA). |
| Biết lệnh tạo ra gì | Subscribe `Application.DocumentChanged` **một lần** khi bật server → ring buffer thread-safe `(seq, time, transactionNames, added/modified/deleted ids)`. Tool trả "changes since marker". Không cần API context để đọc buffer. |
| Catalog lệnh | `Enum.GetValues(typeof(PostableCommand))` → `RevitCommandId.LookupPostableCommandId()` → `Name`, `Id`. Join với `KeyboardShortcuts.xml` (`CommandId`, `Shortcuts`, `Paths` = ribbon path) để có alias `WA`, mô tả người đọc được. Cache theo Revit version; `canPost` tính realtime. |
| Chọn type trước khi vẽ | `Document.SetDefaultElementTypeId(ElementTypeGroup, id)` (system family: Wall/Floor/Roof/Ceiling/Text/Dimension…) và `Document.SetDefaultFamilyTypeId(categoryId, typeId)` (loadable: Door/Window/Component). Revit dùng default này cho Type Selector khi bật lệnh (cần spike S8 xác nhận trên mọi version). |
| Tránh user can thiệp | "Driver lock": trong lúc gesture, hiện overlay mờ "AI đang điều khiển — nhấn Esc 3 lần để dừng"; mọi tool `rcd_*` khác bị `DRIVER_BUSY`. |

---

## 6. Rủi ro & câu hỏi mở → Spike Phase 0

| # | Câu hỏi | Ảnh hưởng nếu sai | Cách kiểm |
|---|---|---|---|
| S1 | ExternalEvent có được xử lý khi lệnh Wall đang chờ click? | Quyết định có được đọc mapping/selection *giữa* lệnh không | Post Wall, sau 1s Raise ExternalEvent log timestamp |
| S2 | `GetWindowRectangle()` trả logical hay physical pixel khi DPI 125/150%? | Click lệch hàng trăm px | So với `GetWindowRect` Win32 + `GetDpiForWindow` |
| S3 | `SendInput` khi Revit không foreground: `SetForegroundWindow` từ trong process có OK? Cần `AttachThreadInput`? | Click rơi vào app khác | Test khi VS Code đang foreground |
| S4 | `PostMessage(WM_LBUTTONDOWN)` vào HWND view có được Revit nhận? | Nếu OK → không chiếm cursor, chạy được khi user làm việc khác | Gửi click vào view khi Wall active |
| S5 | Sai số click do snap kéo điểm | Wall lệch vài mm–cm | Đo lệch với/không `SO`, theo mức zoom |
| S6 | Options Bar có expose UIA (Height/Offset/Chain)? | Nếu không → luôn fix-up bằng API | Inspect.exe / FlaUI |
| S7 | `PostCommand` trong ExternalEvent ném lỗi/Denied khi nào? | Retry policy | Post khi có TaskDialog mở, khi đã post 1 lệnh chưa chạy |
| S8 | `SetDefaultElementTypeId` có đổi Type Selector khi bật lệnh không? (2024–2027) | Phải dùng UIA cho type hoặc fix-up | Set rồi post Wall, đọc type wall tạo ra |
| S9 | Đa monitor / view không maximize / view tab bị che | Mapping sai | Test 2 màn + tiled views |

---

## 7. Phạm vi

**v1 làm:** view 2D (Plan/Section/Elevation/Drafting), 1 monitor, Revit 2024–2027, built-in commands +
lệnh add-in theo id, status-bar driven, dialog auto-policy, ChangeTracker, recipe JSON.

**v1 không làm:** navigate 3D bằng chuột, Family Editor, sketch mode phức tạp có nhiều mode (Floor
boundary vẫn có thể làm bằng chuỗi click + Finish ✓ nhưng để Phase 3), kéo-thả grip, Options Bar
(dùng fix-up API thay thế).

---

## 8. Quyết định đặt tên

- Feature: **Revit Command Driver (RCD)**.
- C# namespace/folder: `RevitMCPCommandSet.Driver`.
- Command JSON-RPC (Revit side): `rcd_*`.
- MCP tools (Node side): `revit_cmd_*` (catalog/post) và `revit_ui_*` (gesture/state).
- Recipe: file `recipes/*.json`, được load bởi Node server và expose làm MCP resource.

Xem tiếp: [01-spec.md](01-spec.md) · [02-plan.md](02-plan.md) · [03-playbook.md](03-playbook.md)
