# Available Commands — revit-mcp-plugin

Declared in `command.json`. All map to `RevitMCPCommandSet/{VERSION}/RevitMCPCommandSet.dll`.

## Read / Query

| Command | Description |
|---------|-------------|
| `get_current_view_info` | Current view metadata (name, type, scale, level) |
| `get_current_view_elements` | All elements visible in the current view |
| `get_selected_elements` | Currently selected elements |
| `get_available_family_types` | All loaded family types in the model |
| `get_material_quantities` | Material takeoffs / quantities |
| `get_sheet_exportable_properties` | Available sheet parameters for export |
| `ai_element_filter` | Query elements by criteria (category, parameter values, etc.) |
| `analyze_model_statistics` | Model complexity stats (element counts by category) |

## Create

| Command | Description |
|---------|-------------|
| `create_line_based_element` | Walls, beams, pipes, ducts (line-based) |
| `create_point_based_element` | Furniture, columns, equipment (point-based) |
| `create_surface_based_element` | Floors, ceilings, roofs (surface-based) |
| `create_grid` | Grid system with smart spacing |
| `create_structural_framing_system` | Beam framing grid |
| `create_room` | Place rooms at specified locations |
| `create_level` | Levels at specified elevations |
| `create_dimensions` | Dimension annotations between elements or points |

## Operate / Modify

| Command | Description |
|---------|-------------|
| `operate_element` | Select / color / hide / isolate / show elements |
| `color_splash` | Color elements by parameter value |
| `tag_walls` | Tag all walls in current view |
| `tag_rooms` | Tag all rooms in current view |
| `delete_element` | Delete elements by ElementId |

## Export / Data

| Command | Description |
|---------|-------------|
| `export_room_data` | Export all rooms with detailed properties |
| `export_sheets_to_excel` | Export sheet data to Excel file |

## Dynamic Execution

| Command | Description |
|---------|-------------|
| `send_code_to_revit` | Compile and execute dynamic C# code inside Revit at runtime |

## Test

| Command | Description |
|---------|-------------|
| `say_hello` | Test greeting dialog |
| `hello_world` | Show hello world dialog with user's full name |
