using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TapTheObject.Interaction
{
    /// <summary>
    /// Reports presses from whichever pointer the player is using, since the Input System exposes both mouse
    /// and touch as a <see cref="Pointer"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PointerTapSource : MonoBehaviour
    {
        public event Action<Vector2> Tapped;

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
            {
                return;
            }

            // Presses consumed by the HUD are not selections, and must not count as a miss.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Tapped?.Invoke(pointer.position.ReadValue());
        }
    }
}
