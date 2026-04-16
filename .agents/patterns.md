# Key Patterns — revit-mcp-plugin

## Adding a New Command (full flow, 4 steps)

### Step 1 — Model (if needed)
`commandset/Models/` — Add request/response DTO:
```csharp
public class MyRequest { public string ParamA { get; set; } }
public class MyResult  { public string Data   { get; set; } }
```

### Step 2 — EventHandler (Revit main thread)
`commandset/Services/MyCommandEventHandler.cs`:
```csharp
public class MyCommandEventHandler : IExternalEventHandler
{
    public MyRequest Request { get; set; }
    public AIResult<MyResult> Result { get; private set; }
    private readonly ManualResetEventSlim _resetEvent = new(false);

    public void Execute(UIApplication app)
    {
        try {
            // Call Revit API here (on main thread)
            Result = new AIResult<MyResult> { Success = true, Response = new MyResult { Data = "..." } };
        }
        catch (Exception ex) {
            Result = new AIResult<MyResult> { Success = false, Message = ex.Message };
        }
        finally { _resetEvent.Set(); }
    }

    public bool WaitForResult(int timeoutMs = 30000) => _resetEvent.Wait(timeoutMs);
    public void Reset() => _resetEvent.Reset();
    public string GetName() => "MyCommandEventHandler";
}
```

### Step 3 — Command class (background thread)
`commandset/Commands/MyCommand.cs`:
```csharp
public class MyCommand : IRevitCommand
{
    private readonly MyCommandEventHandler _handler;
    private readonly ExternalEvent _event;

    public MyCommand(UIApplication app)
    {
        _handler = new MyCommandEventHandler();
        _event   = ExternalEventManager.Instance.GetOrCreateEvent(_handler);
    }

    public string Execute(JObject parameters)
    {
        _handler.Reset();
        _handler.Request = parameters.ToObject<MyRequest>();
        _event.Raise();
        if (!_handler.WaitForResult())
            return JsonConvert.SerializeObject(new AIResult<MyResult> { Success = false, Message = "Timeout" });
        return JsonConvert.SerializeObject(_handler.Result);
    }

    public string CommandName => "my_command";
}
```

### Step 4 — Register in command.json
```json
{ "commandName": "my_command", "assemblyPath": "RevitMCPCommandSet/{VERSION}/RevitMCPCommandSet.dll", "enabled": true }
```

---

## Why ExternalEvent?

Revit API **must** be called from the Revit main thread.
The TCP socket listener runs on a **background thread** → cannot call Revit API directly.

Solution: `IExternalEventHandler` + `ExternalEvent.Raise()` marshals execution back to the main thread.

```
Background thread (TCP)  →  command.Execute()  →  event.Raise()
                                                         ↓
Main thread (Revit)      ←  handler.Execute(UIApplication)
```

`ManualResetEventSlim` is used to block the background thread until the main thread finishes.

---

## JSON-RPC Flow

```
TCP request
  → SocketService (background thread)
  → CommandExecutor.Execute(JsonRPCRequest)
  → RevitCommandRegistry.TryGetCommand(method)
  → IRevitCommand.Execute(params)
  → ExternalEvent.Raise()  ← background thread blocks here
      ↓ (main thread)
  → IExternalEventHandler.Execute(UIApplication)
  → ManualResetEventSlim.Set()
  ← background thread unblocks
  → JSON-RPC response written to TCP socket
```

---

## Coordinate System

All DTO coordinates use **millimeters** (`JZPoint.x`, `JZPoint.y`, `JZPoint.z`).

EventHandlers convert to Revit internal units (decimal feet):
```csharp
double internalValue = UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
```

---

## AIResult<T> Pattern

All commands return `AIResult<T>` serialized as JSON string:
```csharp
public class AIResult<T>
{
    public bool    Success  { get; set; }
    public string  Message  { get; set; }
    public T       Response { get; set; }
}
```

The MCP Server tools unwrap this and return the content to the AI client.
