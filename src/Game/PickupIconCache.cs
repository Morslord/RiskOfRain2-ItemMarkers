using System;
using BepInEx.Logging;
using ItemMarkers.Core;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemMarkers
{
    internal sealed class PickupIconCache
    {
        private readonly AsyncAssetCache<object, Sprite> sprites;
        private readonly ManualLogSource log;
        private int failures;

        public PickupIconCache(ManualLogSource log)
        {
            this.log = log;
            sprites = new AsyncAssetCache<object, Sprite>(key => new SpriteRequest(key), ReportFailure);
        }

        public Sprite? Resolve(PickupDef definition)
        {
            var direct = definition.GetIcon();
            if (direct) return direct;
            var reference = definition.GetIconReference();
            if (reference == null || !reference.RuntimeKeyIsValid()) return null;
            // Own a fresh Addressables handle; never mutate/release the shared AssetReference handle.
            return sprites.Get(reference.RuntimeKey);
        }

        public void Clear() { sprites.Clear(); failures = 0; }

        private void ReportFailure(object key, Exception error)
        {
            if (failures < 5) log.LogWarning("ItemMarkers could not load/release pickup icon '" + key + "': " + error);
            else if (failures == 5) log.LogWarning("Further pickup icon errors are suppressed until the next run.");
            failures++;
        }

        private sealed class SpriteRequest : IAssetRequest<Sprite>
        {
            private AsyncOperationHandle<Sprite> handle;
            private bool released;
            public SpriteRequest(object key) { handle = Addressables.LoadAssetAsync<Sprite>(key); }
            public bool IsDone => !released && handle.IsValid() && handle.IsDone;
            public Sprite? Asset => IsDone && handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
            public Exception? Error => IsDone && handle.Status == AsyncOperationStatus.Failed
                ? handle.OperationException ?? new InvalidOperationException("Addressable sprite load failed.") : null;
            public void Dispose()
            {
                if (released) return;
                released = true;
                if (handle.IsValid()) Addressables.Release(handle);
                handle = default;
            }
        }
    }
}
