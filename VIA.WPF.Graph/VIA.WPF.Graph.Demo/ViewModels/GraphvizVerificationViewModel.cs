using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VIA.WPF.Graph.Graphviz.Verification;

namespace VIA.WPF.Graph.Demo.ViewModels;

public partial class GraphvizVerificationViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunVerificationCommand))]
    private bool isVerificationRunning;

    [ObservableProperty]
    private string resultText = "P0-003 wird gestartet ...";

    [ObservableProperty]
    private string technicalDetails = string.Empty;

    [ObservableProperty]
    private GraphvizReferenceLayoutViewModel? topToBottomLayout;

    [ObservableProperty]
    private GraphvizReferenceLayoutViewModel? leftToRightLayout;

    public GraphvizVerificationViewModel()
    {
        RunVerificationCommand.Execute(null);
    }

    private bool CanRunVerification()
    {
        return !IsVerificationRunning;
    }

    [RelayCommand(CanExecute = nameof(CanRunVerification))]
    private async Task RunVerificationAsync()
    {
        try
        {
            IsVerificationRunning = true;
            ResultText = "P0-003 läuft ...";
            TechnicalDetails = string.Empty;
            TopToBottomLayout = null;
            LeftToRightLayout = null;

            GraphvizRuntimeProbeResult result = await Task.Run(GraphvizRuntimeProbe.Run);

            TopToBottomLayout = GraphvizReferenceLayoutViewModel.Create(result.TopToBottom);
            LeftToRightLayout = GraphvizReferenceLayoutViewModel.Create(result.LeftToRight);
            TechnicalDetails = result.ToDisplayText();
            ResultText = "P0-003 PASSED – beide Graphviz-Layouts wurden als WPF-Testbild erzeugt.";
        }
        catch (Exception exception)
        {
            ResultText = "P0-003 FAILED – die technische Referenz konnte nicht erzeugt werden.";
            TechnicalDetails = exception.ToString();
        }
        finally
        {
            IsVerificationRunning = false;
        }
    }
}
