using Newtonsoft.Json.Linq;

namespace DeepBimMCPTools
{
    public sealed class ExportRoomDataToolViewModel : ToolViewModelBase
    {
        public ExportRoomDataToolViewModel(IToolExecutionService executionService) : base(executionService)
        {
        }

        public override string MethodName => "export_room_data";
        public override string DialogTitle => "Test: export_room_data";
        public override int MaxResultLength => 800;

        public bool IncludeUnplacedRooms { get; set; }
        public bool IncludeNotEnclosedRooms { get; set; }

        protected override JObject BuildParameters()
        {
            return new JObject
            {
                ["includeUnplacedRooms"] = IncludeUnplacedRooms,
                ["includeNotEnclosedRooms"] = IncludeNotEnclosedRooms
            };
        }
    }
}
