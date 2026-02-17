using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Winspeqt.ViewModels.Monitoring;
using Winspeqt.ViewModels.Optimization;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Winspeqt.Views.Optimization
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LargeFileFinder : Page
    {
        public LargeFileFinderViewModel ViewModel { get; }
        public LargeFileFinder()
        {
            InitializeComponent();
            ViewModel = new LargeFileFinderViewModel();
            DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadAsync();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void Folder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string path && path != "")
            {
                _ = ViewModel.RetrieveFolderItems(path);
            }
        }

        private void BreadCrumb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int index)
            {
                _ = ViewModel.RetrieveFolderItems(ViewModel.PathItems[index].Path);
                ViewModel.ResetBreadCrumb(index);
            }
        }

        private void ViewFileExplorer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = ViewModel.PathItems[ViewModel.PathItems.Count - 1].Path ?? string.Empty,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening File Explorer for path {ViewModel.PathItems[ViewModel.PathItems.Count - 1].Path}: {ex.Message}");
            }
        }
    }
}
