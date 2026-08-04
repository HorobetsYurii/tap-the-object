using System;
using System.Threading;
using NUnit.Framework;
using TapTheObject.Content;
using TapTheObject.Core;
using TapTheObject.Presentation;
using TapTheObject.Tests.Fakes;

namespace TapTheObject.Tests
{
    [TestFixture]
    public sealed class TargetObjectSpawnerTests
    {
        private static readonly AssetKey PrefabKey = AssetKey.From("target-object");

        private SynchronizationContext _previousContext;
        private FakeAssetProvider _assetProvider;
        private TargetObjectSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            _assetProvider = new FakeAssetProvider();
            _spawner = new TargetObjectSpawner(_assetProvider);
        }

        [TearDown]
        public void TearDown()
        {
            _spawner.Dispose();
            _assetProvider.Dispose();
            SynchronizationContext.SetSynchronizationContext(_previousContext);
        }

        [Test]
        public void Spawning_loads_the_prefab_once_and_places_one_instance()
        {
            var view = Spawn();

            Assert.That(view, Is.Not.Null);
            Assert.That(_assetProvider.RequestedKeys.Count, Is.EqualTo(1));
            Assert.That(_assetProvider.LiveHandleCount, Is.EqualTo(1), "the prefab reference is held for the session");
        }

        [Test]
        public void Spawning_twice_is_rejected_rather_than_leaking_the_first_prefab_reference()
        {
            Spawn();

            Assert.That(() => Spawn(), Throws.InstanceOf<InvalidOperationException>());
            Assert.That(_assetProvider.LiveHandleCount, Is.EqualTo(1));
        }

        [Test]
        public void Teardown_destroys_the_instance_and_releases_the_prefab()
        {
            var view = Spawn();

            _spawner.Dispose();

            Assert.That(view == null, Is.True, "the spawned instance is destroyed");
            Assert.That(_assetProvider.LiveHandleCount, Is.Zero);
        }

        private TargetObjectView Spawn()
        {
            var spawnTask = _spawner.SpawnAsync(PrefabKey, CancellationToken.None);
            Assert.That(spawnTask.IsCompleted, Is.True, "the fake provider resolves the load inline");
            return spawnTask.GetAwaiter().GetResult();
        }
    }
}
