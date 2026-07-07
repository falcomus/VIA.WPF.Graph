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
    private string resultText = "P0-002 wird gestartet ...";

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
            ResultText = "P0-002 läuft ...";

            GraphvizRuntimeProbeResult result = await Task.Run(GraphvizRuntimeProbe.Run);
            ResultText = result.ToDisplayText();
        }
        catch (Exception exception)
        {
            ResultText = string.Join(
                Environment.NewLine,
                "P0-002 FAILED",
                string.Empty,
                "Die technische Graphviz-Prüfung konnte nicht abgeschlossen werden.",
                "Bitte den vollständigen Text aus diesem Fenster in den Chat kopieren.",
                string.Empty,
                exception);
        }
        finally
        {
            IsVerificationRunning = false;
        }
    }
}
