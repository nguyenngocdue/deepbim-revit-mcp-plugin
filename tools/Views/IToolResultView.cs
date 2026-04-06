namespace DeepBimMCPTools
{
    public interface IToolResultView
    {
        void ShowSuccess(string title, string message);
        void ShowError(string title, string message);
    }
}
