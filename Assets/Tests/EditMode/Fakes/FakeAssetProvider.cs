using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TapTheObject.Content;
using TapTheObject.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TapTheObject.Tests.Fakes
{
    /// <summary>
    /// An <see cref="IAssetProvider"/> whose loads the test resolves by hand, so timings like a round
    /// superseded mid-load can be reproduced exactly. Counts handles that are still undisposed.
    /// </summary>
    internal sealed class FakeAssetProvider : IAssetProvider, IDisposable
    {
        private readonly List<IPendingLoad> _pendingLoads = new List<IPendingLoad>();
        private readonly List<AssetKey> _requestedKeys = new List<AssetKey>();
        private readonly Dictionary<AssetKey, Object> _assetsByKey = new Dictionary<AssetKey, Object>();
        private readonly List<Object> _createdAssets = new List<Object>();

        private int _liveHandleCount;

        /// <summary>Keys reported by <see cref="ResolveKeysAsync"/>.</summary>
        public IReadOnlyList<AssetKey> AvailableKeys { get; set; } = Array.Empty<AssetKey>();

        /// <summary>When false, loads stay pending until the test completes or fails them.</summary>
        public bool CompleteLoadsImmediately { get; set; } = true;

        /// <summary>When set, the next load fails with this message instead of succeeding. One-shot.</summary>
        public string NextLoadFailure { get; set; }

        /// <summary>Every key passed to <see cref="LoadAsync{TAsset}"/>, in order.</summary>
        public IReadOnlyList<AssetKey> RequestedKeys => _requestedKeys;

        /// <summary>Handles handed out but not yet disposed.</summary>
        public int LiveHandleCount => _liveHandleCount;

        public IPendingLoad OldestUnresolvedLoad => _pendingLoads.Find(load => !load.IsResolved);

        /// <summary>The asset most recently created for <paramref name="key"/>.</summary>
        public Object AssetFor(AssetKey key) => _assetsByKey.TryGetValue(key, out var asset) ? asset : null;

        public Task<IReadOnlyList<AssetKey>> ResolveKeysAsync(string label, CancellationToken cancellationToken)
        {
            return Task.FromResult(AvailableKeys);
        }

        public Task<IAssetHandle<TAsset>> LoadAsync<TAsset>(AssetKey key, CancellationToken cancellationToken)
            where TAsset : Object
        {
            _requestedKeys.Add(key);

            var pendingLoad = new PendingLoad<TAsset>(this, key, cancellationToken);
            _pendingLoads.Add(pendingLoad);

            if (CompleteLoadsImmediately)
            {
                var failure = NextLoadFailure;
                NextLoadFailure = null;

                if (failure == null)
                {
                    pendingLoad.Complete();
                }
                else
                {
                    pendingLoad.Fail(failure);
                }
            }

            return pendingLoad.Task;
        }

        public void Dispose()
        {
            foreach (var asset in _createdAssets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            _createdAssets.Clear();
        }

        private TAsset CreateAsset<TAsset>(AssetKey key) where TAsset : Object
        {
            Object asset;
            if (typeof(TAsset) == typeof(Texture2D))
            {
                asset = new Texture2D(2, 2) { name = $"Texture ({key})" };
            }
            else if (typeof(TAsset) == typeof(GameObject))
            {
                // Mirrors the real prefab closely enough for the spawner: a renderer plus the view component.
                var prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                prefab.name = $"Prefab ({key})";
                prefab.AddComponent<TargetObjectView>();
                asset = prefab;
            }
            else
            {
                throw new NotSupportedException($"The fake provider does not create {typeof(TAsset).Name} assets.");
            }

            _createdAssets.Add(asset);
            _assetsByKey[key] = asset;
            return (TAsset)asset;
        }

        /// <summary>A load the test can resolve on demand.</summary>
        internal interface IPendingLoad
        {
            AssetKey Key { get; }

            CancellationToken CancellationToken { get; }

            bool IsResolved { get; }

            void Complete();

            void Fail(string message);
        }

        private sealed class PendingLoad<TAsset> : IPendingLoad where TAsset : Object
        {
            private readonly FakeAssetProvider _owner;
            private readonly TaskCompletionSource<IAssetHandle<TAsset>> _completionSource =
                new TaskCompletionSource<IAssetHandle<TAsset>>();

            private readonly CancellationTokenRegistration _cancellationRegistration;

            public PendingLoad(FakeAssetProvider owner, AssetKey key, CancellationToken cancellationToken)
            {
                _owner = owner;
                Key = key;
                CancellationToken = cancellationToken;

                // As in the real provider, cancellation leaves nothing to release.
                _cancellationRegistration = cancellationToken.Register(() =>
                {
                    IsResolved = true;
                    _completionSource.TrySetCanceled(cancellationToken);
                });
            }

            public Task<IAssetHandle<TAsset>> Task => _completionSource.Task;

            public AssetKey Key { get; }

            public CancellationToken CancellationToken { get; }

            public bool IsResolved { get; private set; }

            public void Complete()
            {
                if (!TryResolve())
                {
                    return;
                }

                _completionSource.SetResult(new FakeAssetHandle<TAsset>(_owner, _owner.CreateAsset<TAsset>(Key)));
            }

            public void Fail(string message)
            {
                if (!TryResolve())
                {
                    return;
                }

                _completionSource.SetException(new AssetLoadException(message));
            }

            private bool TryResolve()
            {
                if (IsResolved)
                {
                    return false;
                }

                IsResolved = true;
                _cancellationRegistration.Dispose();
                return true;
            }
        }

        private sealed class FakeAssetHandle<TAsset> : IAssetHandle<TAsset> where TAsset : Object
        {
            private readonly FakeAssetProvider _owner;
            private bool _isDisposed;

            public FakeAssetHandle(FakeAssetProvider owner, TAsset asset)
            {
                _owner = owner;
                Asset = asset;
                _owner._liveHandleCount++;
            }

            public TAsset Asset { get; }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _owner._liveHandleCount--;
            }
        }
    }
}
