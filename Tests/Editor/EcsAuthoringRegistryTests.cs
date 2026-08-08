namespace UniGame.StaticEcs.Unity.Tests
{
    using System;
    using System.Collections.Generic;
    using FFS.Libraries.StaticEcs;
    using NUnit.Framework;
    using UnityEngine;

    /// <summary>Verifies centralized provider creation, rollback and stale-state recovery.</summary>
    [TestFixture]
    public sealed class EcsAuthoringRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            World<TestSerializableConverterWorld>.Create(WorldConfig.Default());
            World<TestSerializableConverterWorld>.Initialize();
            EcsAuthoringRegistry<TestSerializableConverterWorld>.BeginWorld();
        }

        [TearDown]
        public void TearDown()
        {
            EcsAuthoringRegistry<TestSerializableConverterWorld>.Clear();
            if (World<TestSerializableConverterWorld>.Status != WorldStatus.NotCreated)
                World<TestSerializableConverterWorld>.Destroy();
            foreach (var provider in UnityEngine.Object.FindObjectsByType<TestEntityProvider>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (provider != null)
                    UnityEngine.Object.DestroyImmediate(provider.gameObject);
        }

        [Test]
        public void CreateEntity_QueuesUntilDrain()
        {
            var provider = new GameObject("queued").AddComponent<TestEntityProvider>();

            Assert.That(provider.CreateEntity(), Is.True);
            Assert.That(provider.EntityGid, Is.EqualTo(default(EntityGID)));
            Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain(), Is.EqualTo(1));
            Assert.That(provider.EntityGid.TryUnpack<TestSerializableConverterWorld>(out _), Is.True);
        }

        [Test]
        public void DirectEntityDestroy_RequeuesPersistentProvider()
        {
            var provider = new GameObject("stale").AddComponent<TestEntityProvider>();
            provider.CreateEntity();
            EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain();
            var previous = provider.EntityGid;
            Assert.That(previous.TryUnpack<TestSerializableConverterWorld>(out var entity), Is.True);

            entity.Destroy();
            Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain(), Is.EqualTo(1));

            Assert.That(provider.EntityGid, Is.Not.EqualTo(previous));
            Assert.That(provider.EntityGid.TryUnpack<TestSerializableConverterWorld>(out _), Is.True);
        }

        [Test]
        public void ResolveFailure_RollsBackWholeCreatedBatch()
        {
            var first = new GameObject("first").AddComponent<TestEntityProvider>();
            var second = new GameObject("second").AddComponent<TestEntityProvider>();
            first.serializableConverters.Add(new LifecycleSerializableConverter());
            second.serializableConverters.Add(new ThrowingLinkConverter());
            first.CreateEntity();
            second.CreateEntity();

            Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain(), Is.Zero);
            Assert.That(first.EntityGid, Is.EqualTo(default(EntityGID)));
            Assert.That(second.EntityGid, Is.EqualTo(default(EntityGID)));
            Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.ActiveCount, Is.Zero);
            Assert.That(
                EcsAuthoringRegistry<TestSerializableConverterWorld>.TryGetDiagnostic(
                    second, out var diagnostic), Is.True);
            Assert.That(diagnostic, Does.Contain("rolled back"));
        }

        [Test]
        public void ImmediateCreation_IsAtomicAndVisible()
        {
            var provider = new GameObject("immediate").AddComponent<TestEntityProvider>();

            Assert.That(
                EcsAuthoringRegistry<TestSerializableConverterWorld>.TryCreateImmediate(
                    provider, out var gid, out var reason), Is.True, reason);
            Assert.That(gid.TryUnpack<TestSerializableConverterWorld>(out _), Is.True);
            Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void DependencyWave_CreatesPrerequisiteBeforeEarlierSortedDependent()
        {
            var order = new List<string>();
            var dependent = new GameObject("00 dependent").AddComponent<TestEntityProvider>();
            var prerequisite = new GameObject("99 prerequisite").AddComponent<TestEntityProvider>();
            dependent.serializableConverters.Add(
                new DependentConverter(prerequisite, order));
            prerequisite.serializableConverters.Add(new OrderedConverter("prerequisite", order));
            dependent.CreateEntity();
            prerequisite.CreateEntity();

            Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain(), Is.EqualTo(2));
            Assert.That(order, Is.EqualTo(new[] { "prerequisite", "dependent" }));
            Assert.That(dependent.EntityGid.TryUnpack<TestSerializableConverterWorld>(out _), Is.True);
        }

        [Test]
        public void RequestDestroy_RunsDestroyHookAndDoesNotRecreate()
        {
            var provider = new GameObject("destroy").AddComponent<TestEntityProvider>();
            var converter = new LifecycleSerializableConverter();
            provider.serializableConverters.Add(converter);
            provider.CreateEntity();
            EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain();

            Assert.That(provider.DestroyEntity(), Is.True);

            Assert.That(provider.EntityGid, Is.EqualTo(default(EntityGID)));
            Assert.That(converter.DestroyCount, Is.EqualTo(1));
            Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain(), Is.Zero);
            Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Count, Is.Zero);
        }

        [Test]
        public void Restart_ReplaysEnabledPersistentIntent()
        {
            var provider = new GameObject("restart").AddComponent<TestEntityProvider>();
            var converter = new LifecycleSerializableConverter();
            provider.serializableConverters.Add(converter);
            provider.CreateEntity();
            EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain();
            Assert.That(converter.ApplyCount, Is.EqualTo(1));

            EcsAuthoringRegistry<TestSerializableConverterWorld>.EndWorld();
            Assert.That(provider.EntityGid, Is.EqualTo(default(EntityGID)));
            Assert.That(converter.DestroyCount, Is.EqualTo(1));
            World<TestSerializableConverterWorld>.Destroy();
            World<TestSerializableConverterWorld>.Create(WorldConfig.Default());
            World<TestSerializableConverterWorld>.Initialize();
            EcsAuthoringRegistry<TestSerializableConverterWorld>.BeginWorld();

            Assert.That(EcsAuthoringRegistry<TestSerializableConverterWorld>.Drain(), Is.EqualTo(1));
            Assert.That(converter.ApplyCount, Is.EqualTo(2));
            Assert.That(provider.EntityGid.TryUnpack<TestSerializableConverterWorld>(out _), Is.True);
        }

        [Serializable]
        private sealed class ThrowingLinkConverter :
            EcsSerializableConverter<TestSerializableConverterWorld>,
            IEcsLinkResolver<TestSerializableConverterWorld>
        {
            public override void Apply(
                World<TestSerializableConverterWorld>.Entity entity,
                GameObject host)
            {
            }

            public void ResolveLinks(
                World<TestSerializableConverterWorld>.Entity entity,
                GameObject host) =>
                throw new InvalidOperationException("link failure");
        }

        [Serializable]
        private sealed class OrderedConverter :
            EcsSerializableConverter<TestSerializableConverterWorld>
        {
            private readonly string _name;
            private readonly List<string> _order;

            internal OrderedConverter(string name, List<string> order)
            {
                _name = name;
                _order = order;
            }

            public override void Apply(
                World<TestSerializableConverterWorld>.Entity entity,
                GameObject host) =>
                _order.Add(_name);
        }

        [Serializable]
        private sealed class DependentConverter :
            EcsSerializableConverter<TestSerializableConverterWorld>,
            IEcsConverterDependency<TestSerializableConverterWorld>
        {
            private readonly TestEntityProvider _prerequisite;
            private readonly List<string> _order;

            internal DependentConverter(
                TestEntityProvider prerequisite,
                List<string> order)
            {
                _prerequisite = prerequisite;
                _order = order;
            }

            public bool IsReady(GameObject host, out string reason)
            {
                var ready = _prerequisite != null &&
                            _prerequisite.EntityGid.TryUnpack<TestSerializableConverterWorld>(out _);
                reason = ready ? string.Empty : "Prerequisite is pending.";
                return ready;
            }

            public override void Apply(
                World<TestSerializableConverterWorld>.Entity entity,
                GameObject host) =>
                _order.Add("dependent");
        }
    }
}
