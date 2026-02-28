using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Linq;
using Winspeqt.Models;
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
        /// <param name="e">Navigation event data supplied by the frame.</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadAsync();
        }

        /// <summary>
        /// Navigates back to the previous page when possible.
        /// </summary>
        /// <param name="sender">The back button control.</param>
        /// <param name="e">Routed event data.</param>
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
        /// <param name="sender">The folder button that carries the folder path as its command parameter.</param>
        /// <param name="e">Routed event data.</param>
        private async void Folder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string path && path != "")
            {
                if (ViewModel.ActiveNode.Children == null) return;
                FileSearchItem newNode = ViewModel.ActiveNode.Children.First(c => c.FilePath == path);
                await ViewModel.ChangeActiveNode(newNode);
            }
        }

        /// <summary>
        /// Resets the breadcrumb to a given index and loads that folder.
        /// </summary>
        /// <param name="sender">The breadcrumb button whose command parameter is the target index.</param>
        /// <param name="e">Routed event data.</param>
        private async void BreadCrumb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int index)
            {
                FileSearchItem newNode = ViewModel.ActiveNode;

                for (int progenitor = ViewModel.PathItems.Count - index - 1; progenitor > 0; progenitor--)
                {
                    newNode = newNode.Parent;
                }
                await ViewModel.ChangeActiveNode(newNode);
                ViewModel.ResetBreadCrumb(index);
            }
        }

        /// <summary>
        /// Opens File Explorer at the current breadcrumb path.
        /// </summary>
        /// <param name="sender">The button that triggers the action.</param>
        /// <param name="e">Routed event data.</param>
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
