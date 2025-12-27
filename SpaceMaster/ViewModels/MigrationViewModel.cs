using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpaceMaster.Models;
using SpaceMaster.Services;

namespace SpaceMaster.ViewModels;

public partial class MigrationViewModel : ObservableObject
{
    private readonly MigrationService _migrationService = new();
    private readonly DriveService _driveService = new();
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _sourceInfo = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MigrateCommand))]
    private DriveInfoModel? _selectedTargetDrive;

    [ObservableProperty]
    private ObservableCollection<DriveInfoModel> _availableDrives = [];

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MigrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelMigrationCommand))]
    private bool _isMigrating;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MigrateCommand))]
    private bool _hasSource;

    public void SetSourcePath(string path)
    {
        SourcePath = path;
        HasSource = !string.IsNullOrEmpty(path);

        if (HasSource)
        {
            var fileOp = new FileOperationService();
            var size = fileOp.CalculateSize(path);
            var isDir = Directory.Exists(path);
            SourceInfo = $"{(isDir ? "文件夹" : "文件")} - {FormatSize(size)}";

            // 更新可用磁盘列表
            var sourceDrive = DriveService.GetDriveLetter(path);
            AvailableDrives = new ObservableCollection<DriveInfoModel>(
                _driveService.GetOtherDrives(sourceDrive));

            if (AvailableDrives.Count > 0)
                SelectedTargetDrive = AvailableDrives[0];
        }
        else
        {
            SourceInfo = string.Empty;
            AvailableDrives.Clear();
        }

        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void ClearSource()
    {
        SetSourcePath(string.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanMigrate))]
    private async Task MigrateAsync()
    {
        if (SelectedTargetDrive == null) return;

        IsMigrating = true;
        Progress = 0;
        StatusMessage = string.Empty;
        _cancellationTokenSource = new CancellationTokenSource();

        var progressReporter = new Progress<FileOperationProgress>(p =>
        {
            Progress = p.Percentage;
            ProgressText = $"{FormatSize(p.CopiedBytes)} / {FormatSize(p.TotalBytes)}";
            CurrentFile = p.CurrentFile;
        });

        try
        {
            var result = await _migrationService.MigrateAsync(
                SourcePath,
                SelectedTargetDrive.Name[..1],
                progressReporter,
                _cancellationTokenSource.Token);

            if (result.Success)
            {
                StatusMessage = $"迁移成功！已迁移到 {result.Record!.TargetPath}";
                SetSourcePath(string.Empty);
            }
            else
            {
                StatusMessage = $"迁移失败: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"发生错误: {ex.Message}";
        }
        finally
        {
            IsMigrating = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private bool CanMigrate() => HasSource && SelectedTargetDrive != null && !IsMigrating;

    [RelayCommand(CanExecute = nameof(IsMigrating))]
    private void CancelMigration()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "正在取消...";
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
