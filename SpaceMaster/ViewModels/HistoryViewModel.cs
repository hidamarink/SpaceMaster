using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpaceMaster.Models;
using SpaceMaster.Services;

namespace SpaceMaster.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly MigrationService _migrationService = new();
    private readonly SymbolicLinkService _symbolicLinkService = new();

    [ObservableProperty]
    private ObservableCollection<MigrationRecord> _records = [];

    [ObservableProperty]
    private ObservableCollection<string> _sourceDriveFilters = ["全部"];

    [ObservableProperty]
    private ObservableCollection<string> _targetDriveFilters = ["全部"];

    [ObservableProperty]
    private string _selectedSourceDrive = "全部";

    [ObservableProperty]
    private string _selectedTargetDrive = "全部";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreOrRemigrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRecordCommand))]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    [NotifyPropertyChangedFor(nameof(IsSelectedRestored))]
    private MigrationRecord? _selectedRecord;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreOrRemigrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRecordCommand))]
    private bool _isRestoring;

    [ObservableProperty]
    private double _restoreProgress;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public string ActionButtonText => SelectedRecord?.Status == MigrationStatus.Restored ? "重做选中项" : "还原选中项";
    public bool IsSelectedRestored => SelectedRecord?.Status == MigrationStatus.Restored;

    public HistoryViewModel()
    {
        try
        {
            LogService.Info("HistoryViewModel 初始化");
            LoadFilters();
            LoadRecordsInternal();
        }
        catch (Exception ex)
        {
            LogService.Error("HistoryViewModel 初始化失败", ex);
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    private void LoadFilters()
    {
        try
        {
            var sourceDrives = _migrationService.GetDistinctSourceDrives();
            var targetDrives = _migrationService.GetDistinctTargetDrives();

            SourceDriveFilters = new ObservableCollection<string>(["全部", .. sourceDrives]);
            TargetDriveFilters = new ObservableCollection<string>(["全部", .. targetDrives]);
        }
        catch (Exception ex)
        {
            LogService.Error("加载筛选器失败", ex);
        }
    }

    private void LoadRecordsInternal()
    {
        try
        {
            var sourceFilter = SelectedSourceDrive == "全部" ? null : SelectedSourceDrive;
            var targetFilter = SelectedTargetDrive == "全部" ? null : SelectedTargetDrive;

            var records = _migrationService.GetRecords(sourceFilter, targetFilter);

            // 检测每条记录的有效性
            foreach (var record in records)
            {
                record.IsValid = ValidateRecord(record);
            }

            Records = new ObservableCollection<MigrationRecord>(records);
            var invalidCount = records.Count(r => !r.IsValid);
            StatusMessage = invalidCount > 0
                ? $"共 {records.Count} 条记录，{invalidCount} 条无效"
                : $"共 {records.Count} 条记录";
        }
        catch (Exception ex)
        {
            LogService.Error("加载记录失败", ex);
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    private bool ValidateRecord(MigrationRecord record)
    {
        try
        {
            if (record.Status == MigrationStatus.Active)
            {
                // 已迁移状态：检查源路径是符号链接且目标路径存在
                var isSymLink = _symbolicLinkService.IsSymbolicLink(record.SourcePath);
                var targetExists = record.IsDirectory
                    ? Directory.Exists(record.TargetPath)
                    : File.Exists(record.TargetPath);
                return isSymLink && targetExists;
            }
            else
            {
                // 已还原状态：检查源路径存在（作为普通文件/目录）
                return record.IsDirectory
                    ? Directory.Exists(record.SourcePath)
                    : File.Exists(record.SourcePath);
            }
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private void LoadRecords() => LoadRecordsInternal();

    partial void OnSelectedSourceDriveChanged(string value) => LoadRecordsInternal();
    partial void OnSelectedTargetDriveChanged(string value) => LoadRecordsInternal();

    [RelayCommand(CanExecute = nameof(CanRestoreOrRemigrate))]
    private async Task RestoreOrRemigrateAsync()
    {
        if (SelectedRecord == null) return;

        IsRestoring = true;
        RestoreProgress = 0;

        var isRemigrate = SelectedRecord.Status == MigrationStatus.Restored;
        StatusMessage = isRemigrate ? "正在重做..." : "正在还原...";
        LogService.Info($"开始{(isRemigrate ? "重做" : "还原")}: {SelectedRecord.SourcePath}");

        var progressReporter = new Progress<FileOperationProgress>(p =>
        {
            RestoreProgress = p.Percentage;
        });

        try
        {
            var result = isRemigrate
                ? await _migrationService.RemigrateAsync(SelectedRecord.Id, progressReporter)
                : await _migrationService.RestoreAsync(SelectedRecord.Id, progressReporter);

            if (result.Success)
            {
                LogService.Info($"{(isRemigrate ? "重做" : "还原")}成功: {SelectedRecord.SourcePath}");
                StatusMessage = isRemigrate ? "重做成功！" : "还原成功！";
                LoadFilters();
                LoadRecordsInternal();
            }
            else
            {
                LogService.Warn($"{(isRemigrate ? "重做" : "还原")}失败: {result.ErrorMessage}");
                StatusMessage = $"{(isRemigrate ? "重做" : "还原")}失败: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"{(isRemigrate ? "重做" : "还原")}过程发生异常", ex);
            StatusMessage = $"发生错误: {ex.Message}";
        }
        finally
        {
            IsRestoring = false;
        }
    }

    private bool CanRestoreOrRemigrate() => SelectedRecord != null && !IsRestoring;

    [RelayCommand(CanExecute = nameof(CanDeleteRecord))]
    private void DeleteRecord()
    {
        if (SelectedRecord == null) return;

        try
        {
            _migrationService.DeleteRecord(SelectedRecord.Id);
            LogService.Info($"删除记录: {SelectedRecord.SourcePath}");
            StatusMessage = "记录已删除";
            LoadFilters();
            LoadRecordsInternal();
        }
        catch (Exception ex)
        {
            LogService.Error("删除记录失败", ex);
            StatusMessage = $"删除失败: {ex.Message}";
        }
    }

    private bool CanDeleteRecord() => SelectedRecord != null && !IsRestoring;
}
