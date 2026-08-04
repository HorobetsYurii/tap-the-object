using System;

namespace TapTheObject.Content
{
    /// <summary>
    /// Addressable content could not be loaded. Cancellation is reported separately, as an
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    public sealed class AssetLoadException : Exception
    {
        public AssetLoadException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
