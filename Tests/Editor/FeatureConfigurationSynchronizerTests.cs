using System.Collections.Generic;
using FFS.Libraries.StaticEcs;
using NUnit.Framework;
using UniGame.Core.Runtime;
using UniGame.StaticEcs.Editor.Validation;
using UniGame.StaticEcs.Random;
using UnityEditor;
using UnityEngine;

namespace UniGame.StaticEcs.Unity.Tests
{
    public sealed class FeatureConfigurationSynchronizerTests
    {
        private const string TestFolder = "Assets/__StaticEcsFeatureSyncTests";

        [Test]
        public void Synchronize_RemovesNullAndDuplicates_PreservingFirstEntryStateAndOrder()
        {
            var first = ScriptableObject.CreateInstance<SyncFeatureAsset>();
            var second = ScriptableObject.CreateInstance<SyncFeatureAsset>();
            var wrongWorld = ScriptableObject.CreateInstance<WrongWorldFeatureAsset>();
            var entries = new List<StaticEcsFeatureEntry>
            {
                new() { enabled = false, asset = first },
                null,
                new() { enabled = true, asset = first },
            };

            var result = FeatureConfigurationSynchronizer.Synchronize<SyncWorld>(
                entries,
                new StaticEcsFeatureAssetBase[] { first, wrongWorld, second });

            Assert.That(result.removed, Is.EqualTo(2));
            Assert.That(result.added, Is.EqualTo(1));
            Assert.That(result.wrongWorldSkipped, Is.EqualTo(1));
            Assert.That(entries[0].asset, Is.SameAs(first));
            Assert.That(entries[0].enabled, Is.False);
            Assert.That(entries[1].asset, Is.SameAs(second));
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(wrongWorld);
        }

        [Test]
        public void SynchronizeProjectAssets_UsesAssetsScope_FiltersWorld_AndSupportsUndo()
        {
            AssetDatabase.DeleteAsset(TestFolder);
            AssetDatabase.CreateFolder("Assets", "__StaticEcsFeatureSyncTests");
            var source = ScriptableObject.CreateInstance<StaticEcsServiceSource>();
            var compatible = ScriptableObject.CreateInstance<EcsRngFeatureAsset>();
            try
            {
                AssetDatabase.CreateAsset(source, $"{TestFolder}/Source.asset");
                AssetDatabase.CreateAsset(compatible, $"{TestFolder}/Compatible.asset");
                AssetDatabase.SaveAssets();

                var result = FeatureConfigurationSynchronizer.SynchronizeProjectAssets(source);

                Assert.That(result.added, Is.GreaterThanOrEqualTo(1));
                Assert.That(source.features.Exists(entry => entry.asset == compatible), Is.True);
                Assert.That(source.features, Has.All.Matches<StaticEcsFeatureEntry>(entry =>
                    AssetDatabase.GetAssetPath(entry.asset).StartsWith("Assets/")));

                Undo.PerformUndo();
                Assert.That(source.features, Is.Empty);
            }
            finally
            {
                AssetDatabase.DeleteAsset(TestFolder);
            }
        }

        private struct SyncWorld : IWorldType { }
        private struct WrongWorld : IWorldType { }

        private sealed class SyncFeatureAsset : StaticEcsFeatureAsset<SyncWorld>
        {
            public override IStaticEcsFeature<SyncWorld> CreateFeature(IContext context) => null;
        }

        private sealed class WrongWorldFeatureAsset : StaticEcsFeatureAsset<WrongWorld>
        {
            public override IStaticEcsFeature<WrongWorld> CreateFeature(IContext context) => null;
        }
    }
}
