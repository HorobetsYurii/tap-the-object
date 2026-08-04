using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace TapTheObject.Content
{
    /// <summary>
    /// <see cref="IAssetProvider"/> backed by Addressables, and the only type that touches the package.
    /// </summary>
    public sealed class AddressablesAssetProvider : IAssetProvider
    {
        public async Task<IReadOnlyList<AssetKey>> ResolveKeysAsync(string label, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(label))
            {
                throw new ArgumentException("Label must not be null or empty.", nameof(label));
            }

            var handle = Addressables.LoadResourceLocationsAsync(label);
            try
            {
                var locations = await AwaitAsync(handle, label, cancellationToken);

                var keys = new AssetKey[locations.Count];
                for (var i = 0; i < locations.Count; i++)
                {
                    keys[i] = AssetKey.From(locations[i]);
                }

                return keys;
            }
            finally
            {
                // Locations are plain data, so the operation is only needed until they are copied out.
                // AwaitAsync has already released it if the load failed or was cancelled.
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        public async Task<IAssetHandle<TAsset>> LoadAsync<TAsset>(AssetKey key, CancellationToken cancellationToken)
            where TAsset : Object
        {
            if (!key.IsValid)
            {
                throw new ArgumentException("Asset key is not initialised.", nameof(key));
            }

            // A key from ResolveKeysAsync is already a resolved location, which skips a catalog lookup.
            var handle = key.Value is IResourceLocation location
                ? Addressables.LoadAssetAsync<TAsset>(location)
                : Addressables.LoadAssetAsync<TAsset>(key.Value);

            var asset = await AwaitAsync(handle, key.ToString(), cancellationToken);
            return new AddressablesAssetHandle<TAsset>(handle, asset);
        }

        /// <summary>
        /// Awaits an operation without blocking the main thread and normalises its outcome: the result,
        /// <see cref="OperationCanceledException"/>, or <see cref="AssetLoadException"/>.
        /// </summary>
        private static async Task<TResult> AwaitAsync<TResult>(
            AsyncOperationHandle<TResult> handle,
            string description,
            CancellationToken cancellationToken)
        {
            // ToAwaitable releases the handle when its token is cancelled, and that registration outlives
            // the await. Passing the caller's token straight through would let a later cancellation release
            // an asset that was already handed over, so the await is scoped to a source disposed here.
            using var loadScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                return await handle.ToAwaitable(loadScope.Token);
            }
            catch (AsyncOperationHandleException exception)
            {
                // The awaited reference is already gone; the exception carries a separate one to drop.
                exception.Handle.Release();
                throw new AssetLoadException($"Failed to load addressable content '{description}'.", exception);
            }
        }

        private sealed class AddressablesAssetHandle<TAsset> : IAssetHandle<TAsset> where TAsset : Object
        {
            private AsyncOperationHandle<TAsset> _handle;

            public AddressablesAssetHandle(AsyncOperationHandle<TAsset> handle, TAsset asset)
            {
                _handle = handle;
                Asset = asset;
            }

            public TAsset Asset { get; }

            public void Dispose()
            {
                if (!_handle.IsValid())
                {
                    return;
                }

                Addressables.Release(_handle);
                _handle = default;
            }
        }
    }
}
