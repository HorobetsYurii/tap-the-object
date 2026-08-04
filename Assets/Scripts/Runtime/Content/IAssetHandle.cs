using System;
using Object = UnityEngine.Object;

namespace TapTheObject.Content
{
    /// <summary>
    /// A loaded asset together with ownership of the reference keeping it in memory. Disposing releases
    /// that reference; disposing again is a no-op.
    /// </summary>
    public interface IAssetHandle<out TAsset> : IDisposable where TAsset : Object
    {
        TAsset Asset { get; }
    }
}
