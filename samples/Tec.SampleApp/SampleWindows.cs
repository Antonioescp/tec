using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tec.SampleApp;

/// <summary>Ventana "espere" sin bordes y transparente (ventana en capas).</summary>
public sealed class WaitWindow : Window
{
    public WaitWindow()
    {
        Title = "Espere…";
        Width = 260;
        Height = 110;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Procesando, espere por favor…", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 12) },
                    new ProgressBar { IsIndeterminate = true, Height = 14 },
                },
            },
        };
    }
}

public sealed class ResultWindow : Window
{
    public ResultWindow(string message)
    {
        Title = "Resultado";
        Width = 360;
        Height = 160;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var ok = new Button { Content = "Aceptar", Padding = new Thickness(20, 4, 20, 4), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
        ok.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) },
                ok,
            },
        };
    }
}
