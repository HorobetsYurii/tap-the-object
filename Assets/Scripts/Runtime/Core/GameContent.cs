using TapTheObject.Content;

namespace TapTheObject.Core
{
    /// <summary>Addressable content a session needs, resolved from the inspector before it starts.</summary>
    public readonly struct GameContent
    {
        public GameContent(AssetKey fallbackTexture, string roundImageLabel)
        {
            FallbackTexture = fallbackTexture;
            RoundImageLabel = roundImageLabel;
        }

        public AssetKey FallbackTexture { get; }

        /// <summary>Label every round image carries, used to discover them from the catalog.</summary>
        public string RoundImageLabel { get; }
    }
}
