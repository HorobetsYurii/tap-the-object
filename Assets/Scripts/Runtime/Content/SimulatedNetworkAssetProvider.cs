using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace TapTheObject.Content
{
    [Serializable]
    public sealed class SimulatedNetworkSettings
    {
        [Tooltip("Adds latency and failures on top of the real provider, so cancellation and the fallback " +
                 "texture can be observed on demand.")]
        [SerializeField] private bool _enabled;

        [SerializeField, Min(0f)] private float _minDelaySeconds = 0.5f;
        [SerializeField, Min(0f)] private float _maxDelaySeconds = 1.5f;
        [SerializeField, Range(0f, 1f)] private float _failureRate = 0.25f;

        public SimulatedNetworkSettings()
        {
        }

        public SimulatedNetworkSettings(float minDelaySeconds, float maxDelaySeconds, float failureRate)
        {
            _enabled = true;
            _minDelaySeconds = minDelaySeconds;
            _maxDelaySeconds = maxDelaySeconds;
            _failureRate = failureRate;
        }

        public bool Enabled => _enabled;

        public float MinDelaySeconds => _minDelaySeconds;

        public float MaxDelaySeconds => Mathf.Max(_minDelaySeconds, _maxDelaySeconds);

        public float FailureRate => _failureRate;
    }

    /// <summary>
    /// Adds artificial latency and failures to the round images. They are small enough that a real download
    /// is over before a tap can supersede it, which leaves cancellation and the fallback texture hard to
    /// observe; this makes both reproducible. Disabled by default.
    /// </summary>
    /// <remarks>
    /// Round images only. Failing the prefab or the fallback texture would just end the session, since
    /// there is nothing to fall back to.
    /// </remarks>
    public sealed class SimulatedNetworkAssetProvider : IAssetProvider
    {
        private readonly IAssetProvider _inner;
        private readonly SimulatedNetworkSettings _settings;
        private readonly Random _random;
        private readonly HashSet<AssetKey> _simulatedKeys = new HashSet<AssetKey>();

        public SimulatedNetworkAssetProvider(IAssetProvider inner, SimulatedNetworkSettings settings, Random random = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _random = random ?? new Random();
        }

        public async Task<IReadOnlyList<AssetKey>> ResolveKeysAsync(string label, CancellationToken cancellationToken)
        {
            var keys = await _inner.ResolveKeysAsync(label, cancellationToken);

            // Keys behind a label are the round images, and those are the ones simulated.
            foreach (var key in keys)
            {
                _simulatedKeys.Add(key);
            }

            return keys;
        }

        public async Task<IAssetHandle<TAsset>> LoadAsync<TAsset>(AssetKey key, CancellationToken cancellationToken)
            where TAsset : Object
        {
            if (!_simulatedKeys.Contains(key))
            {
                return await _inner.LoadAsync<TAsset>(key, cancellationToken);
            }

            await SimulateLatencyAsync(cancellationToken);

            if (_random.NextDouble() < _settings.FailureRate)
            {
                throw new AssetLoadException($"Simulated network failure while loading '{key}'.");
            }

            return await _inner.LoadAsync<TAsset>(key, cancellationToken);
        }

        private Task SimulateLatencyAsync(CancellationToken cancellationToken)
        {
            var range = _settings.MaxDelaySeconds - _settings.MinDelaySeconds;
            var seconds = _settings.MinDelaySeconds + range * _random.NextDouble();
            var milliseconds = (int)(seconds * 1000d);

            return milliseconds <= 0 ? Task.CompletedTask : Task.Delay(milliseconds, cancellationToken);
        }
    }
}
