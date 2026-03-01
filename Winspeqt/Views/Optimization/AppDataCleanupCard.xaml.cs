using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Winspeqt.ViewModels.Optimization;

namespace Winspeqt.Views.Optimization
{
    public sealed partial class AppDataCleanupCard : Page
    {
        // ── ViewModel ─────────────────────────────────────────────────────────

        public AppDataCleanupViewModel ViewModel { get; } = new();

        // ── Constructor ───────────────────────────────────────────────────────

        public AppDataCleanupCard()
        {
            InitializeComponent();

            // Wire the list to the ViewModel's observable collection
            OrphanList.ItemsSource = ViewModel.Entries;

            // Keep UI panels in sync with ViewModel state changes
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.ScanAsync();
            SyncPanelVisibility();
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SetSelectAll(SelectAllCheckBox.IsChecked);
            SelectAllCheckBox.IsChecked = ViewModel.SelectAllState;
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Permanently Delete Folders?",
                Content = BuildConfirmationMessage(),
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            await ViewModel.DeleteSelectedAsync();
            SyncPanelVisibility();
        }

        // ── ViewModel → panel visibility sync ────────────────────────────────

        private void ViewModel_PropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(AppDataCleanupViewModel.IsScanning):
                case nameof(AppDataCleanupViewModel.IsDeleting):
                case nameof(AppDataCleanupViewModel.HasResults):
                case nameof(AppDataCleanupViewModel.ScanRan):
                case nameof(AppDataCleanupViewModel.SelectAllState):
                case nameof(AppDataCleanupViewModel.HasSelection):
                case nameof(AppDataCleanupViewModel.InfoVisible):
                case nameof(AppDataCleanupViewModel.InfoMessage):
                case nameof(AppDataCleanupViewModel.InfoSeverity):
                case nameof(AppDataCleanupViewModel.StatusText):
                case nameof(AppDataCleanupViewModel.DeleteProgress):
                case nameof(AppDataCleanupViewModel.SelectionSummary):
                case nameof(AppDataCleanupViewModel.SpaceReclaimText):
                    DispatcherQueue.TryEnqueue(SyncPanelVisibility);
                    break;
            }
        }

        private void SyncPanelVisibility()
        {
            bool scanning = ViewModel.IsScanning;
            bool deleting = ViewModel.IsDeleting;
            bool hasResult = ViewModel.HasResults;
            bool scanRan = ViewModel.ScanRan;

            // State panels
            ScanningPanel.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
            DeletingPanel.Visibility = deleting ? Visibility.Visible : Visibility.Collapsed;
            PreScanState.Visibility = (!scanRan && !scanning) ? Visibility.Visible : Visibility.Collapsed;
            CleanState.Visibility = (scanRan && !hasResult && !scanning) ? Visibility.Visible : Visibility.Collapsed;
            ResultsPanel.Visibility = hasResult ? Visibility.Visible : Visibility.Collapsed;
            FooterBar.Visibility = hasResult ? Visibility.Visible : Visibility.Collapsed;

            // Status text
            ScanStatusText.Text = ViewModel.StatusText;
            DeleteStatusText.Text = ViewModel.StatusText;
            DeleteProgressBar.Value = ViewModel.DeleteProgress;

            // Selection summary
            SelectionSummaryText.Text = ViewModel.SelectionSummary;
            SpaceReclaimText.Text = ViewModel.SpaceReclaimText;
            DeleteButton.IsEnabled = ViewModel.HasSelection;

            // Select-all checkbox tri-state
            SelectAllCheckBox.IsChecked = ViewModel.SelectAllState;

            // InfoBar
            ResultInfoBar.Severity = ViewModel.InfoSeverity switch
            {
                InfoSeverity.Success => InfoBarSeverity.Success,
                InfoSeverity.Warning => InfoBarSeverity.Warning,
                InfoSeverity.Error => InfoBarSeverity.Error,
                InfoSeverity.Informational => InfoBarSeverity.Informational,
                _ => InfoBarSeverity.Informational,
            };
            ResultInfoBar.Message = ViewModel.InfoMessage;
            ResultInfoBar.IsOpen = ViewModel.InfoVisible;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string BuildConfirmationMessage()
        {
            int count = 0;
            long totalBytes = 0;
            foreach (var entry in ViewModel.Entries)
            {
                if (entry.IsSelected) { count++; totalBytes += entry.SizeBytes; }
            }

            string size = totalBytes >= 1_073_741_824 ? $"{totalBytes / 1_073_741_824.0:F1} GB"
                        : totalBytes >= 1_048_576 ? $"{totalBytes / 1_048_576.0:F1} MB"
                        : totalBytes >= 1_024 ? $"{totalBytes / 1_024.0:F1} KB"
                        : $"{totalBytes} B";

            return $"You are about to permanently delete {count} folder{(count != 1 ? "s" : "")} ({size}).\n\n" +
                   "This cannot be undone. Make sure these applications are truly uninstalled before proceeding.";
        }
    }
}
