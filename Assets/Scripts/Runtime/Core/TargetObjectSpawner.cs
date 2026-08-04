using System;
using System.Threading;
using System.Threading.Tasks;
using TapTheObject.Content;
using TapTheObject.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TapTheObject.Core
{
    /// <summary>
    /// Loads the target prefab once and owns both the prefab reference and the spawned instance until
    /// <see cref="Dispose"/>.
    /// </summary>
    public sealed class TargetObjectSpawner : IDisposable
    {
        private readonly IAssetProvider _assetProvider;

        private IAssetHandle<GameObject> _prefab;
        private GameObject _instance;

        public TargetObjectSpawner(IAssetProvider assetProvider)
        {
            _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
        }

        /// <exception cref="AssetLoadException">The prefab could not be loaded, or carries no view component.</exception>
        public async Task<TargetObjectView> SpawnAsync(AssetKey prefabKey, CancellationToken cancellationToken)
        {
            if (_prefab != null)
            {
                throw new InvalidOperationException("The target object has already been spawned.");
            }

            var prefab = await _assetProvider.LoadAsync<GameObject>(prefabKey, cancellationToken);

            // Owned from here on, including on the failure path below.
            _prefab = prefab;

            _instance = Object.Instantiate(prefab.Asset);
            _instance.name = prefab.Asset.name;

            var view = _instance.GetComponent<TargetObjectView>();
            if (view == null)
            {
                throw new AssetLoadException(
                    $"Prefab '{prefab.Asset.name}' is missing a {nameof(TargetObjectView)} component.");
            }

            return view;
        }

        public void Dispose()
        {
            if (_instance != null)
            {
                Destroy(_instance);
                _instance = null;
            }

            _prefab?.Dispose();
            _prefab = null;
        }

        private static void Destroy(GameObject instance)
        {
            // Edit mode, which the tests run in, rejects the deferred Destroy.
            if (Application.isPlaying)
            {
                Object.Destroy(instance);
            }
            else
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
