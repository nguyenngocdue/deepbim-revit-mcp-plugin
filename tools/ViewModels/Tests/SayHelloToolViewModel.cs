using Newtonsoft.Json.Linq;

namespace DeepBimMCPTools
{
    public sealed class SayHelloToolViewModel : ToolViewModelBase
    {
        public SayHelloToolViewModel(IToolExecutionService executionService) : base(executionService)
        {
        }

        public override string MethodName => "say_hello";
        public override string DialogTitle => "Test: say_hello";
        public string Message { get; set; } = "Hello from Tools panel";
        public override int MaxResultLength => 500;

        protected override JObject BuildParameters()
        {
            return new JObject
            {
                ["message"] = Message
            };
        }
    }
}
