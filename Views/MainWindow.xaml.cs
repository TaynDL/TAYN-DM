using System.Windows;
using System.Windows.Input;
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

        SearchHint.Text = english ? "Search downloads..." : "جست‌وجو...";
        NoSelectionTitle.Text = english
            ? "Select a download"
            : "یک دانلود را انتخاب کنید";
        NoSelectionHint.Text = english
            ? "File details will appear here"
            : "جزئیات فایل اینجا نمایش داده می‌شود";
        LanguageButton.Content = english ? "FA" : "EN";

        foreach (var item in viewModel.Downloads)
            item.RefreshLocalization();
    }

    // ── Animation ────────────────────────────────────────────────────

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CenterPanel.Opacity = 0;
        var move = new TranslateTransform(0, 14);
        CenterPanel.RenderTransform = move;

        CenterPanel.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
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
        {
            ApplyTheme();
            viewModel.RefreshEngineIfIdle();
        }
    }

    private void ApplyTheme() { }

    // ── Search / Filter ──────────────────────────────────────────────

    private void Search_Changed(object sender, RoutedEventArgs e)
    {
        viewModel.ApplySearchFilter(SearchBox?.Text, FilterBox?.SelectedItem);
    }

    // ── Download actions ─────────────────────────────────────────────

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddDownloadWindow(viewModel.Settings.DefaultFolder)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            viewModel.AddDownload(dialog.Folder, dialog.DownloadUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "خطا: " + ex.Message,
                "TaynDM",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
        => viewModel.EnqueueSelected(SelectedDownload);

    private void Pause_Click(object sender, RoutedEventArgs e)
        => viewModel.PauseSelected(SelectedDownload);

    private void Remove_Click(object sender, RoutedEventArgs e)
        => viewModel.RemoveSelected(SelectedDownload, this);

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
        => viewModel.OpenFolder(SelectedDownload);

    private void PriorityUp_Click(object sender, RoutedEventArgs e)
        => viewModel.ChangePriority(SelectedDownload, 1);

    private void PriorityDown_Click(object sender, RoutedEventArgs e)
        => viewModel.ChangePriority(SelectedDownload, -1);

    // ── External link injection (from App.xaml.cs) ───────────────────

    /// <summary>
    /// Pass-through for App.xaml.cs to inject external links.
    /// </summary>
    public void AddExternalLink(string url) => viewModel.AddExternalLink(url);
}
