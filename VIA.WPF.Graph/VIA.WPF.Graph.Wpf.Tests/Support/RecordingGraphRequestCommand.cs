using System.Windows.Input;
using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Wpf.Tests.Support;

internal sealed class RecordingGraphRequestCommand : ICommand
{
    private readonly List<GraphRequest> requests = [];

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public IReadOnlyList<GraphRequest> Requests => requests;

    public bool CanExecute(object? parameter)
    {
        return parameter is GraphRequest;
    }

    public void Execute(object? parameter)
    {
        if (parameter is GraphRequest request)
        {
            requests.Add(request);
        }
    }
}
