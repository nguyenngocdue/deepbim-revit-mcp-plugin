# Revit Command Driver (RCD) — Implementation Plan

Đọc trước: [00-brainstorm.md](00-brainstorm.md) · [01-spec.md](01-spec.md)
Repo liên quan: `mcp-addin/revit-mcp-plugin` (C#) và `revit-mcp-server` (TypeScript).
Ước lượng theo ngày công (1 dev), test trên Revit 2025 trước, sau đó ma trận 2024/2026/2027.

---

## Tổng quan phase

| Phase | Mục tiêu | Deliverable chính | Ước lượng |
|---|---|---|---|
| 0 | Spike — chốt kiến trúc thread & input | Bảng kết quả S1–S9, quyết định backend | 2–3 ngày |
| 1 | Catalog + Post + State (không gesture) | AI gọi được Delete/Pin/Hide/Undo/view cmds qua PostCommand | 3–4 ngày |
| 2 | Input driver + ChangeTracker | Demo E2E: AI vẽ wall/line/door bằng lệnh Revit | 5–6 ngày |
| 3 | Dialog policy, recipes, playbook, screenshot | Recipe engine + 10 recipe mẫu + prompt/resource | 4–5 ngày |
| 4 | Hardening & release | Overlay/abort, ma trận version, MSI, docs `.agents/` | 3–4 ngày |

Tổng: ~17–22 ngày. Sau Phase 2 đã có giá trị dùng được thực tế.

---

## Phase 0 — Spikes (không merge code production)

Cách nhanh nhất: dùng tool `send_code_to_revit` có sẵn để chạy C# runtime cho S1, S2, S7, S8; các spike Win32
(S3, S4) viết project console nhỏ `spikes/RcdSpike` hoặc cũng qua `send_code_to_revit`.

| Spike | Cách làm | Kết quả cần ghi | Quyết định phụ thuộc |
|---|---|---|---|
| **S1** ExternalEvent khi lệnh active | Handler A: `PostCommand(ArchitecturalWall)`. Từ socket thread sau 1s `Raise()` handler B ghi timestamp. Lặp với TaskDialog mở. | B chạy ngay / chỉ sau Esc / không bao giờ | Nếu không chạy → `includeLiveMapping` chỉ best-effort; mapping bắt buộc capture trước post (đã là thiết kế mặc định) |
| **S2** DPI | Ở màn 125% và 150%: so `UIView.GetWindowRectangle()` với `GetWindowRect(hwnd)` + `GetDpiForWindow` | Hệ số cần nhân | `ScreenMapper.dpiScale` |
| **S3** Foreground | Cho VS Code foreground → gọi `SetForegroundWindow(MainWindowHandle)` từ socket thread; thử `AttachThreadInput`, `SwitchToThisWindow`, trick `keybd_event(Alt)` | Cách nào chắc chắn | `SendInputBackend.EnsureForeground` |
| **S4** PostMessage | Wall active; `PostMessage(hwndView, WM_MOUSEMOVE/LBUTTONDOWN/LBUTTONUP)` với client coords | Revit có nhận điểm? cần MOUSEMOVE trước? | Có/không backend `postmessage` |
| **S5** Snap error | Vẽ 20 wall bằng click ở zoom 2/5/10 mm/px, có/không `SO`; đo lệch endpoint bằng API | Bảng sai số | Default `maxMmPerPixel`, khuyến nghị `SO` trong playbook |
| **S6** UIA Options Bar | Inspect.exe / FlaUI xem Height/Offset/Chain có AutomationId | Có/không | Phase 3 scope |
| **S7** PostCommand exception | Post khi TaskDialog mở; khi đang trong lệnh khác; khi đã post 1 lệnh chưa chạy | Exception type/message | Mapping mã lỗi `POST_PENDING` / `EXTERNAL_EVENT_TIMEOUT` |
| **S8** Default type | `SetDefaultElementTypeId(WallType)` → post Wall → click 2 điểm → đọc type wall. Lặp với `SetDefaultFamilyTypeId(Doors)`. Trên 2024 & 2025 | Có tác dụng? | Nếu không → fix-up `ChangeElementType` sau tạo |
| **S9** Multi-monitor / tiled | Kéo Revit sang màn 2; tile 2 view | Mapping còn đúng? | Kiểm `screenRect` theo virtual screen |

**Exit criteria Phase 0:** bảng S1–S9 điền đủ, chọn `inputBackend` mặc định, ghi kết quả vào `01-spec.md` §11.

---

## Phase 1 — Catalog, Post, State (MVP không gesture)

### C# (`commandset`)
- [ ] `Driver/Models/CommandInfo.cs`, `ViewMapping.cs`, `ChangeSet.cs`, `UiState.cs`
- [ ] `Driver/KeyboardShortcutsReader.cs` — parse XML, unit test với file mẫu 2025
- [ ] `Driver/CommandCatalog.cs` — reflection `PostableCommand`, join shortcuts, tags, cache file, `Resolve()`
- [ ] `Driver/ScreenMapper.cs` — `Capture()`, `ToScreen()`, `ToModel()`, `FitAndCapture()`
- [ ] `Driver/ChangeTracker.cs` + `plugin/Core/DriverEventHook.cs` — subscribe `DocumentChanged` khi bật server
- [ ] `Driver/StatusBarReader.cs`, `Driver/WindowProbe.cs`, `Driver/Native/Win32.cs`
- [ ] `Driver/DriverLock.cs`
- [ ] `Services/Driver/RcdListCommandsEventHandler.cs` + `Commands/Driver/RcdListCommandsCommand.cs`
- [ ] `Services/Driver/RcdPostCommandEventHandler.cs` + `Commands/Driver/RcdPostCommandCommand.cs`
      (prepare: selection / activeView / fit / defaultType → capture mapping → `Mark()` → `PostCommand`)
- [ ] `Commands/Driver/RcdUiStateCommand.cs` (direct path) — status + foreground + dialog + `Since(marker)`
- [ ] `Commands/Driver/RcdUiCancelCommand.cs` (direct path) — Esc×N qua SendInput + đóng dialog
- [ ] `command.json` — thêm `rcd_list_commands`, `rcd_post_command`, `rcd_ui_state`, `rcd_ui_cancel`
- [ ] Data: `Driver/Data/rcd-interaction-hints.json` (≥100 lệnh phổ biến), `rcd-status-patterns.json` (en)
- [ ] `.csproj`: copy `Driver/Data/*.json` ra output (`CopyToOutputDirectory`)

### TypeScript (`revit-mcp-server`)
- [ ] `src/rcd/schemas.ts` — zod chung (`pointMm`, `elementId`, `prepare`)
- [ ] `src/tools/revit_cmd_search.ts`, `revit_cmd_post.ts`, `revit_ui_state.ts`, `revit_ui_cancel.ts`
- [ ] Resource `revit://rcd/catalog` (cache in-memory từ lần search gần nhất)

### Acceptance
- `revit_cmd_search("wall")` trả ≥ 5 lệnh có shortcut `WA`, `canPost` đúng theo view.
- Chọn 3 wall → `revit_cmd_post("Delete", prepare.selectElementIds)` → `revit_ui_state` báo `deleted: [3 ids]`.
- `revit_cmd_post("Pin")`, `("HideElements")`, `("Undo")`, `("ZoomToFit")`, `("SynchronizeAndModifySettings", expect:"dialog")`
  hoạt động; dialog được `ui_state` phát hiện và `ui_cancel` đóng được.
- Post khi đang mở dialog → lỗi có nghĩa (`EXTERNAL_EVENT_TIMEOUT` / `POST_PENDING`), không treo server.

---

## Phase 2 — Input Driver (gesture)

### C#
- [ ] `Driver/Models/InputStep.cs` (+ JSON converter discriminated `type`), `InputStepResult.cs`
- [ ] `Driver/InputDriver.cs` — vòng lặp step, `statusAfter`, `stopOnDialog`, `dryRun`, abort flag
- [ ] `Driver/Backends/SendInputBackend.cs` — EnsureForeground (kết quả S3), mouse absolute, keyboard unicode/scancode, modifiers hold
- [ ] `Driver/Backends/PostMessageBackend.cs` — nếu S4 pass
- [ ] `waitStatus` / `waitChanges` polling 50ms
- [ ] `Commands/Driver/RcdUiInputCommand.cs` (direct path) + `command.json`
- [ ] Settings: `driver.*` keys + trang Settings UI (checkbox enable, combobox backend, numeric)

### TypeScript
- [ ] `src/tools/revit_ui_input.ts` — `z.discriminatedUnion` cho steps; description nêu rõ flow chuẩn
- [ ] Prompt `revit_modeler` (nội dung từ `03-playbook.md`)

### Acceptance (E2E trên Revit 2025, project mẫu, view Level 1)
- AI: `revit_cmd_post(ArchitecturalWall, fitPoints)` → `revit_ui_input([waitStatus, click A, click B, Esc×2])`
  → `revit_ui_state` có 1 wall; endpoint lệch ≤ 10 mm ở `mmPerPixel ≤ 5`.
- Vẽ wall bằng typed length (`move` + `type "6000"` + Enter) → chiều dài đúng ±1 mm.
- `Door` → click lên wall → door được host; `DetailLine` 3 đoạn (chain); `AlignedDimension` giữa 2 wall.
- `dryRun` trả đúng pixel; điểm ngoài view → `POINT_OFF_SCREEN`.
- Giữa batch, dialog mở → batch dừng `DIALOG_OPEN`; `revit_ui_cancel` phục hồi idle.

---

## Phase 3 — Dialog policy, Recipes, Playbook, (Screenshot)

### C#
- [ ] `Driver/DialogPolicy.cs` + hook `DialogBoxShowing` trong `DriverEventHook`; `Data/rcd-dialog-defaults.json`
- [ ] `Commands/Driver/RcdDialogPolicyCommand.cs` + `command.json`
- [ ] `WindowProbe.ClickButton(hwnd, caption)` cho dialog không bắt được bằng event
- [ ] (Optional) `rcd_capture_view` — `View.ExportImage` hoặc `PrintWindow(hwndView)` → PNG base64 cho vision loop
- [ ] (Nếu S6 pass) `Driver/UiaOptionsBar.cs` — set Height/Offset/Chain

### TypeScript
- [ ] `src/rcd/RecipeEngine.ts` — placeholder, `save`, `assert`, `when`, `onError`
- [ ] `src/tools/revit_recipe_run.ts`, `revit_recipe_save.ts`; resources `revit://rcd/recipes/*`, `revit://rcd/playbook`
- [ ] `src/tools/revit_dialog_policy.ts`
- [ ] `recipes/` mẫu (10): `draw_wall_2pts`, `draw_wall_typed_length`, `draw_wall_chain`, `place_door_on_wall`,
      `place_window_on_wall`, `aligned_dimension_2refs`, `tag_by_category_click`, `place_room_and_tag`,
      `delete_selection`, `copy_selection_with_offset`

### Acceptance
- Recipe `draw_wall_2pts` pass 10/10 lần liên tiếp; fail → `onError` cancel, Revit về idle.
- Dialog "…not joined" tự OK khi AI đặt rule; không rule → batch dừng và báo.
- AI (Claude) chỉ với prompt `revit_modeler` + tools tự vẽ được 4 wall khép kín + 1 door + dimension mà không cần hướng dẫn thêm.

---

## Phase 4 — Hardening & Release

- [ ] `DriverOverlay` WPF topmost + `WH_KEYBOARD_LL` bắt Esc×3 → abort
- [ ] `FOREGROUND_LOST` detection giữa batch; TTL lock tự nhả; log `Logs/rcd-*.log`
- [ ] Ma trận version: build & smoke test 2024 (net48), 2026, 2027 (net10) — `scripts/Build-RevitVersions.ps1`
- [ ] Locale: pattern status bar cho ≥1 ngôn ngữ khác (nếu cần)
- [ ] Docs: cập nhật `.agents/commands.md`, `.agents/server.md`, `.agents/architecture.md`, `AGENT.md`; thêm `.agents/rcd-driver.md`
- [ ] MSI: đưa `recipes/`, `Driver/Data/*.json` vào payload (`installers/msi/*.wxs`)
- [ ] Bảo mật: review whitelist dialog mặc định; đảm bảo không gửi input khi foreground ≠ Revit

---

## Thứ tự file thay đổi (tóm tắt)

```
mcp-addin/revit-mcp-plugin/
├── command.json                              (+6 commands rcd_*)
├── plugin/Core/DriverEventHook.cs           (mới) subscribe/unsubscribe khi bật server (MCPServiceConnection)
├── plugin/Core/Settings.cs                   (+driver.*)
├── plugin/UI/DriverSettingsPage.xaml(.cs)   (mới, Phase 2)
└── commandset/
    ├── Driver/**                             (mới — xem 01-spec.md §3)
    ├── Commands/Driver/Rcd*Command.cs        (mới)
    └── Services/Driver/Rcd*EventHandler.cs   (mới)

revit-mcp-server/
├── src/tools/revit_cmd_search.ts | revit_cmd_post.ts | revit_ui_input.ts | revit_ui_state.ts |
│   revit_ui_cancel.ts | revit_dialog_policy.ts | revit_recipe_run.ts | revit_recipe_save.ts   (mới)
├── src/rcd/schemas.ts | RecipeEngine.ts | resources.ts | prompt.ts                            (mới)
└── recipes/*.json                                                                              (mới)
```

---

## Test plan

| Loại | Nội dung |
|---|---|
| Unit (C#) | `KeyboardShortcutsReader` parse; `ScreenMapper` affine với rect/corners giả; `InputStep` JSON round-trip; `ChangeTracker.Since` |
| Unit (TS) | zod schemas reject step sai; `RecipeEngine` placeholder/assert/onError với mock client |
| Manual trong Revit | Checklist theo acceptance từng phase; chạy trên `rac_basic_sample_project.rvt` |
| Regression | Các tool cũ (`apply_operations`, `create_line_based_element`) vẫn chạy khi driver idle và khi driver lock |
| Chaos | Rút chuột/gõ phím khi AI đang thao tác → abort sạch; đổi view giữa batch → `mappingStale` |

---

## Rủi ro lịch

- S1 âm (ExternalEvent không chạy khi lệnh active) → không ảnh hưởng kế hoạch (thiết kế đã giả định).
- S3 + S4 đều thất bại (không foreground được và PostMessage bị bỏ qua) → cần user để Revit foreground khi AI
  chạy; ghi rõ trong playbook; vẫn ship được.
- S8 âm → thêm bước fix-up `ChangeElementType` vào recipes (đã có tool `modify_element`).
- UIA Options Bar (S6) không khả thi → bỏ khỏi v1, dùng fix-up API; không chặn release.

---

## Definition of Done (v1)

1. 6 command `rcd_*` chạy trên Revit 2024–2027, đăng ký trong `command.json`, bật/tắt được trong Settings.
2. 8 tool MCP + 3 resource + 1 prompt; `pnpm build` sạch; MCP Inspector liệt kê đủ.
3. 10 recipe mẫu pass 10/10 trên Revit 2025.
4. Playbook đủ để Claude/Cursor tự hoàn thành kịch bản "4 wall + door + dimension + tag".
5. Docs `.agents/*` cập nhật; MSI chứa payload mới.
