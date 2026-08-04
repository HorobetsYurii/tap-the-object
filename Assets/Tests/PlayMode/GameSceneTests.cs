using System.Collections;
using NUnit.Framework;
using TapTheObject.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TapTheObject.Tests
{
    /// <summary>
    /// Loads the shipped scene and checks that the assembled game reaches a playable first round.
    /// </summary>
    [TestFixture]
    public sealed class GameSceneTests
    {
        private const string ReadyStatus = "Tap the object!";

        [UnityTest]
        public IEnumerator The_scene_spawns_the_target_object_and_shows_the_first_round_image()
        {
            yield return LoadPlayableScene();

            var target = Object.FindAnyObjectByType<TargetObjectView>();
            Assert.That(target, Is.Not.Null, "the addressable target prefab should have been spawned");

            var propertyBlock = new MaterialPropertyBlock();
            target.GetComponent<Renderer>().GetPropertyBlock(propertyBlock);
            Assert.That(propertyBlock.GetTexture("_MainTex"), Is.Not.Null, "a round image should be on the object");

            Assert.That(FindLabel("ScoreLabel").text, Is.EqualTo("Score: 0"));
        }

        /// <summary>Loads the scene and waits for start-up to report a playable round.</summary>
        private static IEnumerator LoadPlayableScene(float timeoutSeconds = 60f)
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);

            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (FindLabel("StatusLabel")?.text == ReadyStatus)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"The scene did not reach a playable round within {timeoutSeconds:F0}s " +
                        $"(status was '{FindLabel("StatusLabel")?.text}').");
        }

        private static TMP_Text FindLabel(string name)
        {
            var label = GameObject.Find(name);
            return label == null ? null : label.GetComponent<TMP_Text>();
        }
    }
}
