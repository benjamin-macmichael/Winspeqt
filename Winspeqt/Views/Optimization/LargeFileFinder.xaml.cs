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
    /// Page that displays the Large File Finder UI and wires user interactions to the view model.
    /// </summary>
    public sealed partial class LargeFileFinder : Page
    {
        /// <summary>
        /// View model providing data and commands for the page.
        /// </summary>
        public LargeFileFinderViewModel ViewModel { get; }
        /// <summary>
        /// Initializes the page and sets the data context.
        /// </summary>
        public LargeFileFinder()
        {
            InitializeComponent();
            ViewModel = new LargeFileFinderViewModel();
            DataContext = ViewModel;
        }

        /// <summary>
        /// Loads the initial folder contents when the page is navigated to.
        /// </summary>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadAsync();
        }

        /// <summary>
        /// Navigates back to the previous page when possible.
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        /// <summary>
        /// Navigates into a folder item when clicked.
        /// </summary>
        private void Folder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string path && path != "")
            {
                _ = ViewModel.RetrieveFolderItems(path);
            }
        }

        /// <summary>
        /// Resets the breadcrumb to a given index and loads that folder.
        /// </summary>
        private void BreadCrumb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int index)
            {
                _ = ViewModel.RetrieveFolderItems(ViewModel.PathItems[index].Path);
                ViewModel.ResetBreadCrumb(index);
            }
        }

        /// <summary>
        /// Opens File Explorer at the current breadcrumb path.
        /// </summary>
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
