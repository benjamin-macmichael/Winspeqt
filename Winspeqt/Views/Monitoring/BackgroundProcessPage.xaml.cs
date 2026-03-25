using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Winspeqt.Models;
using Winspeqt.ViewModels.Monitoring;

namespace Winspeqt.Views.Monitoring
{
    public sealed partial class BackgroundProcessPage : Page
    {
        public BackgroundProcessViewModel ViewModel { get; }

        public BackgroundProcessPage()
        {
            this.InitializeComponent();
            ViewModel = new BackgroundProcessViewModel();
            UpdateViewToggleStyles();
        }

        // ── Navigation ───────────────────────────────────────────────────────────
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MonitoringDashboardPage));
        }

        // ── View toggle ──────────────────────────────────────────────────────────
        private void SetCardView_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsTableView = false;
            UpdateViewToggleStyles();
        }

        private void SetTableView_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsTableView = true;
            UpdateViewToggleStyles();
        }

        private void UpdateViewToggleStyles()
        {
            if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var accentObj) &&
                accentObj is Style accentStyle)
            {
                CardViewButton.Style = ViewModel.IsTableView ? null : accentStyle;
                TableViewButton.Style = ViewModel.IsTableView ? accentStyle : null;
            }
        }

        // ── Tree expand/collapse ─────────────────────────────────────────────────
        private void ExpandProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ProcessGroup group)
            {
                group.IsExpanded = !group.IsExpanded;

                if (group.IsExpanded)
                    ViewModel.ExpandedProcessIds.Add(group.RootProcess.ProcessId);
                else
                    ViewModel.ExpandedProcessIds.Remove(group.RootProcess.ProcessId);
            }
        }

        // ── End process ──────────────────────────────────────────────────────────
        private async void EndProcess_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var process = button?.Tag as ProcessInfo;
            if (process == null) return;

            if (process.IsProtected) return;

            var confirmDialog = new ContentDialog
            {
                Title = $"End {process.Description}?",
                Content = $"Are you sure you want to close '{process.Description}'?\n\n" +
                          $"Process: {process.ProcessName}\n" +
                          $"Memory: {process.MemoryUsageDisplay}\n\n" +
                          $"If this app has unsaved work, it will be lost.",
                PrimaryButtonText = "End Task",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var success = await ViewModel.EndProcessAsync(process);

                if (success)
                {
                    var successDialog = new ContentDialog
                    {
                        Title = "Process Ended",
                        Content = $"'{process.Description}' has been successfully ended.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
                else
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Failed to End Process",
                        Content = $"Could not end '{process.Description}'.\n\n" +
                                  $"This may happen if:\n" +
                                  $"• You don't have permission to end this process\n" +
                                  $"• The process has already ended\n" +
                                  $"• The process is protected by Windows",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
    }
}
