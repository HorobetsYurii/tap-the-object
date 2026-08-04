using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TapTheObject.Content;
using UnityEngine;
using UnityEngine.TestTools;

namespace TapTheObject.Tests
{
    /// <summary>Exercises the real provider, which the edit mode tests replace with a fake.</summary>
    [TestFixture]
    public sealed class AddressablesAssetProviderTests
    {
        private const string RoundImageLabel = "round-image";
        private const string FallbackTextureAddress = "FallbackTexture";
        private const int ExpectedRoundImageCount = 8;

        private AddressablesAssetProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _provider = new AddressablesAssetProvider();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Resolving_the_round_image_label_finds_every_image_in_the_catalog()
        {
            var resolveTask = _provider.ResolveKeysAsync(RoundImageLabel, CancellationToken.None);
            yield return WaitFor(resolveTask);

            Assert.That(resolveTask.Exception, Is.Null);
            Assert.That(resolveTask.Result.Count, Is.EqualTo(ExpectedRoundImageCount));
        }

        [UnityTest]
        public IEnumerator Loading_the_fallback_texture_returns_a_usable_texture()
        {
            var loadTask = _provider.LoadAsync<Texture2D>(AssetKey.From(FallbackTextureAddress), CancellationToken.None);
            yield return WaitFor(loadTask);

            Assert.That(loadTask.Exception, Is.Null);

            var handle = loadTask.Result;
            Assert.That(handle.Asset, Is.Not.Null);

            handle.Dispose();
            handle.Dispose(); // releasing twice must stay a no-op rather than over-releasing the reference
        }

        [UnityTest]
        public IEnumerator A_load_started_with_a_cancelled_token_is_cancelled()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var loadTask = _provider.LoadAsync<Texture2D>(AssetKey.From(FallbackTextureAddress), cancellation.Token);
            yield return WaitFor(loadTask);

            Assert.That(loadTask.IsCanceled, Is.True);
        }

        [UnityTest]
        public IEnumerator Loading_an_unknown_address_reports_an_asset_load_failure()
        {
            // Addressables logs its own error for a missing key, which is the expected outcome here.
            LogAssert.ignoreFailingMessages = true;

            var loadTask = _provider.LoadAsync<Texture2D>(AssetKey.From("no-such-address"), CancellationToken.None);
            yield return WaitFor(loadTask);

            Assert.That(loadTask.IsFaulted, Is.True);
            Assert.That(loadTask.Exception?.InnerException, Is.InstanceOf<AssetLoadException>());
        }

        /// <summary>Waits for an operation to settle, failing rather than hanging the run.</summary>
        private static IEnumerator WaitFor(Task task, float timeoutSeconds = 30f)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"The operation did not complete within {timeoutSeconds:F0}s.");
                }

                yield return null;
            }
        }
    }
}
