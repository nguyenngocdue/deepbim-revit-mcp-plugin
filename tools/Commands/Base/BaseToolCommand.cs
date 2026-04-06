using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace DeepBimMCPTools
{
    public abstract class BaseToolCommand<TViewModel> : IExternalCommand
        where TViewModel : ToolViewModelBase
    {
        protected virtual ICommandGateway CreateGateway()
        {
            return new DirectCommandGateway();
        }

        protected virtual IToolExecutionService CreateExecutionService(ICommandGateway gateway)
        {
            return new ToolExecutionService(gateway);
        }

        protected virtual IToolResultView CreateView()
        {
            return new TaskDialogToolResultView();
        }

        protected abstract TViewModel CreateViewModel(IToolExecutionService executionService);

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            IToolResultView view = null;
            TViewModel viewModel = null;
            try
            {
                view = CreateView();
                var gateway = CreateGateway();
                var executionService = CreateExecutionService(gateway);
                viewModel = CreateViewModel(executionService);

                if (viewModel.Run(commandData.Application))
                {
                    view.ShowSuccess(viewModel.DialogTitle, viewModel.ResultText);
                    return Result.Succeeded;
                }

                message = viewModel.ErrorText;
                view.ShowError(viewModel.FailureTitle, viewModel.ErrorText);
                return Result.Failed;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                string title = viewModel?.FailureTitle ?? $"{typeof(TViewModel).Name} failed";
                if (view != null)
                    view.ShowError(title, ex.Message);
                else
                    TaskDialog.Show(title, ex.Message);
                return Result.Failed;
            }
        }
    }
}
