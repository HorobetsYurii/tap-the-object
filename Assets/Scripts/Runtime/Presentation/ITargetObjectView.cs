using UnityEngine;

namespace TapTheObject.Presentation
{
    /// <summary>The spawned object as the round loop sees it.</summary>
    public interface ITargetObjectView
    {
        void SetTexture(Texture2D texture);

        /// <summary>
        /// Marks the object as waiting for its next image. It keeps the image it already has, so without
        /// this the only sign of a load would be the status label.
        /// </summary>
        void SetLoading(bool isLoading);

        /// <summary>Plays the feedback for an incorrect selection, leaving the image untouched.</summary>
        void PlayErrorFeedback();
    }
}
