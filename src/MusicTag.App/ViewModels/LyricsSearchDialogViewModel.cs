using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicTag.Core.Services;

namespace MusicTag.App.ViewModels;

/// <summary>One row in the popup's live result list — see
/// <see cref="LyricsSearchDialogViewModel.OnFileResult"/>.</summary>
public sealed record LyricsSearchResultItem(string FileName, string Outcome);

/// <summary>
/// Backs the "Search Lyrics" progress popup (MainWindowViewModel.SearchLyrics /
/// IDialogService.ShowLyricsSearchDialog). Runs the actual <see cref="ILyricsSearchService.SearchAsync"/>
/// call itself — rather than the view just displaying a result handed to it — so it can report
/// live per-file progress into <see cref="Results"/> (newest first) and the Current/Total
/// progress bar as LyricsSearchService's parallel workers each finish a file. The dialog is
/// shown modally (ShowDialog), but WPF's nested dispatcher frame keeps this class's async
/// continuations running on the UI thread exactly like any other progress-dialog pattern.
/// </summary>
public sealed partial class LyricsSearchDialogViewModel : ObservableObject
{
    private readonly ILyricsSearchService _lyricsSearchService;
    private readonly IReadOnlyList<string> _directories;
    private readonly CancellationTokenSource _cts = new();

    public LyricsSearchDialogViewModel(ILyricsSearchService lyricsSearchService, IReadOnlyList<string> directories)
    {
        _lyricsSearchService = lyricsSearchService;
        _directories = directories;
    }

    /// <summary>Newest-first so the user always sees the latest match without needing to
    /// scroll — files complete out of enumeration order since LyricsSearchService now runs
    /// them concurrently.</summary>
    public ObservableCollection<LyricsSearchResultItem> Results { get; } = new();

    [ObservableProperty]
    private int current;

    [ObservableProperty]
    private int total;

    /// <summary>True until the total file count is known (the directory walk happens before
    /// any per-file progress is reported) and forced back to false once the run ends, so a
    /// zero-file search doesn't leave the bar spinning forever.</summary>
    [ObservableProperty]
    private bool isIndeterminate = true;

    /// <summary>Guards the toolbar/window against re-entrancy and switches
    /// <see cref="PrimaryButtonText"/> between Cancel and Close.</summary>
    [ObservableProperty]
    private bool isRunning = true;

    [ObservableProperty]
    private string statusText = "Scanning for audio files…";

    [ObservableProperty]
    private string? summaryText;

    [ObservableProperty]
    private string primaryButtonText = "Cancel";

    public async Task RunAsync()
    {
        try
        {
            var progress = new Progress<LyricsFileResult>(OnFileResult);
            var result = await _lyricsSearchService.SearchAsync(_directories, progress, _cts.Token);

            StatusText = "Done";
            SummaryText =
                $"Downloaded: {result.Downloaded}    Already had lyrics: {result.AlreadyHadLyrics}    " +
                $"No match: {result.NoMatch}    Errors: {result.Errors}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
            SummaryText = $"Stopped after {Current} of {Total} files.";
        }
        finally
        {
            IsRunning = false;
            IsIndeterminate = false;
            PrimaryButtonText = "Close";
        }
    }

    /// <summary>Also invoked when the window is closed (via the titlebar X) while a search is
    /// still running, so the background work doesn't keep making LRCLib requests for a popup
    /// that's no longer visible.</summary>
    [RelayCommand]
    private void Cancel() => _cts.Cancel();

    private void OnFileResult(LyricsFileResult r)
    {
        Current = r.Current;
        Total = r.Total;
        IsIndeterminate = false;
        StatusText = $"Searching lyrics: {r.Current}/{r.Total}";
        Results.Insert(0, new LyricsSearchResultItem(r.FileName, Describe(r.Outcome)));
    }

    private static string Describe(LyricsFileOutcome outcome) => outcome switch
    {
        LyricsFileOutcome.Downloaded => "Downloaded",
        LyricsFileOutcome.AlreadyHadLyrics => "Already had lyrics",
        LyricsFileOutcome.NoMatch => "No match",
        LyricsFileOutcome.Error => "Error",
        _ => outcome.ToString(),
    };
}
