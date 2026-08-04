using System.Threading;
using NUnit.Framework;
using TapTheObject.Content;
using TapTheObject.Tests.Fakes;
using UnityEngine;

namespace TapTheObject.Tests
{
    [TestFixture]
    public sealed class SimulatedNetworkAssetProviderTests
    {
        private const string RoundImageLabel = "round-image";
        private static readonly AssetKey StartUpKey = AssetKey.From("target-object");

        private SynchronizationContext _previousContext;
        private FakeAssetProvider _inner;
        private SimulatedNetworkAssetProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            _inner = new FakeAssetProvider { AvailableKeys = new[] { AssetKey.From("round-image-0") } };

            // Fails every time it is allowed to, so the assertions do not depend on chance.
            _provider = new SimulatedNetworkAssetProvider(_inner, new SimulatedNetworkSettings(0f, 0f, 1f));
        }

        [TearDown]
        public void TearDown()
        {
            _inner.Dispose();
            SynchronizationContext.SetSynchronizationContext(_previousContext);
        }

        [Test]
        public void Start_up_content_is_left_alone_because_nothing_can_recover_from_losing_it()
        {
            var loadTask = _provider.LoadAsync<Texture2D>(StartUpKey, CancellationToken.None);

            Assert.That(loadTask.IsCompleted, Is.True);
            Assert.That(loadTask.Result.Asset, Is.Not.Null);
        }

        [Test]
        public void An_image_the_label_resolved_to_is_simulated()
        {
            var resolveTask = _provider.ResolveKeysAsync(RoundImageLabel, CancellationToken.None);
            var imageKey = resolveTask.Result[0];

            var loadTask = _provider.LoadAsync<Texture2D>(imageKey, CancellationToken.None);

            Assert.That(loadTask.IsFaulted, Is.True);
            Assert.That(loadTask.Exception?.InnerException, Is.InstanceOf<AssetLoadException>());
        }
    }
}
