using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeepBimMCPTools
{
    public abstract class ToolViewModelBase : INotifyPropertyChanged
    {
        private readonly IToolExecutionService _executionService;
        private string _resultText = string.Empty;
        private string _errorText = string.Empty;
        private bool _isSuccess;
        private bool _isExecuting;

        protected ToolViewModelBase(IToolExecutionService executionService)
        {
            _executionService = executionService;
        }

        public abstract string MethodName { get; }
        public abstract string DialogTitle { get; }
        public virtual int MaxResultLength => 800;
        public virtual string FailureTitle => $"{DialogTitle} failed";
        public string ResultText
        {
            get => _resultText;
            private set => SetProperty(ref _resultText, value);
        }

        public string ErrorText
        {
            get => _errorText;
            private set => SetProperty(ref _errorText, value);
        }

        public bool IsSuccess
        {
            get => _isSuccess;
            private set => SetProperty(ref _isSuccess, value);
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            private set => SetProperty(ref _isExecuting, value);
        }

        protected virtual JObject BuildParameters()
        {
            return new JObject();
        }

        protected virtual string FormatResult(object result)
        {
            return ToolResultFormatter.Format(result, MaxResultLength);
        }

        public bool Run(UIApplication uiApp)
        {
            IsExecuting = true;
            ErrorText = string.Empty;

            try
            {
                var result = _executionService.Execute(uiApp, MethodName, BuildParameters());
                ResultText = FormatResult(result);
                IsSuccess = true;
                return true;
            }
            catch (System.Exception ex)
            {
                ResultText = string.Empty;
                ErrorText = ex.Message;
                IsSuccess = false;
                return false;
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
