# Services Features

Domain services grouped by feature area.

Flow:
`Command (bootstrapper) -> ViewModel -> FeatureService -> ToolExecutionService -> ICommandGateway -> RevitMCPCommandSet`

Create one subfolder per domain (for example `Geometry`, `Sheets`, `Rooms`).
