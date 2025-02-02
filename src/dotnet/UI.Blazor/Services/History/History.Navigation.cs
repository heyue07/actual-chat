namespace ActualChat.UI.Blazor.Services;

public partial class History
{
    public Task WhenNavigationCompleted(CancellationToken cancellationToken = default)
        => NavigationQueue.WhenAllEntriesCompleted(cancellationToken);

    public async Task WhenNavigationCompletedWithDefaultTimeout()
    {
        var cts = new CancellationTokenSource(AwaitNavigationDuration);
        try {
            await WhenNavigationCompleted(cts.Token).ConfigureAwait(true);
        }
        catch (Exception e) {
            if (e is TimeoutException || e is OperationCanceledException)
                Log.LogDebug(e, "WhenNavigationCompletedWithDefaultTimeout exceeded timeout");
            else
                throw;
        }
        finally {
            cts.CancelAndDisposeSilently();
        }
    }

    [JSInvokable]
    public async Task NavigateTo(string uri, bool mustReplace = false, bool force = false, bool addInFront = false)
    {
        await WhenNavigationCompletedWithDefaultTimeout().ConfigureAwait(true);

        var fixedUri = new LocalUrl(uri).Value;
        if (!OrdinalEquals(uri, fixedUri)) {
            Log.LogWarning("NavigateTo: {Uri} is fixed to {FixedUri}", uri, fixedUri);
            uri = fixedUri;
        }

        var title = $"NavigateTo: {(mustReplace ? "*>" : "->")} {uri}{(force ? " + force" : "")}";
        var entry = NavigationQueue.Enqueue(addInFront, title, () => {
            if (!force && OrdinalEquals(uri, _uri)) {
                DebugLog?.LogDebug("{Entry} - skipped (same URI + no force option)", title);
                return null;
            }

            DebugLog?.LogDebug("{Entry}", title);
            var itemId = mustReplace ? _currentItem.Id : NewItemId();
            Nav.NavigateTo(uri, new NavigationOptions() {
                ForceLoad = force,
                ReplaceHistoryEntry = mustReplace,
                HistoryEntryState = ItemIdFormatter.Format(itemId),
            });
            return itemId;
        });
        await entry.WhenCompleted.ConfigureAwait(false);
    }

    public ValueTask ForceReload(string eventName, string url, bool mustReplace = true)
    {
        Log.LogWarning("ForceReload on {EventName}: {Url} (mustReplace = {MustReplace})", eventName, url, mustReplace);
        // return JS.InvokeVoidAsync($"{BlazorUICoreModule.ImportName}.History.forceReload", url, mustReplace, NewItemId());
        var method = mustReplace ? "replace" : "assign";
        return JS.InvokeVoidAsync($"window.location.{method}", url);
    }

    // Internal and private methods

    private Task NavigateBack(bool addInFront = false)
    {
        DebugLog?.LogDebug("NavigateBack: {AddInFront}", addInFront);
        return NavigationQueue.Enqueue(addInFront,
                "NavigateBack",
                () => {
                    _ = JS.EvalVoid("window.history.back()");
                    return 0; // "Fits" any itemId
                })
            .WhenCompleted;
    }

    private Task AddHistoryEntry(HistoryItem item, bool addInFront = false)
        => NavigationQueue.Enqueue(addInFront,  $"AddHistoryEntry: {item}", () => {
            Nav.NavigateTo(item.Uri,
                new NavigationOptions {
                    ForceLoad = false,
                    ReplaceHistoryEntry = false,
                    HistoryEntryState = ItemIdFormatter.Format(item.Id),
                });
            return item.Id;
        }).WhenCompleted;

    private Task ReplaceHistoryEntry(HistoryItem item, bool addInFront = false)
        => NavigationQueue.Enqueue(addInFront, $"ReplaceHistoryEntry: {item}", () => {
            Nav.NavigateTo(item.Uri,
                new NavigationOptions {
                    ForceLoad = false,
                    ReplaceHistoryEntry = true,
                    HistoryEntryState = ItemIdFormatter.Format(item.Id),
                });
            return item.Id;
        }).WhenCompleted;

    /*
    private void AddNavigationHistoryEntry(long itemId)
    {
        var sItemId = Hub.ItemIdFormatter.Format(itemId);
        Hub.JS.EvalVoid($"window.history.pushState('{sItemId}', '')");
    }

    private void ReplaceNavigationHistoryEntry(long itemId)
    {
        var sItemId = Hub.ItemIdFormatter.Format(itemId);
        Hub.JS.EvalVoid($"window.history.replaceState('{sItemId}', '')");
    }
    */
}
