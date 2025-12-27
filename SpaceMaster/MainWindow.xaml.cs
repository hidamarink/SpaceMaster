using System.Windows;
using System.Windows.Controls;
using SpaceMaster.Helpers;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SpaceMaster;

public partial class MainWindow : FluentWindow
{
    private bool _isDarkTheme;
    private bool _isCollapsed;
    private bool _isAnimating;
    private System.Windows.Controls.Button? _selectedNavButton;
    private const double ExpandedWidth = 200;
    private const double CollapsedWidth = 48;

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) => DragDropHelper.EnableDragDrop(this);

        Loaded += (_, _) =>
        {
            MainFrame.Navigate(new Views.Pages.MigrationPage());
            UpdateThemeIcon();
            TxtVersion.Text = AppInfo.Version;
            _selectedNavButton = BtnMigration;
        };
    }

    private void SelectNavButton(System.Windows.Controls.Button button)
    {
        // 取消之前的选中状态
        if (_selectedNavButton != null)
        {
            _selectedNavButton.Tag = null;
        }

        // 设置新的选中状态
        button.Tag = "Selected";
        _selectedNavButton = button;
    }

    private void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isAnimating) return;

        _isCollapsed = !_isCollapsed;
        AnimateSidebar(_isCollapsed ? CollapsedWidth : ExpandedWidth);
    }

    private void AnimateSidebar(double targetWidth)
    {
        _isAnimating = true;
        var currentWidth = SidebarColumn.Width.Value;

        // 折叠时立即隐藏文本
        if (_isCollapsed)
        {
            SetTextVisibility(Visibility.Collapsed);
        }

        // 宽度动画
        var steps = 15;
        var stepDuration = 200.0 / steps;
        var step = 0;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(stepDuration)
        };

        timer.Tick += (_, _) =>
        {
            step++;
            var progress = (double)step / steps;
            var easedProgress = 1 - Math.Pow(1 - progress, 3);
            var newWidth = currentWidth + (targetWidth - currentWidth) * easedProgress;
            SidebarColumn.Width = new GridLength(newWidth);

            if (step >= steps)
            {
                timer.Stop();
                SidebarColumn.Width = new GridLength(targetWidth);
                _isAnimating = false;

                // 展开时延迟显示文本
                if (!_isCollapsed)
                {
                    SetTextVisibility(Visibility.Visible);
                }
            }
        };

        timer.Start();
    }

    private void SetTextVisibility(Visibility visibility)
    {
        TxtMigration.Visibility = visibility;
        TxtHistory.Visibility = visibility;
        TxtTheme.Visibility = visibility;
        TxtVersion.Visibility = visibility;
    }

    private void BtnMigration_Click(object sender, RoutedEventArgs e)
    {
        SelectNavButton(BtnMigration);
        MainFrame.Navigate(new Views.Pages.MigrationPage());
    }

    private void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        SelectNavButton(BtnHistory);
        MainFrame.Navigate(new Views.Pages.HistoryPage());
    }

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;

        ApplicationThemeManager.Apply(
            _isDarkTheme ? ApplicationTheme.Dark : ApplicationTheme.Light,
            WindowBackdropType.Mica
        );

        UpdateThemeIcon();
    }

    private void UpdateThemeIcon()
    {
        IconTheme.Symbol = _isDarkTheme ? SymbolRegular.WeatherMoon24 : SymbolRegular.WeatherSunny24;
        TxtTheme.Text = _isDarkTheme ? "浅色模式" : "深色模式";
    }
}
