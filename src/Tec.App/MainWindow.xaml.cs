using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Tec.App.ViewModels;

namespace Tec.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _shutdownCompleted;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnEvidenceDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.OpenEvidenceCommand.CanExecute(null))
            _viewModel.OpenEvidenceCommand.Execute(null);
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        // If a session is running, stop it first so the final report is written.
        if (_shutdownCompleted || !_viewModel.IsRunning)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        await _viewModel.ShutdownAsync();
        _shutdownCompleted = true;
        Close();
    }
}
