# Revit Command Driver — AI Modeler Playbook

> Tài liệu này sẽ được expose làm MCP resource `revit://rcd/playbook` và prompt `revit_modeler`.
> Đối tượng đọc: **AI agent** đang điều khiển Revit qua các tool `revit_cmd_*` / `revit_ui_*`.

---

## 1. Tư duy: bạn là một modeler ngồi trước Revit

Bạn không "tạo phần tử bằng API". Bạn **bật lệnh của Revit** rồi **click / gõ** như người dùng, và **nhìn** status bar
để biết Revit đang chờ gì. Revit lo phần join, host, snap, warning. Bạn lo phần đúng điểm, đúng thứ tự, và dọn dẹp.

Vòng lặp chuẩn cho **mọi** lệnh:

```
1. SEARCH        revit_cmd_search(query)            → chọn đúng name, xem `interaction`, `canPost`
2. PREPARE+POST  revit_cmd_post(command, prepare{selectElementIds, fitPoints, defaultType})
                                                    → nhận `mapping`, `marker`
3. INPUT         revit_ui_input(steps)              → LUÔN mở bằng waitStatus, đóng bằng Escape ×2
4. STATE         revit_ui_state(sinceMarker)        → xác nhận `changes.added/modified/deleted`, `idle: true`
5. FIX-UP        modify_element(...) nếu cần        → set tham số (Height, Level, Mark…) bằng API, KHÔNG dùng Options Bar
6. RECOVER       revit_ui_cancel()                  → gọi ngay khi bất kỳ bước nào lỗi hoặc `dialog.open = true`
```

Quy tắc vàng:
- **Chuẩn bị bằng API, thao tác bằng tay, sửa bằng API.** Type/level/selection/zoom → `prepare`. Điểm/đường/pick → `steps`.
- **Một lệnh tại một thời điểm.** Không post lệnh mới khi `idle: false`.
- **Không đoán mapping.** Luôn dùng `mapping` từ lần post gần nhất; nếu `mappingStale: true` hoặc bạn vừa đổi view → post lại.
- **Zoom vừa đủ.** Truyền `fitPoints` bao toàn bộ điểm sẽ click; `mmPerPixel ≤ 5` cho kết cấu, ≤ 2 cho chi tiết.
- **Ưu tiên gõ số** cho kích thước quan trọng (typed length), click chỉ để định hướng.
- **Esc ×2 kết thúc mọi lệnh.** Esc thứ nhất huỷ bước hiện tại, Esc thứ hai thoát lệnh.
- **Không tự trả lời dialog nguy hiểm** (xoá, ghi đè, sync, detach). Chỉ đặt `revit_dialog_policy` cho warning thuần.

---

## 2. Đọc status bar (Revit tiếng Anh)

| Status bar chứa… | Revit đang chờ | Bạn nên |
|---|---|---|
| `Click to select, TAB for alternates…` | Không có lệnh (idle) | Post lệnh mới |
| `Click to enter wall start point.` | Điểm đầu | `click` |
| `Click to enter wall end point.` | Điểm cuối | `click` hoặc `move` + `type` số + Enter |
| `Click on a Wall to place Door.` / `…to place…` | Pick host | `click` lên vị trí trên wall |
| `Pick a reference…` / `Select the reference…` | Pick tham chiếu (dimension/align) | `click` lên cạnh phần tử |
| `Click to place…` | Điểm đặt | `click` |
| `Select elements…` | Cần selection | Esc, rồi post lại với `prepare.selectElementIds` |
| `Click on Finish…` / sketch mode | Đang trong sketch | Xong boundary → click nút ✓ (Phase 3) hoặc Esc để huỷ |
| Text lạ / dialog | Chưa rõ | `revit_ui_state` → xem `dialog` → `revit_ui_cancel` |

---

## 3. Recipes chuẩn (mô tả bằng steps — cũng có sẵn dạng `revit_recipe_run`)

### 3.1 Wall thẳng giữa 2 điểm
```jsonc
revit_cmd_post({ command: "ArchitecturalWall", expect: "points",
  prepare: { fitPoints: [A, B], defaultType: { group: "WallType", typeId: WALL_TYPE_ID } } })
revit_ui_input({ steps: [
  { type: "waitStatus", contains: "start point" },
  { type: "click", point: A },
  { type: "click", point: B },
  { type: "key", key: "Escape", times: 2 } ] })
revit_ui_state({ sinceMarker }) → added[0] = wallId
modify_element({ elementId: wallId, parameters: { "Unconnected Height": 3000 } })   // fix-up
```

### 3.2 Wall theo chiều dài gõ tay (chính xác ±1 mm)
```jsonc
steps: [
  { type: "waitStatus", contains: "start point" },
  { type: "click", point: A },
  { type: "move",  point: [A.x + 1000, A.y, A.z], holdShift: true },   // định hướng +X, Shift = ortho
  { type: "type",  text: "6000", enter: true },
  { type: "key",   key: "Escape", times: 2 } ]
```

### 3.3 Chuỗi wall khép kín (4 cạnh)
Revit Wall mặc định **Chain = on**: click liên tiếp A→B→C→D→A rồi Esc ×2. Nếu wall không nối tiếp
(status quay về "start point" sau mỗi đoạn) → Chain đang off → làm từng đoạn theo 3.1.

### 3.4 Door / Window lên wall
```jsonc
revit_cmd_post({ command: "Door", expect: "points",
  prepare: { fitPoints: [P], fitPaddingMm: 3000, defaultType: { categoryId: -2000023, typeId: DOOR_TYPE_ID } } })
steps: [
  { type: "waitStatus", contains: "place" },
  { type: "click", point: P },          // P nằm TRÊN đường tim wall; Revit tự host
  { type: "key", key: "Escape", times: 2 } ]
```
Sau đó `revit_ui_state` phải có `added` 1 door; nếu `added` rỗng → điểm không trúng wall → post lại với `fitPoints` sát hơn.

### 3.5 Aligned Dimension giữa 2 wall
```jsonc
revit_cmd_post({ command: "AlignedDimension", expect: "points", prepare: { fitPoints: [W1, W2, PLACE] } })
steps: [
  { type: "waitStatus", contains: "reference" },
  { type: "click", point: W1_face },    // điểm trên mặt / tim wall 1
  { type: "click", point: W2_face },
  { type: "click", point: PLACE },      // điểm trống để đặt dim line
  { type: "key", key: "Escape", times: 2 } ]
```

### 3.6 Tag by Category (click từng phần tử)
```jsonc
revit_cmd_post({ command: "TagByCategory", expect: "points", prepare: { fitPoints: [...] } })
steps: [ { type: "waitStatus", contains: "Click" },
         { type: "click", point: E1 }, { type: "click", point: E2 },
         { type: "key", key: "Escape", times: 2 } ]
```
Với "tag tất cả" → dùng tool API `tag_all_walls` / `tag_all_rooms` nhanh hơn.

### 3.7 Lệnh trên selection (không cần click)
```jsonc
revit_cmd_post({ command: "Delete", expect: "selection", prepare: { selectElementIds: [..] } })
revit_ui_state({ sinceMarker }) → deleted
```
Áp dụng tương tự: `Pin`, `Unpin`, `HideElements`, `IsolateElements`, `Group`, `Ungroup`.
(`JoinGeometry`, `Align`, `Trim` cần pick → là `points`.)

### 3.8 Copy với offset gõ tay
```jsonc
revit_cmd_post({ command: "Copy", expect: "points",
  prepare: { selectElementIds: [..], fitPoints: [BASE, BASE+OFFSET] } })
steps: [
  { type: "waitStatus", contains: "start point" },      // "Click to enter move start point."
  { type: "click", point: BASE },
  { type: "move",  point: [BASE.x + 1000, BASE.y, BASE.z], holdShift: true },
  { type: "type",  text: "2500", enter: true },
  { type: "key",   key: "Escape", times: 2 } ]
```

### 3.9 Room + Room Tag
```jsonc
revit_cmd_post({ command: "Room", expect: "points", prepare: { fitPoints: [CENTER] } })
steps: [ { type: "waitStatus", contains: "Click" }, { type: "click", point: CENTER },
         { type: "key", key: "Escape", times: 2 } ]
```
Room tự có tag nếu option "Tag on Placement" đang bật (mặc định). Kiểm tra `added` có 2 id (Room + RoomTag).

### 3.10 Lệnh mở dialog (Sync, Print, Export…)
```jsonc
revit_cmd_post({ command: "SynchronizeAndModifySettings", expect: "dialog" })
revit_ui_state() → dialog.open, dialog.buttons
```
**Dừng và hỏi người dùng** trước khi bấm OK cho Sync/Save/Export/Delete-worksets. Chỉ tự đóng bằng
`revit_ui_cancel(closeDialogs: "cancel")`.

---

## 4. Snap & độ chính xác

- Snap là bạn: click **gần** endpoint/midpoint (trong 3–4 px) Revit sẽ bắt đúng. Đó là cách nhanh nhất để nối wall.
- Snap là thù khi cần điểm tự do: thêm `snapOverride: "SO"` vào step click đó.
- Ép snap: `SE` endpoint, `SM` midpoint, `SI` intersection, `SC` center, `SP` perpendicular.
- Kích thước quan trọng → gõ số (3.2, 3.8). Click chỉ để định hướng, giữ `holdShift` cho ortho.
- `dryRun: true` để xem pixel trước khi gửi nếu không chắc mapping.

---

## 5. Xử lý sự cố

| Triệu chứng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| `CANNOT_POST` | View hiện tại không cho lệnh (Sheet/3D/Schedule) | `prepare.activeViewId` sang plan |
| `POST_PENDING` | Lệnh trước chưa thoát | `revit_ui_cancel` → post lại |
| `EXTERNAL_EVENT_TIMEOUT` | Revit đang trong lệnh/dialog | `revit_ui_state` → `revit_ui_cancel` |
| `statusAfter` không đổi sau click | Click rơi ngoài view / Revit không foreground | Kiểm `foreground.isRevit`, `POINT_OFF_SCREEN`, post lại với `fitPoints` |
| `added` rỗng dù đã Esc | Điểm không hợp lệ (door không trúng wall) / Revit huỷ | Zoom sát hơn (`maxMmPerPixel: 2`), click đúng tim wall |
| Wall tạo ra sai type | `defaultType` không áp dụng | `modify_element` đổi type |
| Dialog "…not joined / …overlap" | Warning thường | `revit_dialog_policy` OK với `once: true`, hoặc chấp nhận và tiếp tục |
| `DRIVER_BUSY` | Phiên khác đang điều khiển | Chờ hoặc `revit_ui_cancel` với token đó (nếu bạn sở hữu) |
| `USER_ABORTED` | Người dùng nhấn Esc ×3 | Dừng hoàn toàn, báo cáo, không tự tiếp tục |

---

## 6. Khi nào dùng tool API thay vì lệnh Revit?

| Việc | Nên dùng |
|---|---|
| Đọc model, tìm phần tử, tham số | `get_revit_context`, `ai_element_filter`, `get_current_view_elements` |
| Tạo hàng loạt (50 wall, grid 10×10) | `apply_operations` (1 transaction, rollback được) |
| Tạo vài phần tử cần hành vi "như người" (join, host, chain) | **RCD** |
| Lệnh không có API tương đương (Align, Trim, Split, Mirror, Array, Sync, Print, Purge) | **RCD** |
| Sửa tham số sau tạo | `modify_element` |
| Chọn / tô màu / ẩn hiện | `operate_element` hoặc RCD (Pin/Hide) — tuỳ cái nào ngắn hơn |

Kết hợp cả hai là bình thường: dựng khung bằng `apply_operations`, rồi dùng RCD cho door/dimension/tag/trim.
