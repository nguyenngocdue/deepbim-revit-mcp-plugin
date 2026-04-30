# Triển khai AI Runtime Workflow cho DeepBIM MCP Revit

## Mục tiêu

Triển khai workflow để AI có thể tạo workflow Revit bằng cách compose các operation nhỏ, thay vì phải viết MCP tool riêng cho từng chức năng.

Kiến trúc mong muốn:

```text
AI Assistant
→ Node MCP Server
→ Revit Plugin C#
→ Operation Dispatcher
→ Operation Handlers
→ Revit API
```

Nguyên tắc:

```text
- Node chỉ expose vài MCP tool tổng quát.
- C# Revit Plugin mới là nơi gọi Revit API.
- AI sinh JSON operations.
- apply_operations nhận operations và dispatch theo field "op".
- Mọi thao tác sửa model phải chạy trong TransactionGroup/Transaction.
- Nếu lỗi thì rollback và trả structured JSON error về AI.
```

---

## 1. MCP tools cần có bên Node

Triển khai 3 tool tối thiểu:

```text
1. get_revit_context
2. run_python_script
3. apply_operations
```

Có thể thêm sau:

```text
4. query_elements
5. get_operation_catalog
```

---

## 1.1. Tool `get_revit_context`

Mục đích: lấy thông tin model hiện tại để AI không đoán mò.

Luồng:

```text
AI gọi get_revit_context
→ Node gửi command "get_revit_context" sang Revit Plugin
→ C# đọc model
→ trả JSON context về AI
```

Expected response:

```json
{
  "success": true,
  "document": {
    "title": "Demo.rvt",
    "pathName": "C:\\Project\\Demo.rvt",
    "isWorkshared": false
  },
  "activeView": {
    "id": 123,
    "name": "Level 1",
    "viewType": "FloorPlan",
    "scale": 100
  },
  "units": {
    "length": "millimeters",
    "internalLength": "feet"
  },
  "selection": {
    "count": 0,
    "selectedElementIds": []
  },
  "levels": [
    {
      "id": 101,
      "name": "Level 1",
      "elevation": 0
    },
    {
      "id": 102,
      "name": "Level 2",
      "elevation": 4000
    }
  ],
  "types": {
    "wallTypes": [
      {
        "id": 301,
        "name": "Basic Wall 200mm",
        "familyName": "Basic Wall",
        "width": 200
      }
    ]
  }
}
```

---

## 1.2. Tool `run_python_script`

Mục đích: cho AI dùng Python để tính toán logic và sinh danh sách operations.

Quan trọng:

```text
- Python không gọi Revit API.
- Python không sửa model.
- Python chỉ nhận context và trả về result.operations.
```

Input:

```json
{
  "code": "operations = []\n...\nresult = {\"operations\": operations}",
  "context": {}
}
```

Output nếu thành công:

```json
{
  "success": true,
  "result": {
    "operations": [
      {
        "op": "create_level",
        "name": "Level 2",
        "elevation": 4000
      }
    ]
  }
}
```

Output nếu lỗi:

```json
{
  "success": false,
  "stage": "python_execution",
  "errorType": "NameError",
  "message": "name 'operation' is not defined",
  "traceback": "..."
}
```

---

## 1.3. Tool `apply_operations`

Mục đích: gửi danh sách operations xuống Revit Plugin để C# execute.

Input:

```json
{
  "mode": "preview",
  "operations": [
    {
      "op": "create_level",
      "name": "Level 2",
      "elevation": 4000
    }
  ]
}
```

Mode:

```text
preview = chỉ validate, không sửa model
execute = validate rồi execute trong Transaction
```

Output preview:

```json
{
  "success": true,
  "mode": "preview",
  "summary": [
    "Operation 0: create_level"
  ]
}
```

Output execute success:

```json
{
  "success": true,
  "mode": "execute",
  "results": [
    {
      "success": true,
      "message": "Created level: Level 2",
      "elementId": 102
    }
  ]
}
```

Output execute error:

```json
{
  "success": false,
  "stage": "revit_transaction",
  "failedOperationIndex": 0,
  "failedOperation": {
    "op": "create_wall_by_level",
    "levelName": "Level 99"
  },
  "message": "Level not found: Level 99",
  "rolledBack": true
}
```

---

## 2. Node MCP Server implementation

Triển khai file ví dụ:

```text
src/server.ts
```

Node responsibilities:

```text
- Register MCP tools.
- Forward get_revit_context và apply_operations sang Revit Plugin.
- Chạy Python process cho run_python_script.
- Trả JSON text về AI.
```

Pseudo-code:

```ts
const REVIT_PLUGIN_URL = "http://127.0.0.1:8181/command";

async function sendToRevit(command: string, payload: any) {
  const response = await fetch(REVIT_PLUGIN_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ command, payload })
  });

  if (!response.ok) {
    throw new Error(`Revit plugin error: ${response.status} ${response.statusText}`);
  }

  return await response.json();
}
```

Register tools:

```ts
server.tool("get_revit_context", {}, async () => {
  const result = await sendToRevit("get_revit_context", {});
  return {
    content: [{ type: "text", text: JSON.stringify(result, null, 2) }]
  };
});

server.tool(
  "run_python_script",
  {
    code: z.string(),
    context: z.any().optional()
  },
  async ({ code, context }) => {
    const result = await runPythonCode(code, context ?? {});
    return {
      content: [{ type: "text", text: JSON.stringify(result, null, 2) }]
    };
  }
);

server.tool(
  "apply_operations",
  {
    mode: z.enum(["preview", "execute"]),
    operations: z.array(z.record(z.any()))
  },
  async ({ mode, operations }) => {
    const result = await sendToRevit("apply_operations", {
      mode,
      operations
    });

    return {
      content: [{ type: "text", text: JSON.stringify(result, null, 2) }]
    };
  }
);
```

`runPythonCode` requirements:

```text
- Write AI code to temp file.
- Inject variable `context`.
- Execute python script.
- Require script to define variable `result`.
- Print JSON to stdout.
- Catch exception and return structured JSON error.
- Timeout: 15 seconds.
- Max buffer: reasonable, e.g. 10MB.
```

---

## 3. C# Revit Plugin implementation

Triển khai core classes:

```text
RevitCommandDispatcher.cs
GetRevitContextHandler.cs
ApplyOperationsHandler.cs
OperationResult.cs
```

---

## 3.1. `RevitCommandDispatcher.cs`

Mục đích: nhận command từ HTTP/TCP layer và route sang handler.

```csharp
public class RevitCommandDispatcher
{
    private readonly UIApplication _uiapp;

    public RevitCommandDispatcher(UIApplication uiapp)
    {
        _uiapp = uiapp;
    }

    public object Dispatch(string command, JObject payload)
    {
        switch (command)
        {
            case "get_revit_context":
                return new GetRevitContextHandler(_uiapp).Execute();

            case "apply_operations":
                return new ApplyOperationsHandler(_uiapp).Execute(payload);

            default:
                return new
                {
                    success = false,
                    message = $"Unknown command: {command}"
                };
        }
    }
}
```

---

## 3.2. `GetRevitContextHandler.cs`

Mục đích: read-only context collector.

Cần lấy:

```text
- Document title/path/worksharing
- Active view id/name/type/scale
- Units info
- Current selection ids
- Levels: id/name/elevation in mm
- WallTypes: id/name/familyName/width in mm
```

Use Revit API:

```csharp
FilteredElementCollector(doc).OfClass(typeof(Level))
FilteredElementCollector(doc).OfClass(typeof(WallType))
uidoc.Selection.GetElementIds()
doc.ActiveView
doc.Title
doc.PathName
doc.IsWorkshared
```

Return JSON object with `success = true`.

Convert feet to mm:

```csharp
private static double FeetToMm(double feet)
{
    return Math.Round(feet * 304.8, 3);
}
```

---

## 3.3. `OperationResult.cs`

```csharp
public class OperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int? ElementId { get; set; }

    public static OperationResult Ok(string message, int? elementId = null)
    {
        return new OperationResult
        {
            Success = true,
            Message = message,
            ElementId = elementId
        };
    }

    public static OperationResult Fail(string message)
    {
        return new OperationResult
        {
            Success = false,
            Message = message
        };
    }
}
```

---

## 3.4. `ApplyOperationsHandler.cs`

Mục đích:

```text
- Nhận payload { mode, operations }
- Validate operations
- Nếu mode preview: chỉ trả summary
- Nếu mode execute: chạy từng operation trong TransactionGroup
- Dispatch theo field op
- Rollback toàn bộ nếu lỗi
```

Operation handler registry:

```csharp
_handlers = new Dictionary<string, Func<JObject, OperationResult>>
{
    ["create_level"] = CreateLevel,
    ["create_grid_line"] = CreateGridLine,
    ["create_wall_by_level"] = CreateWallByLevel
};
```

Dispatch:

```csharp
private OperationResult ExecuteOneOperation(JObject op)
{
    string opName = op["op"]?.ToString();

    if (string.IsNullOrWhiteSpace(opName))
        return OperationResult.Fail("Missing field: op");

    if (!_handlers.TryGetValue(opName, out var handler))
        return OperationResult.Fail($"Unknown operation: {opName}");

    return handler(op);
}
```

Validation requirements:

```text
create_level requires:
- name
- elevation

create_grid_line requires:
- name
- start
- end

create_wall_by_level requires:
- typeName
- levelName
- start
- end
- height
```

Execute transaction:

```csharp
using (var tg = new TransactionGroup(_doc, "AI Apply Operations"))
{
    tg.Start();

    try
    {
        using (var tx = new Transaction(_doc, "Apply Operations"))
        {
            tx.Start();

            foreach operation:
                result = ExecuteOneOperation(op)
                if !result.Success:
                    tx.RollBack()
                    tg.RollBack()
                    return structured error

            tx.Commit();
        }

        tg.Assimilate();
        return success result;
    }
    catch (Exception ex)
    {
        tg.RollBack();
        return structured exception;
    }
}
```

---

## 4. Primitive operations cần triển khai ban đầu

## 4.1. `create_level`

Input:

```json
{
  "op": "create_level",
  "name": "Level 2",
  "elevation": 4000
}
```

Behavior:

```text
- Nếu level name đã tồn tại, trả success với existing id.
- Nếu chưa tồn tại, Level.Create(doc, elevationFeet).
- Set name.
```

---

## 4.2. `create_grid_line`

Input:

```json
{
  "op": "create_grid_line",
  "name": "A",
  "start": [0, 0, 0],
  "end": [0, 20000, 0]
}
```

Behavior:

```text
- Convert start/end from mm to internal feet.
- Create Line.
- Grid.Create(doc, line).
- Set grid.Name.
```

---

## 4.3. `create_wall_by_level`

Input:

```json
{
  "op": "create_wall_by_level",
  "typeName": "Basic Wall 200mm",
  "levelName": "Level 1",
  "start": [0, 0, 0],
  "end": [10000, 0, 0],
  "height": 4000
}
```

Behavior:

```text
- Find WallType by typeName.
- Find Level by levelName.
- Convert start/end/height from mm to feet.
- Create Line.
- Wall.Create(doc, line, wallType.Id, level.Id, heightFeet, 0, false, false).
```

---

## 5. Utility functions

Need:

```csharp
private double MmToFeet(double mm)
{
    return mm / 304.8;
}

private double FeetToMm(double feet)
{
    return feet * 304.8;
}

private XYZ ToXyzFromMm(JToken token)
{
    double[] values = token.ToObject<double[]>();

    if (values == null || values.Length != 3)
        throw new Exception("Point must be [x, y, z].");

    return new XYZ(
        MmToFeet(values[0]),
        MmToFeet(values[1]),
        MmToFeet(values[2])
    );
}
```

---

## 6. Full workflow example

User request:

```text
Tạo cho tôi một tấm tường từ tầng 1 đến 20, nhưng mỗi tầng là 1 wall element riêng.
```

AI workflow:

```text
1. Call get_revit_context.
2. Read existing levels and wall types.
3. Generate Python script to create operation list.
4. Call run_python_script with context.
5. Extract result.operations.
6. Call apply_operations mode="preview".
7. If preview success, call apply_operations mode="execute".
8. Report created element ids.
```

Python generated by AI:

```python
operations = []

wall_type = context["types"]["wallTypes"][0]["name"]
floor_height = 4000
wall_start = [0, 0, 0]
wall_end = [10000, 0, 0]

existing_levels = set([level["name"] for level in context["levels"]])

for i in range(1, 21):
    level_name = f"Level {i}"

    if level_name not in existing_levels:
        operations.append({
            "op": "create_level",
            "name": level_name,
            "elevation": (i - 1) * floor_height
        })

    operations.append({
        "op": "create_wall_by_level",
        "typeName": wall_type,
        "levelName": level_name,
        "start": wall_start,
        "end": wall_end,
        "height": floor_height
    })

result = {
    "operations": operations
}
```

Result operations example:

```json
{
  "operations": [
    {
      "op": "create_level",
      "name": "Level 2",
      "elevation": 4000
    },
    {
      "op": "create_wall_by_level",
      "typeName": "Basic Wall 200mm",
      "levelName": "Level 1",
      "start": [0, 0, 0],
      "end": [10000, 0, 0],
      "height": 4000
    }
  ]
}
```

---

## 7. Important behavior

## Error feedback loop

All errors must be returned to AI as structured JSON.

Python error format:

```json
{
  "success": false,
  "stage": "python_execution",
  "errorType": "NameError",
  "message": "...",
  "traceback": "..."
}
```

Validation error format:

```json
{
  "success": false,
  "stage": "validation",
  "failedOperationIndex": 0,
  "failedOperation": {},
  "message": "create_wall_by_level requires levelName."
}
```

Transaction error format:

```json
{
  "success": false,
  "stage": "revit_transaction",
  "failedOperationIndex": 3,
  "failedOperation": {},
  "message": "Level not found: Level 99",
  "rolledBack": true
}
```

## Safety

```text
- run_python_script does not access Revit API.
- apply_operations is the only path that modifies Revit.
- execute mode always runs in TransactionGroup.
- rollback on any failure.
- preview mode must not modify model.
- C# validates all operation fields before execute.
```

---

## 8. Deliverables

Please implement:

```text
Node:
- src/server.ts
- sendToRevit(command, payload)
- runPythonCode(code, context)
- MCP tools:
  - get_revit_context
  - run_python_script
  - apply_operations

C#:
- RevitCommandDispatcher.cs
- GetRevitContextHandler.cs
- ApplyOperationsHandler.cs
- OperationResult.cs
- Primitive operations:
  - create_level
  - create_grid_line
  - create_wall_by_level
- Utility:
  - MmToFeet
  - FeetToMm
  - ToXyzFromMm
```

Do not implement one MCP tool per Revit operation. Keep MCP tools generic and put operation dispatching in C#.
