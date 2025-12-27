using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SpaceMaster.ViewModels;

namespace SpaceMaster.Views.Pages;

public partial class MigrationPage : Page
{
    private MigrationViewModel ViewModel => (MigrationViewModel)DataContext;

    public MigrationPage()
    {
        InitializeComponent();
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length > 0)
            {
                ViewModel.SetSourcePath(files[0]);
            }
        }
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择要迁移的文件夹",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.SetSourcePath(dialog.FolderName);
        }
    }
}
