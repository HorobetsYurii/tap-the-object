using System;
using UnityEngine;

namespace TapTheObject.Interaction
{
    /// <summary>Decides whether a tap in screen space landed on the target object.</summary>
    public sealed class TargetRaycaster
    {
        private readonly Camera _camera;
        private readonly Transform _target;

        public TargetRaycaster(Camera camera, Transform target)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _target = target != null ? target : throw new ArgumentNullException(nameof(target));
        }

        public bool IsTargetHit(Vector2 screenPosition)
        {
            // Either can be destroyed during teardown while a press is still being processed.
            if (_camera == null || _target == null)
            {
                return false;
            }

            var ray = _camera.ScreenPointToRay(screenPosition);

            // The collider may sit on a child of the spawned instance.
            return Physics.Raycast(ray, out var hit) && hit.transform.IsChildOf(_target);
        }
    }
}
