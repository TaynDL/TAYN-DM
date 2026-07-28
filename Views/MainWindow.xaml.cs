using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TaynDM;

namespace TaynDM.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;

    public MainWindow()
    {
        InitializeComponent();

        string? forcedLanguage = Environment.GetEnvironmentVariable("TAYN_LANGUAGE");

        viewModel = new MainViewModel(AppLogger.Instance, Dispatcher);

        if (forcedLanguage is "fa" or "en")
            viewModel.Settings.Language = forcedLanguage;

        DataContext = viewModel;
        viewModel.Initialize();

        Loaded += MainWindow_Loaded;
        Closing += (_, _) => viewModel.Shutdown();
    }

    // ── Responsive layout ────────────────────────────────────────────────

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width < 1000)
        {
            DetailsPanel.Visibility = Visibility.Collapsed;
            MainShell.ColumnDefinitions[2].Width = new GridLength(0);
        }
        else
        {
            DetailsPanel.Visibility = Visibility.Visible;
            MainShell.ColumnDefinitions[2].Width = new GridLength(292);
        }
    }

    // ── Window chrome ────────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        else
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── Language ─────────────────────────────────────────────────────

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        viewModel.ToggleLanguage();
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        bool english = viewModel.Settings.Language == "en";
        LocalizationService.Apply(this, english);

        FlowDirection = FlowDirection.RightToLeft;
        MainShell.FlowDirection = FlowDirection.LeftToRight;
        SidebarPanel.FlowDirection = english
            ? FlowDirection.LeftToRight
            : FlowDirection.RightToLeft;
        CenterPanel.FlowDirection = english
            ? FlowDirection.LeftToRight
            : FlowDirection.RightToLeft;
        DetailsPanel.FlowDirection = FlowDirection.LeftToRight;

        SearchHint.Text = english ? "Search downloads..." : "\u062C\u0633\u062A\u200C\u0648\u062C\u0648...";
        NoSelectionTitle.Text = english
            ? "Select a download"
            : "\u06CC\u06A9 \u062F\u0627\u0646\u0644\u0648\u062F \u0631\u0627 \u0627\u0646\u062A\u062E\u0627\u0628 \u06A9\u0646\u06CC\u062F";
        NoSelectionHint.Text = english
            ? "File details will appear here"
            : "\u062C\u0632\u0626\u06CC\u0627\u062A \u0641\u0627\u06CC\u0644 \u0627\u06CC\u0646\u062C\u0627 \u0646\u0627\u0645\u0627\u06CC\u0634 \u062F\u0627\u062F\u0647 \u0645\u06CC\u200C\u0634\u0648\u062F";
        LanguageButton.Content = english ? "FA" : "EN";

        foreach (var item in viewModel.Downloads)
            item.RefreshLocalization();
    }

    // ── Animation ────────────────────────────────────────────────────

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Trigger initial responsive state
        MainWindow_SizeChanged(this, new SizeChangedEventArgs(SizeChangedEvent, this, this));

        CenterPanel.Opacity = 0;
        var move = new TranslateTransform(0, 14);
        CenterPanel.RenderTransform = move;

        CenterPanel.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                Duration = TimeSpan.FromMilliseconds(260)
            });

        move.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    // ── Settings ─────────────────────────────────────────────────────

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (new SettingsWindow(viewModel.Settings) { Owner = this }.ShowDialog() == true)
            viewModel.RefreshSettings();
    }

    // ── Download search ───────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => viewModel.SearchText = SearchBox.Text;

    // ── Download listing ─────────────────────────────────────────────────

    private void DownloadsList_SelectionChanged(object sender, SelectionChangedEventArgs<DownloadItem> e)
        => viewModel.SelectedDownload = e.AddedItems.Cast<DownloadItem>().FirstOrDefault();

    // ── External link injection (from App.xaml.cs) ───────────────────

    public void AddExternalLink(string url) => viewModel.AddExternalLink(url);
}
