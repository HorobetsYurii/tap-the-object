using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TapTheObject.Content;
using TapTheObject.Presentation;
using UnityEngine;
using Random = System.Random;

namespace TapTheObject.Core
{
    /// <summary>
    /// Runs the round loop. Each round shows a newly loaded image, a hit starts the next round, a miss only
    /// plays feedback. Owns every asset it loads and releases them on <see cref="Dispose"/>.
    /// </summary>
    public sealed class GameFlowController : IDisposable
    {
        private readonly IAssetProvider _assetProvider;
        private readonly ITargetObjectView _target;
        private readonly IGameHud _hud;
        private readonly GameContent _content;
        private readonly Random _random;

        private CancellationTokenSource _sessionCts;
        private CancellationTokenSource _roundCts;
        private IAssetHandle<Texture2D> _fallbackTexture;
        private IAssetHandle<Texture2D> _currentTexture;
        private IReadOnlyList<AssetKey> _roundImageKeys = Array.Empty<AssetKey>();
        private int _currentImageIndex = -1;
        private bool _isRunning;
        private bool _isDisposed;

        public GameFlowController(
            IAssetProvider assetProvider,
            ITargetObjectView target,
            IGameHud hud,
            GameContent content,
            Random random = null)
        {
            _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _content = content;
            _random = random ?? new Random();
        }

        public int Score { get; private set; }

        /// <summary>
        /// Loads the fallback texture and the set of round images, then starts the first round.
        /// </summary>
        /// <exception cref="AssetLoadException">Start-up content is unavailable.</exception>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(GameFlowController));
            }

            if (_sessionCts != null)
            {
                throw new InvalidOperationException("The session has already been started.");
            }

            _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _hud.SetScore(Score);
            _hud.SetStatus(GameStatus.Loading);

            _fallbackTexture = await _assetProvider.LoadAsync<Texture2D>(_content.FallbackTexture, _sessionCts.Token);

            // Shown straight away, so the object carries an image while the first round image downloads.
            _target.SetTexture(_fallbackTexture.Asset);

            _roundImageKeys = await _assetProvider.ResolveKeysAsync(_content.RoundImageLabel, _sessionCts.Token);

            _isRunning = true;
            await RunRoundAsync();
        }

        /// <summary>
        /// Handles a selection. A hit scores a point and starts the next round, which supersedes an image
        /// still being downloaded. A miss leaves the current image in place.
        /// </summary>
        public void HandleTap(bool hitTarget)
        {
            if (!_isRunning)
            {
                return;
            }

            if (!hitTarget)
            {
                _hud.ShowMessage(GameMessage.Miss);
                _target.PlayErrorFeedback();
                return;
            }

            Score++;
            _hud.SetScore(Score);
            RunRoundAsync().Forget();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _isRunning = false;

            // Cancel first, so a load still in flight releases its own reference instead of leaking it.
            _sessionCts?.Cancel();

            _roundCts?.Dispose();
            _sessionCts?.Dispose();
            _roundCts = null;
            _sessionCts = null;

            _currentTexture?.Dispose();
            _fallbackTexture?.Dispose();
            _currentTexture = null;
            _fallbackTexture = null;
        }

        private async Task RunRoundAsync()
        {
            var roundToken = BeginRound();
            _hud.SetStatus(GameStatus.Loading);

            // The previous round's miss or failure notice does not apply here.
            _hud.ClearMessage();
            _target.SetLoading(true);

            if (_roundImageKeys.Count == 0)
            {
                ApplyRoundResult(-1, null, "no round images are present in the catalog");
                return;
            }

            var imageIndex = PickNextImageIndex();
            IAssetHandle<Texture2D> loaded = null;
            string failure = null;

            try
            {
                loaded = await _assetProvider.LoadAsync<Texture2D>(_roundImageKeys[imageIndex], roundToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (AssetLoadException exception)
            {
                failure = exception.Message;
            }

            // The image can arrive just before the round is cancelled. Releasing it is then this round's job.
            if (roundToken.IsCancellationRequested)
            {
                loaded?.Dispose();
                return;
            }

            ApplyRoundResult(imageIndex, loaded, failure);
        }

        /// <summary>
        /// Opens a round and cancels the one before it, so an image that arrives late cannot overwrite a
        /// newer one.
        /// </summary>
        private CancellationToken BeginRound()
        {
            _roundCts?.Cancel();
            _roundCts?.Dispose();
            _roundCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);
            return _roundCts.Token;
        }

        private void ApplyRoundResult(int imageIndex, IAssetHandle<Texture2D> loaded, string failure)
        {
            // Releasing only now keeps an image on the object while the next one downloads.
            _currentTexture?.Dispose();
            _currentTexture = loaded;
            _currentImageIndex = loaded == null ? -1 : imageIndex;

            _target.SetTexture(loaded == null ? _fallbackTexture?.Asset : loaded.Asset);
            _target.SetLoading(false);
            _hud.SetStatus(GameStatus.Ready);

            if (failure == null)
            {
                return;
            }

            Debug.LogWarning($"[TapTheObject] Round image unavailable ({failure}), showing the fallback texture.");
            _hud.ShowMessage(GameMessage.ImageLoadFailed);
        }

        /// <summary>
        /// Picks an image other than the one on screen, so a correct selection is always visible. Indexing
        /// around the current image keeps the choice uniform and always terminates.
        /// </summary>
        private int PickNextImageIndex()
        {
            if (_roundImageKeys.Count == 1)
            {
                return 0;
            }

            var index = _random.Next(_roundImageKeys.Count - 1);
            if (_currentImageIndex >= 0 && index >= _currentImageIndex)
            {
                index++;
            }

            return index;
        }
    }
}
