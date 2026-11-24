using System.Windows;
using System.Windows.Input;

namespace Tec.SampleApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnProcess(object sender, RoutedEventArgs e)
    {
        var wait = new WaitWindow { Owner = this };
        wait.Show();
        IsEnabled = false;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2)); // "proceso" largo
        }
        finally
        {
            wait.Close();
            IsEnabled = true;
        }

        new ResultWindow($"El cliente \"{CustomerName.Text}\" se procesó correctamente.") { Owner = this }.ShowDialog();
        StatusText.Text = $"Último proceso: {DateTime.Now:T}";
    }

    private async void OnLoadData(object sender, RoutedEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        await Task.Delay(TimeSpan.FromSeconds(2));
        DataList.ItemsSource = Enumerable.Range(1, 5).Select(i => $"Registro {i} — {DateTime.Now:T}").ToList();
        DataList.Visibility = Visibility.Visible;
        LoadingOverlay.Visibility = Visibility.Collapsed;
        StatusText.Text = "Datos cargados.";
    }

    private void OnSave(object sender, ExecutedRoutedEventArgs e) =>
        MessageBox.Show(this, "Datos guardados.", "Guardar", MessageBoxButton.OK, MessageBoxImage.Information);

    private void OnDelete(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "¿Eliminar el registro?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning);

    private void OnCustomerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) StatusText.Text = $"Buscando \"{CustomerName.Text}\"…";
    }
}
