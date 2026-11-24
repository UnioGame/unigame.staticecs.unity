namespace UniGame.StaticEcs.Unity.Tests
{
    using System;
    using FFS.Libraries.StaticEcs;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

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
            provider.serializableConverters.Add(new TransformSerializableConverter());

            PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var restored = prefab.GetComponent<StaticEcsEntityProvider>();

            Assert.That(restored.serializableConverters, Has.Count.EqualTo(1));
            Assert.That(
                restored.serializableConverters[0],
                Is.TypeOf<TransformSerializableConverter>()
            );
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
        public void Preset_AppliesNestedConvertersInConfiguredOrder()
        {
            World<TestSerializableConverterWorld>.Create(WorldConfig.Default());
            World<TestSerializableConverterWorld>.Types().Component<PresetValueComponent>();
            World<TestSerializableConverterWorld>.Initialize();
            var preset = ScriptableObject.CreateInstance<TestConverterPreset>();
            preset.Add(new PresetValueSerializableConverter { Value = 10 });
            preset.Add(new PresetValueSerializableConverter { Value = 20 });
            try
            {
                var entity = World<TestSerializableConverterWorld>.NewEntity<Default>();

                preset.Apply(entity, null);

                if (entity.Has<PresetValueComponent>())
                    Assert.That(entity.Read<PresetValueComponent>().Value, Is.EqualTo(20));
                else
                    Assert.Fail("Preset did not add PresetValueComponent.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
                World<TestSerializableConverterWorld>.Destroy();
            }
        }

        [Test]
        public void Preset_CanBeReusedWithoutSharingEntityState()
        {
            World<TestSerializableConverterWorld>.Create(WorldConfig.Default());
            World<TestSerializableConverterWorld>.Types().Component<PresetValueComponent>();
            World<TestSerializableConverterWorld>.Initialize();
            var preset = ScriptableObject.CreateInstance<TestConverterPreset>();
            preset.Add(new PresetValueSerializableConverter { Value = 10 });
            try
            {
                var first = World<TestSerializableConverterWorld>.NewEntity<Default>();
                var second = World<TestSerializableConverterWorld>.NewEntity<Default>();
                preset.Apply(first, null);
                preset.Apply(second, null);

                if (first.Has<PresetValueComponent>() && second.Has<PresetValueComponent>())
                {
                    first.Ref<PresetValueComponent>().Value = 99;
                    Assert.That(second.Read<PresetValueComponent>().Value, Is.EqualTo(10));
                }
                else
                    Assert.Fail("Preset did not add PresetValueComponent to both entities.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
                World<TestSerializableConverterWorld>.Destroy();
            }
        }

        [Test]
        public void Provider_SkipsDisabledConverterForEveryLifecyclePhase()
        {
            EcsAuthoringRegistry<TestSerializableConverterWorld>.Clear();
            World<TestSerializableConverterWorld>.Create(WorldConfig.Default());
            World<TestSerializableConverterWorld>.Initialize();
            EcsAuthoringRegistry<TestSerializableConverterWorld>.BeginWorld();
            GameObject host = null;
            try
            {
                host = new GameObject("disabled-provider");
                var provider = host.AddComponent<TestEntityProvider>();
                var converter = new LifecycleSerializableConverter();
                converter.Enabled = false;
                provider.serializableConverters.Add(converter);

                Assert.That(provider.CreateEntity(), Is.True);
                Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain(), Is.EqualTo(1));
                var gid = provider.EntityGid;
                Assert.That(gid.TryUnpack<TestSerializableConverterWorld>(out _), Is.True);
                UnityEngine.Object.DestroyImmediate(host);
                host = null;

                Assert.That(converter.ApplyCount, Is.Zero);
                Assert.That(converter.ResolveCount, Is.Zero);
                Assert.That(converter.DestroyCount, Is.Zero);
                Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Count, Is.Zero);
                Assert.That(gid.TryUnpack<TestSerializableConverterWorld>(out _), Is.True,
                    "The None destruction policy must leave the entity owned by its world.");
            }
            finally
            {
                try
                {
                    if (host != null)
                        UnityEngine.Object.DestroyImmediate(host);
                }
                finally
                {
                    try
                    {
                        EcsAuthoringRegistry<TestSerializableConverterWorld>.Clear();
                    }
                    finally
                    {
                        World<TestSerializableConverterWorld>.Destroy();
                    }
                }
            }
        }

        [Test]
        public void Provider_ForwardsEnabledConverterLifecycleExactlyOnce()
        {
            EcsAuthoringRegistry<TestSerializableConverterWorld>.Clear();
            World<TestSerializableConverterWorld>.Create(WorldConfig.Default());
            World<TestSerializableConverterWorld>.Initialize();
            EcsAuthoringRegistry<TestSerializableConverterWorld>.BeginWorld();
            GameObject host = null;
            try
            {
                host = new GameObject("enabled-provider");
                var provider = host.AddComponent<TestEntityProvider>();
                provider.onDestroyType = FFS.Libraries.StaticEcs.Unity.OnDestroyType.DestroyEntity;
                var converter = new LifecycleSerializableConverter();
                provider.serializableConverters.Add(converter);

                Assert.That(provider.CreateEntity(), Is.True);
                Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain(), Is.EqualTo(1));
                var gid = provider.EntityGid;
                Assert.That(gid.TryUnpack<TestSerializableConverterWorld>(out _), Is.True);
                Assert.That(converter.ApplyCount, Is.EqualTo(1));
                Assert.That(converter.ResolveCount, Is.EqualTo(1));
                Assert.That(converter.DestroyCount, Is.Zero);
                UnityEngine.Object.DestroyImmediate(host);
                host = null;

                Assert.That(converter.ApplyCount, Is.EqualTo(1));
                Assert.That(converter.ResolveCount, Is.EqualTo(1));
                Assert.That(converter.DestroyCount, Is.EqualTo(1));
                Assert.That(gid.TryUnpack<TestSerializableConverterWorld>(out _), Is.False);
                Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Count, Is.Zero);
            }
            finally
            {
                try
                {
                    if (host != null)
                        UnityEngine.Object.DestroyImmediate(host);
                }
                finally
                {
                    try
                    {
                        EcsAuthoringRegistry<TestSerializableConverterWorld>.Clear();
                    }
                    finally
                    {
                        World<TestSerializableConverterWorld>.Destroy();
                    }
                }
            }
        }

        [Test]
        public void TransformBinding_UsesHostAndExplicitTarget()
        {
            World<TestSerializableConverterWorld>.Create(WorldConfig.Default());
            World<TestSerializableConverterWorld>.Types().Component<TransformComponent>();
            World<TestSerializableConverterWorld>.Initialize();
            var host = new GameObject("host");
            var target = new GameObject("target");
            try
            {
                var entity = World<TestSerializableConverterWorld>.NewEntity<Default>();
                var converter =
                    new TransformSerializableConverter<TestSerializableConverterWorld>();
                converter.Apply(entity, host);
                if (entity.Has<TransformComponent>())
                {
                    Assert.That(
                        entity.Read<TransformComponent>().Transform,
                        Is.SameAs(host.transform)
                    );

                    converter.Target = target.transform;
                    converter.Apply(entity, host);
                    Assert.That(
                        entity.Read<TransformComponent>().Transform,
                        Is.SameAs(target.transform)
                    );
                }
                else
                    Assert.Fail("Converter did not add TransformComponent.");
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
                var converter =
                    new EcsEntityResourceSerializableConverter<TestSerializableConverterWorld, TestEntityResource>();

                converter.Apply(entity, null);
                Assert.That(
                    World<TestSerializableConverterWorld>.GetResource<TestEntityResource>().Gid,
                    Is.EqualTo(entity.GID)
                );

                converter.OnEntityDestroyed(entity, null);
                Assert.That(
                    World<TestSerializableConverterWorld>.GetResource<TestEntityResource>().Gid,
                    Is.EqualTo(default(EntityGID))
                );
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

    [ExecuteAlways]
    internal sealed class TestEntityProvider :
        EcsEntityProvider<TestSerializableConverterWorld>
    {
    }

    [Serializable]
    internal sealed class LifecycleSerializableConverter
        : EcsSerializableConverter<TestSerializableConverterWorld>,
            IEcsLinkResolver<TestSerializableConverterWorld>,
            IEcsConverterDestroyHandler<TestSerializableConverterWorld>
    {
        public bool Enabled { get; set; } = true;
        public override bool IsEnabled => Enabled;
        public int ApplyCount { get; private set; }
        public int ResolveCount { get; private set; }
        public int DestroyCount { get; private set; }

        public override void Apply(
            World<TestSerializableConverterWorld>.Entity entity,
            GameObject host
        )
        {
            ApplyCount++;
        }

        public void ResolveLinks(
            World<TestSerializableConverterWorld>.Entity entity,
            GameObject host
        )
        {
            ResolveCount++;
        }

        public void OnEntityDestroyed(
            World<TestSerializableConverterWorld>.Entity entity,
            GameObject host
        )
        {
            DestroyCount++;
        }
    }

    internal struct TestSerializableConverterWorld : IWorldType { }

    internal struct TestEntityResource : IEcsEntityRefResource
    {
        public EntityGID Gid { get; set; }
    }

    internal struct PresetValueComponent : IComponent
    {
        public int Value;
    }

    [Serializable]
    internal sealed class PresetValueSerializableConverter
        : EcsComponentSerializableConverter<TestSerializableConverterWorld, PresetValueComponent>
    {
        public int Value { get; set; }

        protected override PresetValueComponent Build(GameObject host)
        {
            return new PresetValueComponent { Value = Value };
        }
    }
}
