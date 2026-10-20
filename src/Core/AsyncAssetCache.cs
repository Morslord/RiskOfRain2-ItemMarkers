using System;
using System.Collections.Generic;

namespace ItemMarkers.Core
{
    public interface IAssetRequest<TAsset> : IDisposable where TAsset : class
    {
        bool IsDone { get; }
        TAsset? Asset { get; }
        Exception? Error { get; }
    }

    // One owned request per asset key. Polling avoids callbacks that might update a recycled view.
    public sealed class AsyncAssetCache<TKey, TAsset> where TKey : notnull where TAsset : class
    {
        private sealed class Entry
        {
            public IAssetRequest<TAsset>? Request;
            public bool FailureReported;
        }

        private readonly Dictionary<TKey, Entry> entries = new Dictionary<TKey, Entry>();
        private readonly Func<TKey, IAssetRequest<TAsset>> load;
        private readonly Action<TKey, Exception> reportError;

        public AsyncAssetCache(Func<TKey, IAssetRequest<TAsset>> load, Action<TKey, Exception> reportError)
        { this.load = load; this.reportError = reportError; }

        public TAsset? Get(TKey key)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                entries.Add(key, entry);
                try { entry.Request = load(key); }
                catch (Exception ex) { ReportOnce(key, entry, ex); }
            }
            var request = entry.Request;
            if (request == null || !request.IsDone) return null;
            if (request.Error != null) { ReportOnce(key, entry, request.Error); return null; }
            var asset = request.Asset;
            if (asset == null) ReportOnce(key, entry, new InvalidOperationException("Asset load completed without an asset."));
            return asset;
        }

        public void Clear()
        {
            foreach (var pair in entries)
            {
                try { pair.Value.Request?.Dispose(); }
                catch (Exception ex) { reportError(pair.Key, ex); }
            }
            entries.Clear();
        }

        private void ReportOnce(TKey key, Entry entry, Exception error)
        {
            if (entry.FailureReported) return;
            entry.FailureReported = true;
            reportError(key, error);
        }
    }
}
