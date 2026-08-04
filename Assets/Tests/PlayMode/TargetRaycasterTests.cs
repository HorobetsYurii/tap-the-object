using System.Collections;
using NUnit.Framework;
using TapTheObject.Interaction;
using UnityEngine;
using UnityEngine.TestTools;

namespace TapTheObject.Tests
{
    /// <summary>Covers the selection rule: a tap either lands on the object or on empty space.</summary>
    [TestFixture]
    public sealed class TargetRaycasterTests
    {
        private const int ViewportSize = 256;

        // Well away from the origin, so colliders left by another test's scene cannot end up in front.
        private static readonly Vector3 TestOrigin = new Vector3(1000f, 1000f, 1000f);

        private Camera _camera;
        private RenderTexture _viewport;
        private GameObject _target;

        [SetUp]
        public void SetUp()
        {
            // Rendering into a texture of a known size gives the camera a deterministic pixel rect, which a
            // batch mode run would otherwise not have.
            _viewport = new RenderTexture(ViewportSize, ViewportSize, 16);

            _camera = new GameObject("Test Camera", typeof(Camera)).GetComponent<Camera>();
            _camera.transform.position = TestOrigin + new Vector3(0f, 0f, -5f);
            _camera.targetTexture = _viewport;

            _target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _target.transform.position = TestOrigin;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_target);
            Object.Destroy(_camera.gameObject);
            _viewport.Release();
            Object.Destroy(_viewport);
        }

        [UnityTest]
        public IEnumerator A_tap_on_the_object_is_a_hit_and_a_tap_on_empty_space_is_a_miss()
        {
            yield return null;

            // Moving a transform does not move its collider until physics syncs, and queries do not sync on
            // their own. In the game many frames pass between spawning and the first tap; here they do not.
            Physics.SyncTransforms();

            var raycaster = new TargetRaycaster(_camera, _target.transform);
            var onTarget = (Vector2)_camera.WorldToScreenPoint(_target.transform.position);

            Assert.That(raycaster.IsTargetHit(onTarget), Is.True, "the object sits under this point");
            Assert.That(raycaster.IsTargetHit(Vector2.one), Is.False, "the corner of the view is empty space");
        }
    }
}
