using System;
using System.Reflection;
using FFS.Libraries.StaticEcs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UniGame.StaticEcs.Unity.Tests
{
    public sealed class SerializableConverterTests
    {
        private const string TempFolder = "Assets/__StaticEcsSerializableConverterTests";
        private const string PrefabPath = TempFolder + "/Provider.prefab";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
        }

        [Test]
        public void SerializeReference_RoundTripsConcreteConverterThroughPrefab()
        {
            AssetDatabase.CreateFolder("Assets", "__StaticEcsSerializableConverterTests");
            var source = new GameObject("provider");
            var provider = source.AddComponent<StaticEcsEntityProvider>();
            provider.serializableConverters.Add(new TransformBindingSerializableConverter());

            PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var restored = prefab.GetComponent<StaticEcsEntityProvider>();

            Assert.That(restored.serializableConverters, Has.Count.EqualTo(1));
            Assert.That(restored.serializableConverters[0], Is.TypeOf<TransformBindingSerializableConverter>());
            Assert.That(restored.serializableConverters[0].IsEnabled, Is.True);
        }

        [Test]
        public void Preset_ForwardsLifecycleHooksToEnabledNestedConverters()
        {
            var preset = ScriptableObject.CreateInstance<TestConverterPreset>();
            var converter = new LifecycleSerializableConverter();
            preset.Add(converter);

            preset.ResolveLinks(default, null);
            preset.OnEntityDestroyed(default, null);

            Assert.That(converter.ResolveCount, Is.EqualTo(1));
            Assert.That(converter.DestroyCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(preset);
        }

        [Test]
        public void EnabledContract_UsesSerializedFlag()
        {
            var converter = new LifecycleSerializableConverter();
            typeof(EcsSerializableConverter<TestSerializableConverterWorld>)
                .GetField("_isEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(converter, false);

            Assert.That(converter.IsEnabled, Is.False);
        }

        [Test]
        public void TransformBinding_UsesHostAndExplicitTarget()
        {
            World<TestSerializableConverterWorld>.Create(WorldConfig.Default());
            World<TestSerializableConverterWorld>.Types().Component<TransformBindingComponent>();
            World<TestSerializableConverterWorld>.Initialize();
            var host = new GameObject("host");
            var target = new GameObject("target");
            try
            {
                var entity = World<TestSerializableConverterWorld>.NewEntity<Default>();
                var converter = new TransformBindingSerializableConverter<TestSerializableConverterWorld>();
                converter.Apply(entity, host);
                Assert.That(entity.Read<TransformBindingComponent>().Transform, Is.SameAs(host.transform));

                converter.Target = target.transform;
                converter.Apply(entity, host);
                Assert.That(entity.Read<TransformBindingComponent>().Transform, Is.SameAs(target.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(target);
                World<TestSerializableConverterWorld>.Destroy();
            }
        }

        [Test]
        public void EntityResource_AppliesAndClearsOwnedEntityReference()
        {
            World<TestSerializableConverterWorld>.Create(WorldConfig.Default());
            World<TestSerializableConverterWorld>.Initialize();
            try
            {
                var entity = World<TestSerializableConverterWorld>.NewEntity<Default>();
                var converter = new EcsEntityResourceSerializableConverter<
                    TestSerializableConverterWorld,
                    TestEntityResource>();

                converter.Apply(entity, null);
                Assert.That(World<TestSerializableConverterWorld>.GetResource<TestEntityResource>().Gid, Is.EqualTo(entity.GID));

                converter.OnEntityDestroyed(entity, null);
                Assert.That(World<TestSerializableConverterWorld>.GetResource<TestEntityResource>().Gid, Is.EqualTo(default(EntityGID)));
            }
            finally
            {
                World<TestSerializableConverterWorld>.Destroy();
            }
        }
    }

    internal sealed class TestConverterPreset : EcsConverterPreset<TestSerializableConverterWorld>
    {
        public void Add(IEcsConverter<TestSerializableConverterWorld> converter)
        {
            nestedConverters.Add(converter);
        }
    }

    [Serializable]
    internal sealed class LifecycleSerializableConverter :
        EcsSerializableConverter<TestSerializableConverterWorld>,
        IEcsLinkResolver<TestSerializableConverterWorld>,
        IEcsConverterDestroyHandler<TestSerializableConverterWorld>
    {
        public int ResolveCount { get; private set; }
        public int DestroyCount { get; private set; }

        public override void Apply(World<TestSerializableConverterWorld>.Entity entity, GameObject host) { }

        public void ResolveLinks(World<TestSerializableConverterWorld>.Entity entity, GameObject host)
        {
            ResolveCount++;
        }

        public void OnEntityDestroyed(World<TestSerializableConverterWorld>.Entity entity, GameObject host)
        {
            DestroyCount++;
        }
    }

    internal struct TestSerializableConverterWorld : IWorldType { }

    internal struct TestEntityResource : IEcsEntityRefResource
    {
        public EntityGID Gid { get; set; }
    }
}
