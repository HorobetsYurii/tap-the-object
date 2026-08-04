using System;
using System.Threading.Tasks;
using TapTheObject.Content;
using TapTheObject.Core;
using TapTheObject.Interaction;
using TapTheObject.Presentation;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TapTheObject.Bootstrap
{
    /// <summary>
    /// Builds a session from the scene references, runs it and tears it down with the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Addressable content")]
        [SerializeField] private AssetReferenceGameObject _targetPrefab;
        [SerializeField] private AssetReferenceTexture2D _fallbackTexture;
        [SerializeField] private AssetLabelReference _roundImageLabel;

        [Header("Scene")]
        [SerializeField] private Camera _camera;
        [SerializeField] private GameHudView _hud;
        [SerializeField] private PointerTapSource _tapSource;

        [Header("Diagnostics")]
        [SerializeField] private SimulatedNetworkSettings _simulatedNetwork = new SimulatedNetworkSettings();

        private TargetObjectSpawner _spawner;
        private GameFlowController _flow;
        private TargetRaycaster _raycaster;

        private void Start()
        {
            RunAsync().Forget();
        }

        private void OnDestroy()
        {
            if (_tapSource != null)
            {
                _tapSource.Tapped -= OnTapped;
            }

            _flow?.Dispose();
            _spawner?.Dispose();
        }

        private async Task RunAsync()
        {
            var assetProvider = CreateAssetProvider();
            _spawner = new TargetObjectSpawner(assetProvider);

            try
            {
                ValidateReferences();
                _hud.SetStatus(GameStatus.Loading);

                var target = await _spawner.SpawnAsync(AssetKey.From(_targetPrefab.RuntimeKey), destroyCancellationToken);
                _raycaster = new TargetRaycaster(_camera, target.transform);

                var content = new GameContent(AssetKey.From(_fallbackTexture.RuntimeKey), _roundImageLabel.labelString);
                _flow = new GameFlowController(assetProvider, target, _hud, content);

                _tapSource.Tapped += OnTapped;
                await _flow.StartAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The scene was torn down while start-up content was loading.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                if (_hud != null)
                {
                    _hud.SetStatus(GameStatus.Failed);
                    _hud.ShowMessage(GameMessage.ContentUnavailable);
                }
            }
        }

        private void OnTapped(Vector2 screenPosition)
        {
            _flow.HandleTap(_raycaster.IsTargetHit(screenPosition));
        }

        private IAssetProvider CreateAssetProvider()
        {
            IAssetProvider provider = new AddressablesAssetProvider();

            return _simulatedNetwork.Enabled
                ? new SimulatedNetworkAssetProvider(provider, _simulatedNetwork)
                : provider;
        }

        private void ValidateReferences()
        {
            if (_camera == null || _hud == null || _tapSource == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} is missing a scene reference (camera, HUD or tap source).");
            }

            if (!_targetPrefab.RuntimeKeyIsValid() || !_fallbackTexture.RuntimeKeyIsValid())
            {
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} is missing an addressable reference (target prefab or fallback texture).");
            }

            if (string.IsNullOrEmpty(_roundImageLabel.labelString))
            {
                throw new InvalidOperationException($"{nameof(GameBootstrap)} is missing the round image label.");
            }
        }
    }
}
