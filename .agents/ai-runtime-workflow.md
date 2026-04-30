# AI Runtime Workflow — revit-mcp-plugin

## Overview

The AI Runtime Workflow enables an AI agent (Claude, GPT, etc.) to read a Revit model's context and create elements autonomously using 3 MCP tools and a registry-based operation system.

---

## MCP Tools (Node.js → Revit)

| Tool | Command sent to Revit | Purpose |
|---|---|---|
| `get_revit_context` | `get_revit_context` | Read model state (levels, wall types, units, selection, active view) |
| `run_python_script` | *(local only — no Revit call)* | Run Python to compute geometry / generate operation lists |
| `apply_operations` | `apply_operations` | Execute a list of primitive ops inside a TransactionGroup |

---

## Runtime Flow

```
Step 1 — AI reads model
────────────────────────────────────────────
AI → get_revit_context
       │ TCP → localhost:8080
       ▼
  GetRevitContextEventHandler (Revit main thread)
  Returns:
  {
    document:   { title, pathName, isWorkshared },
    activeView: { id, name, viewType, scale },
    units:      { length: "mm" },
    selection:  { count, selectedElementIds },
    levels:     [{ id, name, elevation(mm) }],
    types: {
      wallTypes: [{ id, name, familyName, width(mm) }]
    }
  }


Step 2 — AI computes (optional)
────────────────────────────────────────────
AI → run_python_script(code, context)
       │ spawns python locally, injects context variable
       ▼
  Python returns computed operation list:
  [
    { op: "create_level",     name: "L1", elevation: 0    },
    { op: "create_grid_line", name: "A",  start: [0,0,0], end: [10000,0,0] },
    ...
  ]


Step 3 — AI validates (preview)
────────────────────────────────────────────
AI → apply_operations(mode: "preview", operations: [...])
       │
       ▼
  ApplyOperationsEventHandler
  → OperationHandlerRegistry.TryGet(opName) for each op
  → check RequiredFields
  → NO model changes
  Returns: { success: true, mode: "preview", summary: ["op 0: create_level", ...] }


Step 4 — AI executes
────────────────────────────────────────────
AI → apply_operations(mode: "execute", operations: [...])
       │
       ▼
  ApplyOperationsEventHandler
  ┌─ TransactionGroup.Start()
  │  Transaction.Start()
  │  for each op:
  │    OperationHandlerRegistry.TryGet(opName) → handler.Execute(doc, op)
  │    if fail → Transaction.RollBack() + TransactionGroup.RollBack()
  │              return { success: false, failedOperationIndex: i, rolledBack: true }
  │  Transaction.Commit()
  └─ TransactionGroup.Assimilate()
  Returns: { success: true, results: [{ elementId: 123 }, { elementId: 124 }, ...] }
```

---

## IOperationHandler Pattern

All primitive operations implement a single interface. The registry auto-discovers them via reflection at startup — no registration required.

```csharp
public interface IOperationHandler
{
    string   OpName         { get; }  // e.g. "create_level"
    string[] RequiredFields { get; }  // validated before Execute() is called
    OperationResult Execute(Document doc, JObject op);
}
```

**Registry (auto-discovery at static ctor):**
```csharp
_handlers = Assembly.GetExecutingAssembly().GetTypes()
    .Where(t => typeof(IOperationHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
    .Select(t => (IOperationHandler)Activator.CreateInstance(t)!)
    .ToDictionary(h => h.OpName, StringComparer.OrdinalIgnoreCase);
```

**File layout:**
```
commandset/Operations/
├── IOperationHandler.cs              ← interface
├── OperationHandlerRegistry.cs       ← reflection scan → Dictionary<opName, handler>
├── OperationUtils.cs                 ← MmToFeet(), ToXyzFromMm(), GetElementId()
├── CreateLevelOperation.cs
├── CreateGridLineOperation.cs
├── CreateWallByLevelOperation.cs
├── CreateColumnByLevelOperation.cs
├── CreateFloorByBoundaryOperation.cs
└── CreateIsolatedFoundationOperation.cs
```

---

## Supported Operations

| OpName | Required Fields | Notes |
|---|---|---|
| `create_level` | `name`, `elevation` | Skips if level name already exists |
| `create_grid_line` | `name`, `start`, `end` | `start`/`end` = `[x,y,z]` in mm |
| `create_wall_by_level` | `typeName`, `levelName`, `start`, `end`, `height` | All dims in mm |
| `create_column_by_level` | `familyTypeName`, `levelName`, `x`, `y` | Matches "TypeName" or "FamilyName - TypeName" |
| `create_floor_by_boundary` | `levelName`, `floorTypeName`, `boundary` | `boundary` = `[[x,y], ...]` min 3 pts in mm |
| `create_isolated_foundation` | `familyTypeName`, `levelName`, `x`, `y` | StructuralType.Footing |

---

## Adding a New Operation

Create one file in `commandset/Operations/`. No other file needs to change.

```csharp
// commandset/Operations/CreateMyElementOperation.cs
public class CreateMyElementOperation : IOperationHandler
{
    public string   OpName         => "create_my_element";
    public string[] RequiredFields => ["levelName", "x", "y"];

    public OperationResult Execute(Document doc, JObject op)
    {
        // All input is pre-validated — RequiredFields guaranteed present
        double x = OperationUtils.MmToFeet(op["x"]!.ToObject<double>());
        double y = OperationUtils.MmToFeet(op["y"]!.ToObject<double>());

        // ... call Revit API ...

        return OperationResult.Ok("Created element", elementId);
        // or: return OperationResult.Fail("reason") → rolls back entire group
    }
}
```

Registry picks it up on next plugin load. If an unknown op is sent, the error response automatically lists all available ops from the registry.

---

## Error Handling

| Stage | Behaviour |
|---|---|
| `validation` | Caught before transaction opens. Response includes `failedOperationIndex`, op object, and message. `rolledBack: false`. |
| `revit_transaction` | Transaction + TransactionGroup both rolled back. All previously executed ops in the batch are undone. `rolledBack: true`. |
| `python_execution` | Python stderr/exception returned directly. No Revit call made. |

---

## Key Files

| File | Location |
|---|---|
| MCP tool: get context | `revit-mcp-server/src/tools/get_revit_context.ts` |
| MCP tool: python | `revit-mcp-server/src/tools/run_python_script.ts` |
| MCP tool: apply ops | `revit-mcp-server/src/tools/apply_operations.ts` |
| Revit command: context | `commandset/Commands/GetRevitContextCommand.cs` |
| Revit command: ops | `commandset/Commands/ApplyOperationsCommand.cs` |
| Context handler | `commandset/Services/GetRevitContextEventHandler.cs` |
| Ops dispatcher | `commandset/Services/ApplyOperationsEventHandler.cs` |
| Op interface | `commandset/Operations/IOperationHandler.cs` |
| Op registry | `commandset/Operations/OperationHandlerRegistry.cs` |
| Op utilities | `commandset/Operations/OperationUtils.cs` |
