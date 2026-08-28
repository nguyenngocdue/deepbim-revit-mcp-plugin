# Revit Command Driver (RCD) — Specification

Trạng thái: **Draft v0.1** · Áp dụng cho: `mcp-addin/revit-mcp-plugin` (C#) + `revit-mcp-server` (TypeScript)
Đọc trước: [00-brainstorm.md](00-brainstorm.md)

---

## 1. Mục tiêu

1. Expose **toàn bộ lệnh Revit có thể post** (`PostableCommand` + lệnh add-in theo id) cho AI dưới dạng
   catalog tìm kiếm được.
2. Cho AI **bật lệnh** (`PostCommand`) với ngữ cảnh đã chuẩn bị (selection, view, zoom, default type).
3. Cho AI **thao tác như user** sau khi lệnh bật: click theo toạ độ model (mm), gõ số/phím, chờ prompt.
4. Cho AI **quan sát**: status bar, dialog đang mở, phần tử vừa được tạo/sửa/xoá.
5. An toàn: một driver tại một thời điểm, timeout, abort, dialog policy, không làm hỏng luồng tool cũ.

### Non-goals (v1)
- 3D orbit/pan bằng chuột; Family Editor; kéo grip; Options Bar (thay bằng fix-up API).
- Thay thế các tool API hiện có — RCD **bổ sung**, AI được chọn cách nào phù hợp.

---

## 2. Kiến trúc

```
AI client ── MCP ──▶ revit-mcp-server (Node)
                      │  tools: revit_cmd_search / revit_cmd_post / revit_ui_input /
                      │         revit_ui_state / revit_ui_cancel / revit_dialog_policy / revit_recipe_run
                      │  resources: revit://rcd/catalog, revit://rcd/recipes/*, revit://rcd/playbook
                      ▼  TCP JSON-RPC (localhost:8080)  method = "rcd_*"
            RevitMCPPlugin (SocketService, background thread)
                      │
                      ├─ ExternalEvent path (Revit main thread, API context)
                      │     rcd_list_commands  → CommandCatalog
                      │     rcd_post_command   → PrepareContext + ScreenMapper.Capture + PostCommand
                      │     rcd_ui_state(includeLiveMapping) → live mapping/selection (chỉ khi Revit idle)
                      │
                      └─ Direct path (background thread, KHÔNG API context, Win32)
                            rcd_ui_input      → InputDriver (SendInput | PostMessage)
                            rcd_ui_state      → StatusBarReader + WindowProbe + ChangeTracker.Since()
                            rcd_ui_cancel     → Esc×N + close stray dialogs + verify idle
                            rcd_dialog_policy → DialogPolicy (in-memory rules)

            Event subscriptions (đăng ký 1 lần khi bật server, callback chạy trên main thread):
                 Application.DocumentChanged    → ChangeTracker (ring buffer)
                 UIApplication.DialogBoxShowing → DialogPolicy.Apply()
```

### 2.1 Threading model

| Thành phần | Thread | Cần API context? |
|---|---|---|
| `CommandCatalog` build | main (ExternalEvent), lazy 1 lần | Có (`LookupPostableCommandId`, `CanPostCommand`) |
| `rcd_post_command` | main (ExternalEvent) | Có |
| `InputDriver` | socket thread | **Không** — chỉ Win32 |
| `StatusBarReader`, `WindowProbe` | socket thread | Không |
| `ChangeTracker` ghi | main (event callback) | — |
| `ChangeTracker` đọc | socket thread | Không (lock) |
| `DialogPolicy` | main (event callback) | — |

Lý do: sau khi post lệnh tương tác, Revit có thể không xử lý ExternalEvent nữa (spike S1). Mọi tool
"tay & mắt" phải sống được mà không có API context.

### 2.2 Vòng đời một lệnh (sequence)

```
AI                      Node                     Plugin (socket thr)         Revit main thread
│ revit_cmd_post ─────▶ rcd_post_command ──────▶ Raise ExternalEvent ──────▶ PostCommandHandler.Execute
│                                                                            • prepare (view/zoom/type/selection)
│                                                                            • snapshot = ScreenMapper.Capture(uiview)
│                                                                            • marker = ChangeTracker.Mark()
│                                                                            • uiapp.PostCommand(id)
│ ◀── {posted:true, mapping, marker, statusBefore} ◀────────────────────────  return → Revit chạy lệnh
│
│ revit_ui_input ─────▶ rcd_ui_input ──────────▶ InputDriver (dùng mapping đã cache)
│    [waitStatus, click, click, key Esc×2]        đọc status bar sau mỗi step
│ ◀── {steps:[{ok, statusAfter}], statusFinal, changes}
│
│ revit_ui_state ─────▶ rcd_ui_state ──────────▶ ChangeTracker.Since(marker) + status + dialogs
│ ◀── {idle:true, changes:{added:[..]}}
│
│ (tuỳ chọn) modify_element(added ids) — tool API cũ để fix-up tham số
```

---

## 3. Thành phần C#

Thư mục mới: `commandset/Driver/` (cùng assembly `RevitMCPCommandSet`, để dùng chung `ElementIdExtensions`,
`AIResult<T>`). Phần hook sự kiện đặt ở plugin để đăng ký lúc `MCPServiceConnection` bật server.

```
commandset/
├── Driver/
│   ├── CommandCatalog.cs          ← build & cache danh sách lệnh; search; Resolve(alias) → RevitCommandId
│   ├── KeyboardShortcutsReader.cs ← parse KeyboardShortcuts.xml → {commandId, shortcuts[], ribbonPath}
│   ├── ScreenMapper.cs            ← UIView → ViewMapping (affine model↔pixel), Capture(), ToScreen(), ToModel()
│   ├── InputDriver.cs             ← thực thi InputStep[]; 2 backend: SendInputBackend, PostMessageBackend
│   ├── Backends/SendInputBackend.cs
│   ├── Backends/PostMessageBackend.cs
│   ├── Native/Win32.cs            ← P/Invoke: SendInput, SetForegroundWindow, GetWindowRect, SendMessage, EnumChildWindows, GetDpiForWindow
│   ├── StatusBarReader.cs         ← tìm msctls_statusbar32 dưới MainWindowHandle, đọc text
│   ├── WindowProbe.cs             ← liệt kê dialog owned by Revit (title, buttons), foreground check, ClickButton
│   ├── ChangeTracker.cs           ← ring buffer DocumentChanged; Mark(); Since(seq)
│   ├── DialogPolicy.cs            ← rules {dialogId|title|message pattern → resultCode}; Apply(args)
│   ├── DriverLock.cs              ← 1 session tại 1 thời điểm; ownerToken; TTL; abort flag
│   ├── DriverOverlay.xaml(.cs)    ← (Phase 4) WPF topmost banner "AI đang điều khiển… Esc×3 để dừng"
│   ├── Data/
│   │   ├── rcd-interaction-hints.json ← name → instant|selection|points|dialog (≈150 lệnh phổ biến)
│   │   ├── rcd-status-patterns.json   ← idle patterns / prompt patterns theo locale
│   │   └── rcd-dialog-defaults.json   ← whitelist dialog auto-OK an toàn
│   └── Models/
│       ├── CommandInfo.cs         ← name, id, kind(postable|addin), shortcuts[], ribbonPath, canPost, tags[], interaction
│       ├── ViewMapping.cs         ← viewId, viewType, hwnd?, screenRect(px), modelCorners(mm), mmPerPixel, dpiScale
│       ├── InputStep.cs           ← discriminated by `type`: click|dblclick|move|drag|type|key|wait|waitStatus|waitChanges
│       ├── InputStepResult.cs
│       ├── ChangeSet.cs           ← fromSeq, toSeq, transactionNames[], added[], modified[], deleted[]
│       └── UiState.cs
├── Commands/Driver/
│   ├── RcdListCommandsCommand.cs      ← "rcd_list_commands"  (ExternalEventCommandBase)
│   ├── RcdPostCommandCommand.cs       ← "rcd_post_command"   (ExternalEventCommandBase)
│   ├── RcdUiInputCommand.cs           ← "rcd_ui_input"       (IRevitCommand trực tiếp, KHÔNG ExternalEvent)
│   ├── RcdUiStateCommand.cs           ← "rcd_ui_state"       (trực tiếp; tuỳ chọn thử ExternalEvent timeout 300ms cho live mapping)
│   ├── RcdUiCancelCommand.cs          ← "rcd_ui_cancel"      (trực tiếp)
│   └── RcdDialogPolicyCommand.cs      ← "rcd_dialog_policy"  (trực tiếp)
└── Services/Driver/
    ├── RcdListCommandsEventHandler.cs
    └── RcdPostCommandEventHandler.cs

plugin/Core/
└── DriverEventHook.cs   ← Subscribe/Unsubscribe DocumentChanged + DialogBoxShowing khi bật/tắt server
```

> Lưu ý: `CommandManager` hiện load command bằng reflection từ DLL với ctor `(UIApplication)`. Các command
> "direct path" vẫn implement `IRevitCommand` (`CommandName`, `Execute(JObject, string)`) nhưng không kế thừa
> `ExternalEventCommandBase`; chúng giữ `UIApplication` chỉ để lấy `MainWindowHandle` (đọc 1 lần lúc khởi tạo
> — property này an toàn để đọc ngoài API context vì là giá trị cache).

### 3.1 `CommandCatalog`

- Nguồn 1: `Enum.GetValues(typeof(PostableCommand))` → `RevitCommandId.LookupPostableCommandId(pc)`
  → `CommandInfo{ name = pc.ToString(), id = cmdId.Name /* "ID_OBJECTS_WALL" */, kind = "postable" }`.
- Nguồn 2: `KeyboardShortcutsReader` — đọc file theo thứ tự ưu tiên:
  `%APPDATA%\Autodesk\Revit\Autodesk Revit {YYYY}\KeyboardShortcuts.xml` → nếu không có, file mặc định trong
  thư mục cài Revit (đường dẫn xác định ở spike). Join theo `CommandId` để gắn `shortcuts` (VD `["WA"]`) và
  `ribbonPath` (VD `Architecture>Build>Wall:Wall: Architectural`). Đây là nguồn mô tả "người đọc được" tốt nhất.
- Nguồn 3 (mở rộng): lệnh add-in ngoài, người dùng khai báo trong `%APPDATA%\…\rcd-commands.user.json`
  (`{ "alias": "pyRevit.Tools.Foo", "commandId": "CustomCtrl_%CustomCtrl_%…" }`).
- `tags[]` sinh tự động từ ribbonPath (tab/panel) để lọc: `architecture`, `annotate`, `modify`, `view`…
- `interaction` lấy từ `rcd-interaction-hints.json`; không có → `unknown`.
- `canPost` **không cache** — tính lúc trả kết quả (`uiapp.CanPostCommand`).
- Cache catalog theo `(RevitVersion, mtime KeyboardShortcuts.xml)` vào `%APPDATA%\…\rcd-catalog-{YYYY}.json`
  để lần sau khởi động nhanh.
- `Resolve(string query)` chấp nhận: enum name (`ArchitecturalWall`), command id (`ID_OBJECTS_WALL`),
  shortcut (`WA` — nếu unique), alias user. Không unique → lỗi `AMBIGUOUS_COMMAND` kèm candidates.

### 3.2 `ScreenMapper`

```csharp
ViewMapping Capture(UIDocument uidoc, View view)
{
    var uiview  = uidoc.GetOpenUIViews().First(v => v.ViewId == view.Id);
    var rect    = uiview.GetWindowRectangle();   // px màn hình (spike S2: logical vs physical)
    var corners = uiview.GetZoomCorners();       // 2 XYZ model (feet): góc dưới-trái & trên-phải vùng nhìn
    // Chiếu về hệ 2D của view bằng view.RightDirection / view.UpDirection / view.Origin → (u, v)
    // px = rect.Left + (u - uMin) / (uMax - uMin) * rect.Width
    // py = rect.Bottom - (v - vMin) / (vMax - vMin) * rect.Height
}
```

- Chỉ cho `View.ViewType ∈ {FloorPlan, CeilingPlan, EngineeringPlan, AreaPlan, Section, Elevation,
  Detail, DraftingView, Legend}`. `ThreeD`/Sheet/Schedule → `VIEW_NOT_2D`.
- Lưu `mmPerPixel`; `rcd_post_command` có option `prepare.fitPoints` → gọi
  `uiview.ZoomAndCenterRectangle()` với padding trước khi capture để đảm bảo `mmPerPixel ≤ maxMmPerPixel`
  (mặc định 5 mm/px). Không đạt → vẫn post nhưng trả `warnings[]`.
- `ToScreen(JZPoint mm)` → `(px, py)`; ném `POINT_OFF_SCREEN` nếu ra ngoài rect (margin 4px).
- Mapping cache trong `DriverLock` session với `capturedAtSeq`; `rcd_ui_state` báo `mappingStale=true` nếu
  phát hiện view đổi (title cửa sổ chính đổi phần "Floor Plan: …") để AI post lại.

### 3.3 `InputDriver`

Backend chọn qua Settings (`driver.inputBackend = "sendinput" | "postmessage"`, mặc định `sendinput`).

`SendInputBackend`:
- Đảm bảo foreground: `GetForegroundWindow() == MainWindowHandle`? nếu không và `driver.allowForegroundSteal`
  → `SetForegroundWindow` (+ fallback `AttachThreadInput` / `SwitchToThisWindow`), chờ ≤500ms, nếu vẫn không
  → `FOREGROUND_FAILED`. Nếu giữa batch foreground đổi → dừng `FOREGROUND_LOST`.
- Chuột: `MOUSEEVENTF_ABSOLUTE|MOVE` (toạ độ chuẩn hoá 0..65535 theo virtual screen) → `LEFTDOWN/UP`.
  Delay giữa move và click (mặc định 40ms) để Revit cập nhật snap/hover.
- Phím: `SendInput` với `KEYEVENTF_UNICODE` cho text; scan code cho `Enter/Escape/Tab/Shift/Ctrl/Space/Delete/F-keys`.
  Modifier giữ trong suốt step nếu `holdShift: true`.

`PostMessageBackend` (nếu spike S4 pass): `WM_MOUSEMOVE/WM_LBUTTONDOWN/WM_LBUTTONUP` tới HWND view
(tìm bằng `WindowFromPoint` một lần lúc capture, hoặc `EnumChildWindows` theo class). `WM_CHAR/WM_KEYDOWN` cho phím.

Các `InputStep`:

| type | fields | Ghi chú |
|---|---|---|
| `click` | `point:[x,y,z] (mm)` **hoặc** `screen:[px,py]`, `button: left\|right\|middle`, `holdShift?`, `snapOverride?: "SO"\|"SE"\|"SM"\|"SI"\|"SC"\|"SP"` | `snapOverride` → gõ 2 phím ngay trước khi click |
| `dblclick` | như click | |
| `move` | `point` / `screen`, `holdShift?` | Định hướng trước khi gõ số |
| `drag` | `from`, `to`, `holdShift?` | Window-select / kéo |
| `type` | `text`, `enter?: true` | Unicode; `enter` gửi ↵ sau text |
| `key` | `key: "Escape"\|"Enter"\|"Tab"\|"Space"\|"Delete"\|…`, `times?`, `modifiers?: ["Shift","Ctrl","Alt"]` | |
| `wait` | `ms` | ≤ 5000 |
| `waitStatus` | `contains` / `regex`, `timeoutMs` (≤15000) | Poll status bar mỗi 50ms |
| `waitChanges` | `minAdded?`, `timeoutMs` | Chờ ChangeTracker có phần tử mới kể từ marker |

Sau **mỗi** step: đọc status bar → `statusAfter`. Nếu step có `expectStatus` và không khớp, và
`stopOnStatusMismatch=true` → dừng batch, trả `STATUS_MISMATCH` kèm index. Nếu phát hiện dialog mới
(WindowProbe) và `stopOnDialog=true` (mặc định) → dừng, trả `DIALOG_OPEN` kèm `{title, text, buttons[]}`.

### 3.4 `StatusBarReader`

- `hwndMain = uiapp.MainWindowHandle` (Revit 2019+; cache lúc khởi tạo).
- `EnumChildWindows(hwndMain)` tìm class `msctls_statusbar32`; đọc part 0: `SendMessage(SB_GETTEXTLENGTH)`,
  `SendMessage(SB_GETTEXT, part, buffer)`. Cache HWND; re-scan nếu `IsWindow` false.
- Trả `{ text, isIdle }`. `isIdle` = text rỗng hoặc khớp pattern idle trong `rcd-status-patterns.json`
  (`"Click to select…"`, `"Ready"`), có thể thêm locale khác.

### 3.5 `ChangeTracker`

```csharp
record ChangeEntry(long Seq, DateTime Utc, string DocTitle, string[] TransactionNames,
                   long[] Added, long[] Modified, long[] Deleted, string Operation);
long Mark();                                   // trả Seq hiện tại (dùng làm marker)
ChangeSet Since(long seq, int maxIds = 500);
```
- Đăng ký `app.DocumentChanged` trong `DriverEventHook.Subscribe(uiapp)` khi bật server; unsubscribe khi tắt.
- Ring buffer 2000 entries; `lock` đơn giản. `Operation` = `e.Operation.ToString()` (TransactionCommitted / Undone / Redone).
- `ChangeSet` gộp tất cả entries `> seq`; `addedByCategory` chỉ điền khi có API context (best-effort qua
  `rcd_ui_state(includeLiveMapping=true)`), nếu không trả ids thôi.

### 3.6 `DialogPolicy`

- Rule: `{ match: { dialogId?: "TaskDialog_…", titleRegex?: "…", messageRegex?: "…" }, action: { overrideResult: 1 | "IDOK" | "IDCANCEL" | "IDYES" | "IDNO" | { commandLink: n } }, once?: bool }`.
- Mặc định (**an toàn**): không tự trả lời gì ngoài các dialog "thuần cảnh báo" trong whitelist
  `rcd-dialog-defaults.json`. Dialog liên quan xoá / ghi đè / sync / detach / audit **không bao giờ** auto.
- Rule do AI đặt qua `rcd_dialog_policy` sống theo session driver, tự xoá khi `rcd_ui_cancel` hoặc hết `ttlMs`.
- Mỗi lần apply ghi vào `dialogEvents[]` để `rcd_ui_state` trả về cho AI biết chuyện gì đã xảy ra.

### 3.7 `DriverLock`

- `Acquire(token, ttlMs)` khi `rcd_post_command` với `expect != "instant"` hoặc `rcd_ui_input`; release khi
  `rcd_ui_cancel`, hết TTL (mặc định 120s), hoặc khi status idle + không có change liên tục 10s.
- Tool API cũ (`apply_operations`, `modify_element`…) **không bị chặn** — chúng cần API context nên nếu Revit
  đang bận lệnh sẽ tự timeout. Chỉ các `rcd_*` bị `DRIVER_BUSY` khi token khác.
- Abort: người dùng nhấn `Esc` 3 lần trong 1s (hook `SetWindowsHookEx(WH_KEYBOARD_LL)`, Phase 4) → set
  abort flag → InputDriver dừng batch với `USER_ABORTED`.

---

## 4. JSON-RPC (Revit side) — contracts

Tất cả trả `AIResult<T>`: `{ success, message, response }`. Lỗi có thêm `response.errorCode` (xem §8).

### 4.1 `rcd_list_commands`
```jsonc
// request
{ "query": "wall", "tags": ["architecture"], "onlyPostable": true, "limit": 50 }
// response
{ "revitVersion": "2025", "total": 12, "items": [
  { "name": "ArchitecturalWall", "id": "ID_OBJECTS_WALL", "kind": "postable",
    "shortcuts": ["WA"], "ribbonPath": "Architecture>Build>Wall:Wall: Architectural",
    "tags": ["architecture","build"], "canPost": true,
    "interaction": "points" } ] }   // instant | selection | points | dialog | unknown
```

### 4.2 `rcd_post_command`
```jsonc
// request
{
  "command": "ArchitecturalWall",            // name | id | shortcut | alias
  "expect": "points",                        // instant | selection | points | dialog | unknown
  "prepare": {
    "selectElementIds": [123, 456],          // Selection.SetElementIds trước khi post
    "clearSelection": false,
    "activeViewId": 789,                     // đổi view trước khi post (optional)
    "fitPoints": [[0,0,0],[8000,5000,0]],    // ZoomAndCenterRectangle với padding
    "fitPaddingMm": 1500,
    "maxMmPerPixel": 5,
    "defaultType": { "group": "WallType", "typeId": 321 }        // SetDefaultElementTypeId
    // hoặc     { "categoryId": -2000023, "typeId": 654 }         // SetDefaultFamilyTypeId (Door)
  },
  "lockToken": "sess-01", "lockTtlMs": 120000
}
// response
{
  "posted": true, "command": { "name": "ArchitecturalWall", "id": "ID_OBJECTS_WALL" },
  "marker": 1042,
  "statusBefore": "Click to select, TAB for alternates, CTRL adds, SHIFT unselects.",
  "mapping": { "viewId": 789, "viewName": "Level 1", "viewType": "FloorPlan",
               "screenRect": [120, 180, 1720, 980], "modelCorners": [[-2000,-1500,0],[10000,6500,0]],
               "mmPerPixel": 7.5, "dpiScale": 1.25 },
  "warnings": ["mmPerPixel 7.5 > maxMmPerPixel 5: tighten fitPoints or accept lower precision"]
}
```
Lỗi: `COMMAND_NOT_FOUND`, `AMBIGUOUS_COMMAND{candidates}`, `CANNOT_POST{activeViewType}`, `POST_PENDING`
(InvalidOperationException — lệnh trước chưa chạy), `VIEW_NOT_2D`, `DRIVER_BUSY{ownerToken}`,
`EXTERNAL_EVENT_TIMEOUT` (Revit bận/modal — gợi ý gọi `rcd_ui_cancel`).

### 4.3 `rcd_ui_input`
```jsonc
// request
{ "lockToken": "sess-01",
  "steps": [
    { "type": "waitStatus", "contains": "start point", "timeoutMs": 5000 },
    { "type": "click", "point": [0,0,0] },
    { "type": "move",  "point": [6000,0,0], "holdShift": true },
    { "type": "type",  "text": "6000", "enter": true },
    { "type": "key",   "key": "Escape", "times": 2 }
  ],
  "stopOnDialog": true, "stopOnStatusMismatch": false, "interStepDelayMs": 60, "dryRun": false }
// response
{ "completed": 5, "steps": [
    { "index": 0, "ok": true, "statusAfter": "Click to enter wall start point.", "elapsedMs": 210 },
    { "index": 1, "ok": true, "screen": [640, 512], "statusAfter": "Click to enter wall end point." },
    { "index": 2, "ok": true, "screen": [1440, 512] },
    { "index": 3, "ok": true, "statusAfter": "Click to enter wall start point." },
    { "index": 4, "ok": true, "statusAfter": "Click to select, TAB for alternates…" } ],
  "statusFinal": "Click to select, TAB for alternates…", "idle": true,
  "dialog": null, "changes": { "added": [901], "modified": [], "deleted": [] } }
```
`dryRun: true` → chỉ tính `screen` cho từng step, không gửi input (AI kiểm tra mapping).

### 4.4 `rcd_ui_state`
```jsonc
// request
{ "sinceMarker": 1042, "includeLiveMapping": false, "maxIds": 200 }
// response
{ "status": { "text": "…", "isIdle": true },
  "foreground": { "isRevit": true, "title": "Autodesk Revit 2025 - Project1 - Floor Plan: Level 1" },
  "dialog": { "open": false },   // hoặc { "open": true, "hwnd": 1234, "title": "…", "text": "…", "buttons": ["OK","Cancel"] }
  "changes": { "fromSeq": 1042, "toSeq": 1044, "transactionNames": ["Wall"], "added": [901], "modified": [], "deleted": [] },
  "dialogEvents": [ { "utc": "…", "dialogId": "…", "action": "overrideResult:1" } ],
  "driver": { "locked": true, "ownerToken": "sess-01", "mappingStale": false },
  "liveMapping": null }
```

### 4.5 `rcd_ui_cancel`
```jsonc
{ "lockToken": "sess-01", "escapes": 3, "closeDialogs": "cancel", "releaseLock": true }
// closeDialogs: "cancel" | "none"
→ { "statusFinal": "…", "idle": true, "closedDialogs": ["Autodesk Revit 2025"] }
```

### 4.6 `rcd_dialog_policy`
```jsonc
{ "action": "set",   // set | clear | list
  "rules": [ { "match": { "titleRegex": "^Autodesk Revit", "messageRegex": "not joined" },
               "action": { "overrideResult": "IDOK" }, "once": true } ],
  "ttlMs": 60000 }
```

---

## 5. MCP tools (Node — `revit-mcp-server/src/tools/`)

Một file / tool, export `register*`; handler gọi `withRevitConnection(c => c.sendCommand("rcd_*", args))`.
**Description là thứ AI đọc** — phải nói rõ *khi nào dùng* và *flow chuẩn*.

| Tool | → Revit method | Mô tả ngắn cho AI |
|---|---|---|
| `revit_cmd_search` | `rcd_list_commands` | Tìm lệnh Revit built-in theo từ khoá/shortcut/tag. Trả name/id/shortcut/ribbonPath/canPost/interaction. Dùng trước `revit_cmd_post`. |
| `revit_cmd_post` | `rcd_post_command` | Kích hoạt một lệnh Revit y như user gõ shortcut. Có thể chuẩn bị selection/view/zoom/default type. Lệnh `points` cần tiếp `revit_ui_input`; lệnh `selection` (Delete, Pin…) chạy ngay; lệnh `dialog` mở hộp thoại. Trả `mapping` + `marker`. |
| `revit_ui_input` | `rcd_ui_input` | Thao tác chuột/bàn phím vào Revit theo toạ độ model (mm) trong view vừa post. Luôn bắt đầu bằng `waitStatus` và kết thúc bằng `key Escape ×2`. Đọc `statusAfter` để biết Revit đang chờ gì. |
| `revit_ui_state` | `rcd_ui_state` | Nhìn Revit: status bar, dialog đang mở, phần tử thêm/sửa/xoá kể từ `marker`. Gọi sau mỗi lệnh để xác nhận kết quả. |
| `revit_ui_cancel` | `rcd_ui_cancel` | Thoát lệnh đang chạy (Esc), đóng dialog, nhả driver lock. Gọi khi bất kỳ bước nào lỗi. |
| `revit_dialog_policy` | `rcd_dialog_policy` | Đặt luật tự trả lời dialog Revit cho phiên hiện tại (VD: warning → OK). |
| `revit_recipe_run` | Node điều phối nhiều `rcd_*` | Chạy recipe có sẵn (`draw_wall_2pts`, `place_door_on_wall`, …) với tham số; recipe = chuỗi post + input + verify. |
| `revit_recipe_save` | Node ghi file | Lưu chuỗi bước vừa thành công thành recipe để tái sử dụng. |

MCP **resources**:
- `revit://rcd/catalog` — catalog JSON (cache từ lần `rcd_list_commands` gần nhất; refresh khi AI yêu cầu).
- `revit://rcd/playbook` — nội dung [03-playbook.md](03-playbook.md).
- `revit://rcd/recipes/{name}` — từng recipe.

MCP **prompt**: `revit_modeler` — hướng dẫn vòng lặp *Search → Post → Input → State → Fix-up → Cancel*.

Zod schema: dùng chung `pointMm = z.tuple([z.number(), z.number(), z.number()])`; `steps` là
`z.discriminatedUnion("type", [...])` để AI ít sai format. Đặt ở `src/rcd/schemas.ts`.

---

## 6. Recipe format (`revit-mcp-server/recipes/*.json`)

```jsonc
{
  "name": "draw_wall_2pts",
  "description": "Vẽ 1 wall thẳng giữa 2 điểm bằng lệnh Wall của Revit",
  "params": {
    "start":      { "type": "pointMm",   "required": true },
    "end":        { "type": "pointMm",   "required": true },
    "wallTypeId": { "type": "elementId", "required": false },
    "heightMm":   { "type": "number",    "required": false }
  },
  "steps": [
    { "tool": "revit_cmd_post",
      "args": { "command": "ArchitecturalWall", "expect": "points",
                "prepare": { "fitPoints": ["{{start}}", "{{end}}"],
                             "defaultType": { "group": "WallType", "typeId": "{{wallTypeId}}" } } },
      "save": { "marker": "$.marker" } },
    { "tool": "revit_ui_input",
      "args": { "steps": [
        { "type": "waitStatus", "contains": "start point" },
        { "type": "click", "point": "{{start}}" },
        { "type": "click", "point": "{{end}}" },
        { "type": "key", "key": "Escape", "times": 2 } ] } },
    { "tool": "revit_ui_state", "args": { "sinceMarker": "{{marker}}" },
      "assert": { "path": "$.changes.added.length", "gte": 1 },
      "save": { "wallId": "$.changes.added[0]" } },
    { "tool": "modify_element", "when": "{{heightMm}}",
      "args": { "elementId": "{{wallId}}", "parameters": { "Unconnected Height": "{{heightMm}}" } } }
  ],
  "onError": [ { "tool": "revit_ui_cancel", "args": {} } ]
}
```
- Placeholder `{{name}}` thay bằng param/saved value; `$.path` là JSONPath đơn giản trên response bước trước.
- `when` bỏ qua step nếu falsy. `onError` luôn chạy khi có step fail.
- Recipe engine nằm ở Node (`src/rcd/RecipeEngine.ts`), không đụng C#. Recipe do AI lưu → `recipes/user/`
  với `"source": "ai"`.

---

## 7. Cấu hình (plugin `Settings.cs` + trang Settings UI)

| Key | Default | Ý nghĩa |
|---|---|---|
| `driver.enabled` | `true` | Tắt → tất cả `rcd_*` trả `DRIVER_DISABLED` |
| `driver.inputBackend` | `sendinput` | `sendinput` \| `postmessage` |
| `driver.maxMmPerPixel` | `5` | Ngưỡng cảnh báo độ chính xác |
| `driver.interStepDelayMs` | `60` | Delay giữa các step |
| `driver.lockTtlMs` | `120000` | TTL driver lock |
| `driver.showOverlay` | `true` | Banner khi AI điều khiển (Phase 4) |
| `driver.allowForegroundSteal` | `true` | Cho phép `SetForegroundWindow` |
| `driver.statusLocale` | `en` | Bộ pattern status bar |

---

## 8. Mã lỗi

| errorCode | Khi nào | AI nên làm gì |
|---|---|---|
| `DRIVER_DISABLED` | Settings tắt | Báo user |
| `DRIVER_BUSY` | Token khác đang giữ lock | Chờ / `revit_ui_cancel` với token đó |
| `COMMAND_NOT_FOUND` / `AMBIGUOUS_COMMAND` | Resolve thất bại | `revit_cmd_search` lại |
| `CANNOT_POST` | `CanPostCommand=false` | Đổi view / selection rồi post lại |
| `POST_PENDING` | Lệnh post trước chưa chạy | `revit_ui_cancel` rồi thử lại |
| `EXTERNAL_EVENT_TIMEOUT` | Revit bận/modal | `revit_ui_state` xem dialog → `revit_ui_cancel` |
| `VIEW_NOT_2D` | Active view là 3D/Sheet/Schedule | Đổi `activeViewId` |
| `POINT_OFF_SCREEN` | Điểm ngoài vùng nhìn | Post lại với `fitPoints` |
| `FOREGROUND_FAILED` / `FOREGROUND_LOST` | Không giữ được Revit foreground | Báo user; thử `postmessage` backend |
| `STATUS_TIMEOUT` / `STATUS_MISMATCH` | Prompt không như mong đợi | Đọc `statusAfter`, điều chỉnh |
| `DIALOG_OPEN` | Dialog chặn | Đọc `dialog.buttons` → `revit_dialog_policy` hoặc cancel |
| `USER_ABORTED` | User nhấn Esc×3 | Dừng, báo user |

---

## 9. Tương thích phiên bản

- Revit 2024 (`net48`) ↔ 2025/2026 (`net8`) ↔ 2027 (`net10`): P/Invoke giống nhau; dùng `#if REVIT2024_OR_GREATER`
  cho `ElementId.Value`. `UIApplication.MainWindowHandle` có từ 2019 → OK.
- `PostableCommand` thêm/bớt giá trị theo version → catalog **luôn** build bằng reflection runtime, không hardcode.
- Text status bar theo ngôn ngữ Revit: pattern để trong JSON, hỗ trợ `en` trước.
- Revit 2027: xác nhận `UIView.GetZoomCorners()` / `GetWindowRectangle()` không đổi chữ ký (spike).

---

## 10. Bảo mật & an toàn

- RCD gửi input **chỉ** khi Revit đang foreground (SendInput) hoặc chỉ tới HWND Revit (PostMessage); không bao giờ
  gửi tới cửa sổ khác. Nếu foreground đổi giữa batch → dừng ngay `FOREGROUND_LOST`.
- Không auto-trả lời dialog xoá/ghi đè/sync/detach nếu AI không đặt rule tường minh; rule có TTL.
- Mỗi batch input ghi log đầy đủ (mapping, step, status) vào `Logs/rcd-*.log` để tái hiện.
- Recipe do AI lưu đặt ở `recipes/user/`, gắn `"source": "ai"`, người dùng review được.

---

## 11. Câu hỏi mở (Spike Phase 0 — chi tiết trong [02-plan.md](02-plan.md))

S1 ExternalEvent khi lệnh active · S2 DPI · S3 Foreground · S4 PostMessage · S5 Snap error · S6 UIA Options Bar ·
S7 PostCommand exception · S8 SetDefaultElementTypeId · S9 Multi-monitor.
Kết quả spike sẽ được ghi ngược lại vào mục này.
