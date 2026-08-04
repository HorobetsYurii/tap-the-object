using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using TapTheObject.Content;
using TapTheObject.Core;
using TapTheObject.Presentation;
using TapTheObject.Tests.Fakes;
using Random = System.Random;

namespace TapTheObject.Tests
{
    [TestFixture]
    public sealed class GameFlowControllerTests
    {
        private const string RoundImageLabel = "round-image";
        private static readonly AssetKey FallbackKey = AssetKey.From("fallback-texture");

        private SynchronizationContext _previousContext;
        private FakeAssetProvider _assetProvider;
        private RecordingTargetView _target;
        private RecordingGameHud _hud;
        private GameFlowController _flow;

        [SetUp]
        public void SetUp()
        {
            // The editor's synchronization context would defer the flow's continuations to a player loop
            // tick that never comes in an edit mode test. Without a context they resume inline.
            _previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            _assetProvider = new FakeAssetProvider { AvailableKeys = CreateImageKeys(4) };
            _target = new RecordingTargetView();
            _hud = new RecordingGameHud();
        }

        [TearDown]
        public void TearDown()
        {
            _flow?.Dispose();
            _assetProvider.Dispose();
            SynchronizationContext.SetSynchronizationContext(_previousContext);
        }

        [Test]
        public void Starting_a_session_loads_the_fallback_texture_and_shows_the_first_round_image()
        {
            StartSession();

            Assert.That(_assetProvider.RequestedKeys[0], Is.EqualTo(FallbackKey), "the fallback is loaded up front");
            Assert.That(_assetProvider.RequestedKeys.Count, Is.EqualTo(2), "fallback plus the first round image");
            Assert.That(_target.Texture, Is.SameAs(_assetProvider.AssetFor(_assetProvider.RequestedKeys[1])));
            Assert.That(_hud.Status, Is.EqualTo(GameStatus.Ready));
            Assert.That(_hud.Score, Is.Zero);
        }

        [Test]
        public void The_object_carries_the_fallback_texture_while_the_first_image_downloads()
        {
            _assetProvider.CompleteLoadsImmediately = false;
            _flow = new GameFlowController(
                _assetProvider, _target, _hud, new GameContent(FallbackKey, RoundImageLabel), new Random(1));

            var startTask = _flow.StartAsync(CancellationToken.None);
            _assetProvider.OldestUnresolvedLoad.Complete(); // the fallback texture

            Assert.That(startTask.IsCompleted, Is.False, "the first round image is still on its way");
            Assert.That(_target.Texture, Is.SameAs(_assetProvider.AssetFor(FallbackKey)));
            Assert.That(_hud.Status, Is.EqualTo(GameStatus.Loading));
        }

        [Test]
        public void A_correct_selection_scores_a_point_and_shows_a_new_image()
        {
            StartSession();
            var firstTexture = _target.Texture;

            _flow.HandleTap(hitTarget: true);

            Assert.That(_flow.Score, Is.EqualTo(1));
            Assert.That(_hud.Score, Is.EqualTo(1));
            Assert.That(_assetProvider.RequestedKeys.Count, Is.EqualTo(3), "a new image is requested");
            Assert.That(_target.Texture, Is.Not.SameAs(firstTexture));
            Assert.That(_hud.Status, Is.EqualTo(GameStatus.Ready));
        }

        [Test]
        public void An_incorrect_selection_keeps_the_image_and_shows_negative_feedback()
        {
            StartSession();
            var texture = _target.Texture;
            var requestsBefore = _assetProvider.RequestedKeys.Count;

            _flow.HandleTap(hitTarget: false);

            Assert.That(_flow.Score, Is.Zero);
            Assert.That(_assetProvider.RequestedKeys.Count, Is.EqualTo(requestsBefore), "a miss requests nothing");
            Assert.That(_target.Texture, Is.SameAs(texture), "the image stays put");
            Assert.That(_target.ErrorFeedbackCount, Is.EqualTo(1));
            Assert.That(_hud.Messages, Does.Contain(GameMessage.Miss));
        }

        [Test]
        public void The_object_reads_as_loading_until_its_image_arrives()
        {
            StartSession();
            Assert.That(_target.IsLoading, Is.False);

            _assetProvider.CompleteLoadsImmediately = false;
            _flow.HandleTap(hitTarget: true);

            Assert.That(_target.IsLoading, Is.True, "the image it still shows is the previous round's");

            _assetProvider.OldestUnresolvedLoad.Complete();

            Assert.That(_target.IsLoading, Is.False);
        }

        [Test]
        public void A_new_round_clears_the_notice_the_previous_one_left_on_screen()
        {
            StartSession();
            _flow.HandleTap(hitTarget: false);
            Assert.That(_hud.VisibleMessage, Is.EqualTo(GameMessage.Miss));

            _flow.HandleTap(hitTarget: true);

            Assert.That(_hud.VisibleMessage, Is.Null, "the miss belongs to the round it happened in");
        }

        [Test]
        public void A_new_round_cancels_the_previous_load_so_a_late_image_cannot_overwrite_it()
        {
            StartSession();
            _assetProvider.CompleteLoadsImmediately = false;

            _flow.HandleTap(hitTarget: true);
            var supersededLoad = _assetProvider.OldestUnresolvedLoad;
            Assert.That(supersededLoad, Is.Not.Null, "the second round should be waiting on its image");

            _flow.HandleTap(hitTarget: true);
            var currentLoad = _assetProvider.OldestUnresolvedLoad;

            Assert.That(supersededLoad.CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(currentLoad, Is.Not.SameAs(supersededLoad));

            var textureBeforeLateArrival = _target.Texture;
            supersededLoad.Complete();
            Assert.That(_target.Texture, Is.SameAs(textureBeforeLateArrival), "a cancelled round must not win");

            currentLoad.Complete();
            Assert.That(_target.Texture, Is.SameAs(_assetProvider.AssetFor(currentLoad.Key)));
            Assert.That(_assetProvider.LiveHandleCount, Is.EqualTo(2), "only the fallback and the current image");
        }

        [Test]
        public void A_new_round_releases_the_image_it_replaces()
        {
            StartSession();
            Assert.That(_assetProvider.LiveHandleCount, Is.EqualTo(2), "the fallback and the first image");

            _flow.HandleTap(hitTarget: true);
            _flow.HandleTap(hitTarget: true);

            Assert.That(_assetProvider.LiveHandleCount, Is.EqualTo(2), "earlier images must not pile up");
        }

        [Test]
        public void A_failed_image_load_shows_the_fallback_texture_and_the_round_continues()
        {
            StartSession();
            _assetProvider.NextLoadFailure = "simulated download failure";

            _flow.HandleTap(hitTarget: true);

            Assert.That(_target.Texture, Is.SameAs(_assetProvider.AssetFor(FallbackKey)));
            Assert.That(_hud.Status, Is.EqualTo(GameStatus.Ready), "the player can still play");
            Assert.That(_hud.Messages, Does.Contain(GameMessage.ImageLoadFailed));
        }

        [Test]
        public void An_empty_catalog_shows_the_fallback_texture_instead_of_failing()
        {
            _assetProvider.AvailableKeys = Array.Empty<AssetKey>();

            StartSession();

            Assert.That(_target.Texture, Is.SameAs(_assetProvider.AssetFor(FallbackKey)));
            Assert.That(_hud.Messages, Does.Contain(GameMessage.ImageLoadFailed));
        }

        [Test]
        public void Consecutive_rounds_never_request_the_same_image_twice_in_a_row()
        {
            StartSession();

            for (var round = 0; round < 20; round++)
            {
                _flow.HandleTap(hitTarget: true);
            }

            var keys = _assetProvider.RequestedKeys;

            // Index 0 is the fallback; the rest are round images, in order.
            for (var i = 2; i < keys.Count; i++)
            {
                Assert.That(keys[i], Is.Not.EqualTo(keys[i - 1]), $"round {i} repeated the image on screen");
            }
        }

        [Test]
        public void An_image_that_arrives_after_its_round_was_superseded_is_released()
        {
            StartSession();
            _assetProvider.CompleteLoadsImmediately = false;

            // Hold the round's continuation instead of running it inline, which is the only way to
            // reproduce an image that completes just before its round is cancelled. The load has to be
            // completed from outside the context, or the continuation is inlined into this thread anyway.
            var deferred = new DeferredSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(deferred);
            _flow.HandleTap(hitTarget: true);
            SynchronizationContext.SetSynchronizationContext(null);

            var supersededLoad = _assetProvider.OldestUnresolvedLoad;
            var appliedBefore = _target.TextureChangeCount;

            supersededLoad.Complete();
            Assert.That(_target.TextureChangeCount, Is.EqualTo(appliedBefore), "the continuation should be held");

            _flow.Dispose();
            deferred.Drain();

            Assert.That(_assetProvider.LiveHandleCount, Is.Zero, "the late image must not outlive the session");
        }

        [Test]
        public void Teardown_releases_every_asset_the_session_loaded()
        {
            StartSession();
            _flow.HandleTap(hitTarget: true);

            _flow.Dispose();

            Assert.That(_assetProvider.LiveHandleCount, Is.Zero);
        }

        [Test]
        public void Teardown_cancels_a_load_that_is_still_in_flight()
        {
            StartSession();
            _assetProvider.CompleteLoadsImmediately = false;
            _flow.HandleTap(hitTarget: true);
            var inFlightLoad = _assetProvider.OldestUnresolvedLoad;

            _flow.Dispose();

            Assert.That(inFlightLoad.CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(_assetProvider.LiveHandleCount, Is.Zero, "a cancelled load leaves nothing behind");
        }

        [Test]
        public void Selections_are_ignored_once_the_session_is_torn_down()
        {
            StartSession();
            var requestsBefore = _assetProvider.RequestedKeys.Count;

            _flow.Dispose();
            _flow.HandleTap(hitTarget: true);

            Assert.That(_flow.Score, Is.Zero);
            Assert.That(_assetProvider.RequestedKeys.Count, Is.EqualTo(requestsBefore));
        }

        private void StartSession(int randomSeed = 1)
        {
            _flow = new GameFlowController(
                _assetProvider,
                _target,
                _hud,
                new GameContent(FallbackKey, RoundImageLabel),
                new Random(randomSeed));

            var startTask = _flow.StartAsync(CancellationToken.None);

            Assert.That(startTask.IsCompleted, Is.True, "the fake provider resolves start-up inline");
            startTask.GetAwaiter().GetResult();
        }

        /// <summary>Holds posted continuations until the test asks for them.</summary>
        private sealed class DeferredSynchronizationContext : SynchronizationContext
        {
            private readonly Queue<Action> _pending = new Queue<Action>();

            public override void Post(SendOrPostCallback callback, object state)
            {
                _pending.Enqueue(() => callback(state));
            }

            public void Drain()
            {
                while (_pending.Count > 0)
                {
                    _pending.Dequeue().Invoke();
                }
            }
        }

        private static AssetKey[] CreateImageKeys(int count)
        {
            var keys = new AssetKey[count];
            for (var i = 0; i < count; i++)
            {
                keys[i] = AssetKey.From($"round-image-{i}");
            }

            return keys;
        }
    }
}
