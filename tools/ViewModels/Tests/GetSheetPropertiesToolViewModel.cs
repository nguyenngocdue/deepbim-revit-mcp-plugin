namespace DeepBimMCPTools
{
    public sealed class GetSheetPropertiesToolViewModel : ToolViewModelBase
    {
        public GetSheetPropertiesToolViewModel(IToolExecutionService executionService) : base(executionService)
        {
        }

        public override string MethodName => "get_sheet_exportable_properties";
        public override string DialogTitle => "Test: get_sheet_exportable_properties";
        public override int MaxResultLength => 800;
    }
}
