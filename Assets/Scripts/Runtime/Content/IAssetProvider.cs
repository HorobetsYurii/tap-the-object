using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Object = UnityEngine.Object;

namespace TapTheObject.Content
{
    /// <summary>
    /// Asynchronous access to addressable content.
    /// </summary>
    /// <remarks>
    /// Ownership: a successful <see cref="LoadAsync{TAsset}"/> transfers the reference to the caller, which
    /// must dispose the returned handle. If the call throws, whether cancelled or failed, there is nothing
    /// left to release.
    /// </remarks>
    public interface IAssetProvider
    {
        /// <summary>
        /// Resolves every asset carrying <paramref name="label"/> into keys for <see cref="LoadAsync{TAsset}"/>,
        /// so content can be added to the catalog without touching code.
        /// </summary>
        Task<IReadOnlyList<AssetKey>> ResolveKeysAsync(string label, CancellationToken cancellationToken);

        /// <exception cref="AssetLoadException">The asset could not be loaded.</exception>
        /// <exception cref="System.OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        Task<IAssetHandle<TAsset>> LoadAsync<TAsset>(AssetKey key, CancellationToken cancellationToken)
            where TAsset : Object;
    }
}
