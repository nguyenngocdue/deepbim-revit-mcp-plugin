namespace DeepBimMCPTools
{
    public sealed class GetCurrentViewInfoToolViewModel : ToolViewModelBase
    {
        public GetCurrentViewInfoToolViewModel(IToolExecutionService executionService) : base(executionService)
        {
        }

        public override string MethodName => "get_current_view_info";
        public override string DialogTitle => "Test: get_current_view_info";
        public override int MaxResultLength => 800;
    }
}
