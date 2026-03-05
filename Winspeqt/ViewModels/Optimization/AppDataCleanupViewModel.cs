using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Winspeqt.Helpers;
using Winspeqt.Models;

namespace Winspeqt.ViewModels.Optimization
{
    public class AppDataCleanupViewModel : INotifyPropertyChanged
    {
        // ── Private state ─────────────────────────────────────────────────────

        private readonly DispatcherQueue _dispatcher;
        private CancellationTokenSource? _cts;

        private bool _isScanning;
        private bool _isDeleting;
        private bool _hasResults;
        private bool _hasSelection;
        private bool _scanRan;
        private string _statusText = string.Empty;
        private string _selectionSummary = string.Empty;
        private string _spaceReclaimText = string.Empty;
        private double _deleteProgress;
        private InfoSeverity _infoSeverity = InfoSeverity.None;
        private string _infoMessage = string.Empty;
        private bool _infoVisible;
        private bool? _selectAllState = false;

        // ── Constructor ───────────────────────────────────────────────────────

        public AppDataCleanupViewModel()
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            Entries = [];
        }

        // ── Observable collections ────────────────────────────────────────────

        public ObservableCollection<OrphanedAppDataEntry> Entries { get; }

        // ── Bound properties ──────────────────────────────────────────────────

        public bool IsScanning
        {
            get => _isScanning;
            private set { _isScanning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanScan)); }
        }

        public bool IsDeleting
        {
            get => _isDeleting;
            private set { _isDeleting = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDelete)); }
        }

        public bool HasResults
        {
            get => _hasResults;
            private set { _hasResults = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowEmptyState)); }
        }

        public bool HasSelection
        {
            get => _hasSelection;
            private set { _hasSelection = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDelete)); }
        }

        public bool ScanRan
        {
            get => _scanRan;
            private set { _scanRan = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowEmptyState)); }
        }

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        public string SelectionSummary
        {
            get => _selectionSummary;
            private set { _selectionSummary = value; OnPropertyChanged(); }
        }

        public string SpaceReclaimText
        {
            get => _spaceReclaimText;
            private set { _spaceReclaimText = value; OnPropertyChanged(); }
        }

        public double DeleteProgress
        {
            get => _deleteProgress;
            private set { _deleteProgress = value; OnPropertyChanged(); }
        }

        public InfoSeverity InfoSeverity
        {
            get => _infoSeverity;
            private set { _infoSeverity = value; OnPropertyChanged(); }
        }

        public string InfoMessage
        {
            get => _infoMessage;
            private set { _infoMessage = value; OnPropertyChanged(); }
        }

        public bool InfoVisible
        {
            get => _infoVisible;
            private set { _infoVisible = value; OnPropertyChanged(); }
        }

        /// <summary>Tri-state for the select-all checkbox: true / false / null (indeterminate).</summary>
        public bool? SelectAllState
        {
            get => _selectAllState;
            set { _selectAllState = value; OnPropertyChanged(); }
        }

        // Derived
        public bool CanScan => !_isScanning && !_isDeleting;
        public bool CanDelete => _hasSelection && !_isDeleting && !_isScanning;
        public bool ShowEmptyState => !_hasResults && _scanRan && !_isScanning;

        // ── Commands ──────────────────────────────────────────────────────────

        public async Task ScanAsync()
        {
            if (!CanScan) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            IsScanning = true;
            ScanRan = false;
            HasResults = false;
            InfoVisible = false;
            Entries.Clear();

            var progress = new Progress<ScanProgress>(p =>
            {
                StatusText = $"Scanning {p.CurrentPath}…";
            });

            try
            {
                var found = await AppDataCleanupHelper.ScanAsync(progress, _cts.Token);

                foreach (var entry in found)
                {
                    entry.PropertyChanged += Entry_PropertyChanged;
                    Entries.Add(entry);
                }

                ScanRan = true;
                HasResults = found.Count > 0;

                if (found.Count == 0)
                {
                    ShowInfo(InfoSeverity.Success, "Scan complete — no orphaned folders found. Your system looks clean!");
                    SelectionSummary = string.Empty;
                    SpaceReclaimText = string.Empty;
                }
                else
                {
                    ShowInfo(InfoSeverity.Informational,
                        $"Found {found.Count} potentially orphaned folder{(found.Count != 1 ? "s" : "")}. " +
                        "Review carefully before deleting.");
                    RefreshSelectionState();
                }
            }
            catch (OperationCanceledException)
            {
                ScanRan = true;
                ShowInfo(InfoSeverity.Informational, "Scan cancelled.");
            }
            catch (Exception ex)
            {
                ScanRan = true;
                ShowInfo(InfoSeverity.Error, $"Scan failed: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
            }
        }

        public async Task DeleteSelectedAsync()
        {
            var toDelete = Entries.Where(e => e.IsSelected).ToList();
            if (toDelete.Count == 0) return;

            IsDeleting = true;
            InfoVisible = false;
            DeleteProgress = 0;

            var progress = new Progress<DeleteProgress>(p =>
            {
                StatusText = $"Deleting {p.CurrentName}…";
                DeleteProgress = p.Percent;
            });

            try
            {
                var failures = await AppDataCleanupHelper.DeleteFoldersAsync(toDelete, progress, _cts?.Token ?? default);
                var deleted = toDelete.Where(e => !failures.ContainsKey(e.FolderPath)).ToList();
                var reclaimed = deleted.Sum(e => e.SizeBytes);

                foreach (var entry in deleted)
                {
                    entry.PropertyChanged -= Entry_PropertyChanged;
                    Entries.Remove(entry);
                }

                HasResults = Entries.Count > 0;

                if (failures.Count == 0)
                    ShowInfo(InfoSeverity.Success,
                        $"Deleted {deleted.Count} folder{(deleted.Count != 1 ? "s" : "")} · Freed {FormatBytes(reclaimed)}");
                else
                    ShowInfo(InfoSeverity.Warning,
                        $"Deleted {deleted.Count} folder{(deleted.Count != 1 ? "s" : "")}, " +
                        $"{failures.Count} could not be deleted (in use or access denied).");

                RefreshSelectionState();
            }
            catch (Exception ex)
            {
                ShowInfo(InfoSeverity.Error, $"Deletion failed: {ex.Message}");
            }
            finally
            {
                IsDeleting = false;
            }
        }

        public void SetSelectAll(bool? checkState)
        {
            bool select = checkState == true;
            foreach (var entry in Entries)
                entry.IsSelected = select;
            // SelectAllState will be reconciled by RefreshSelectionState via entry PropertyChanged
        }

        public void CancelOperation()
        {
            _cts?.Cancel();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OrphanedAppDataEntry.IsSelected))
                RefreshSelectionState();
        }

        private void RefreshSelectionState()
        {
            var selected = Entries.Where(e => e.IsSelected).ToList();
            int selectedCount = selected.Count;
            long totalSize = selected.Sum(e => e.SizeBytes);

            HasSelection = selectedCount > 0;

            SelectionSummary = selectedCount > 0
                ? $"{selectedCount} of {Entries.Count} selected"
                : $"{Entries.Count} folder{(Entries.Count != 1 ? "s" : "")} found";

            SpaceReclaimText = selectedCount > 0
                ? $"— {FormatBytes(totalSize)} will be freed"
                : string.Empty;

            SelectAllState = selectedCount == 0 ? false
                           : selectedCount == Entries.Count ? true
                           : null; // indeterminate
        }

        private void ShowInfo(InfoSeverity severity, string message)
        {
            InfoSeverity = severity;
            InfoMessage = message;
            InfoVisible = true;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1_024) return $"{bytes / 1_024.0:F1} KB";
            return $"{bytes} B";
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Mirrors WinUI InfoBarSeverity without a direct dependency on the UI layer.</summary>
    public enum InfoSeverity { None, Informational, Success, Warning, Error }
}
