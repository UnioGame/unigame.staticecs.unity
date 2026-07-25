namespace UniGame.StaticEcs.Unity.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Cysharp.Threading.Tasks;
    using FFS.Libraries.StaticEcs;
    using NUnit.Framework;
    using UniGame.Context.Runtime;
    using UniGame.Core.Runtime;
    using UnityEngine;
    using UnityEngine.TestTools;
    using Object = UnityEngine.Object;

    public sealed class EcsServiceLifecycleTests
    {
        private static readonly List<string> Log = new();
        private static readonly Dictionary<string, UniTaskCompletionSource> Gates = new();
        private static readonly List<LifecycleFeatureAsset> RuntimeAssets = new();
        private static ILifeTime LastWorldLifeTime;
        private static ILifeTime LastFeatureLifeTime;
        private static WorldStatus LastWorldStatus;
        private EntityContext _context;

        [SetUp]
        public void SetUp()
        {
            Log.Clear();
            Gates.Clear();
            RuntimeAssets.Clear();
            LastWorldLifeTime = null;
            LastFeatureLifeTime = null;
            LastWorldStatus = WorldStatus.NotCreated;
            _context = new EntityContext();
            DestroyWorld();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyWorld();
            _context?.Dispose();
        }

        [Test]
        public async Task SequentialModePreservesConfiguredFeatureOrder()
        {
            var gate = new UniTaskCompletionSource();
            Gates["first"] = gate;
            var first = CreateAsset("first", addSystems: true);
            var second = CreateAsset("second", addSystems: false);
            var config = StaticEcsWorldConfig.Default;
            config.featureInitializationMode =
                StaticEcsFeatureInitializationMode.Sequential;
            var service = CreateService(config);
            try
            {
                var initialization = service.InitializeAsync(
                    Entries(first, second),
                    _context,
                    CancellationToken.None);
                await UniTask.WaitUntil(
                    static () => Log.Contains("first:initialize:begin"));

                CollectionAssert.DoesNotContain(Log, "second:initialize:begin");
                gate.TrySetResult();
                await initialization;

                Assert.IsTrue(service.Report.IsSuccess);
                Assert.Less(
                    Log.IndexOf("first:initialize:end"),
                    Log.IndexOf("second:initialize:begin"));
                Assert.Less(Log.IndexOf("struct:init"), Log.IndexOf("class:init"));

                service.Update();
                Assert.Less(Log.IndexOf("struct:update"), Log.IndexOf("class:update"));
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(first);
            }

            CollectionAssert.Contains(Log, "class:destroy");
            CollectionAssert.Contains(Log, "struct:destroy");
            Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
        }

        [Test]
        public async Task ParallelModeOverlapsFeatureInitializationByDefault()
        {
            var gate = new UniTaskCompletionSource();
            Gates["parallel-first"] = gate;
            var first = CreateAsset("parallel-first", addSystems: false);
            var second = CreateAsset("parallel-second", addSystems: false);
            var service = CreateService();
            try
            {
                var initialization = service.InitializeAsync(
                    Entries(first, second),
                    _context,
                    CancellationToken.None);
                await UniTask.WaitUntil(
                    static () =>
                        Log.Contains("parallel-first:initialize:begin") &&
                        Log.Contains("parallel-second:initialize:end"));

                CollectionAssert.DoesNotContain(
                    Log,
                    "parallel-first:initialize:end");
                gate.TrySetResult();
                await initialization;

                Assert.IsTrue(service.Report.IsSuccess);
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(first);
            }
        }

        [Test]
        public async Task AdvanceTickWorksWithoutSystemGroups()
        {
            var systems = StaticEcsSystemsConfig.Default;
            systems.update = false;
            var service = new EcsService<TestWorld>(
                StaticEcsWorldConfig.Default,
                systems);
            try
            {
                await service.InitializeAsync(
                    Array.Empty<StaticEcsFeatureEntry>(),
                    _context,
                    CancellationToken.None);

                service.Update();
                Assert.AreEqual(0, service.Report.updateCount);
                service.AdvanceTick();
                Assert.AreEqual(1, service.Report.updateCount);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public void CancellationRollsBackWorldAndRuntimeAssetClone()
        {
            var asset = CreateAsset("cancelled", addSystems: false);
            var service = CreateService();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () =>
                await service.InitializeAsync(
                    Entries(asset),
                    _context,
                    cancellation.Token));

            Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
            Assert.AreEqual(0, RuntimeAssets.Count);
            service.Dispose();
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void InitializationFailureReportsFeatureStageAndRollsBack()
        {
            var asset = CreateAsset("broken", addSystems: false);
            asset.failInitialization = true;
            var service = CreateService();

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await service.InitializeAsync(
                    Entries(asset),
                    _context,
                    CancellationToken.None));

            Assert.AreEqual(EcsStartupStage.InitializeFeatures, service.Report.failedStage);
            StringAssert.Contains("broken", service.Report.failedFeature);
            Assert.IsNotNull(LastWorldLifeTime);
            Assert.IsTrue(LastWorldLifeTime.IsTerminated);
            Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
            service.Dispose();
            Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task RuntimeAssetIsClonedFreshForEveryInitialization()
        {
            var asset = CreateAsset("repeat", addSystems: false);
            var service = CreateService();
            try
            {
                await service.InitializeAsync(
                    Entries(asset),
                    _context,
                    CancellationToken.None);
                var first = RuntimeAssets[0];

                RuntimeAssets.Clear();
                await service.InitializeAsync(
                    Entries(asset),
                    _context,
                    CancellationToken.None);
                var second = RuntimeAssets[0];

                Assert.AreNotSame(asset, first);
                Assert.AreNotSame(asset, second);
                Assert.AreNotSame(first, second);
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task WorldLifeTimeIsAvailableDuringInitializationAndRecreated()
        {
            var asset = CreateAsset("lifetime", addSystems: false);
            var service = CreateService();
            var cleanupCount = 0;
            ILifeTime secondLifeTime = null;
            try
            {
                await service.InitializeAsync(
                    Entries(asset),
                    _context,
                    CancellationToken.None);

                var firstLifeTime = LastWorldLifeTime;
                Assert.IsNotNull(firstLifeTime);
                Assert.AreSame(firstLifeTime, LastFeatureLifeTime);
                Assert.AreEqual(WorldStatus.Created, LastWorldStatus);
                Assert.IsFalse(firstLifeTime.IsTerminated);
                Assert.AreSame(firstLifeTime, World<TestWorld>.Handle.GetLifeTime());
                Assert.AreSame(
                    firstLifeTime,
                    World<TestWorld>
                        .GetResource<EcsWorldLifeTimeResource>()
                        .LifeTime);
                firstLifeTime.AddCleanUpAction(() => cleanupCount++);

                RuntimeAssets.Clear();
                await service.InitializeAsync(
                    Entries(asset),
                    _context,
                    CancellationToken.None);

                secondLifeTime = LastWorldLifeTime;
                Assert.IsTrue(firstLifeTime.IsTerminated);
                Assert.AreEqual(1, cleanupCount);
                Assert.AreNotSame(firstLifeTime, secondLifeTime);
                Assert.IsFalse(secondLifeTime.IsTerminated);
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }

            Assert.IsNotNull(secondLifeTime);
            Assert.IsTrue(secondLifeTime.IsTerminated);
            Assert.AreEqual(1, cleanupCount);
            Assert.IsFalse(_context.LifeTime.IsTerminated);
        }

        [Test]
        public async Task CompletedStartupTokenDoesNotOwnRunningWorldLifeTime()
        {
            var asset = CreateAsset("detached-startup-token", addSystems: false);
            var service = CreateService();
            using var cancellation = new CancellationTokenSource();
            try
            {
                await service.InitializeAsync(
                    Entries(asset),
                    _context,
                    cancellation.Token);
                var worldLifeTime = World<TestWorld>.Handle.GetLifeTime();

                cancellation.Cancel();

                Assert.IsFalse(worldLifeTime.IsTerminated);
                Assert.AreEqual(WorldStatus.Initialized, World<TestWorld>.Status);
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task CancellationDuringFeatureInitializationTerminatesWorldLifeTime()
        {
            var gate = new UniTaskCompletionSource();
            Gates["cancel-during-initialize"] = gate;
            var asset = CreateAsset("cancel-during-initialize", addSystems: false);
            var nextAsset = CreateAsset("must-not-initialize", addSystems: false);
            var config = StaticEcsWorldConfig.Default;
            config.featureInitializationMode =
                StaticEcsFeatureInitializationMode.Sequential;
            var service = CreateService(config);
            using var cancellation = new CancellationTokenSource();
            try
            {
                var initialization = service.InitializeAsync(
                    Entries(asset, nextAsset),
                    _context,
                    cancellation.Token);
                await UniTask.WaitUntil(static () => LastWorldLifeTime != null);
                var worldLifeTime = LastWorldLifeTime;

                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>(async () =>
                    await initialization);

                Assert.IsTrue(worldLifeTime.IsTerminated);
                Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
                CollectionAssert.DoesNotContain(
                    Log,
                    "must-not-initialize:initialize:begin");
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(nextAsset);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task TeardownOrderIsLifeTimeSystemsWorldThenRuntimeAsset()
        {
            var asset = CreateAsset("teardown-order", addSystems: true);
            var service = CreateService();
            await service.InitializeAsync(
                Entries(asset),
                _context,
                CancellationToken.None);
            var worldLifeTime = World<TestWorld>.Handle.GetLifeTime();
            worldLifeTime.AddCleanUpAction(() => Log.Add("lifetime:destroy"));

            service.Dispose();
            Object.DestroyImmediate(asset);

            Assert.Less(
                Log.IndexOf("lifetime:destroy"),
                Log.IndexOf("struct:destroy"));
            Assert.Less(
                Log.IndexOf("class:destroy"),
                Log.IndexOf("world:destroy"));
            Assert.Less(
                Log.IndexOf("world:destroy"),
                Log.IndexOf("teardown-order:asset:destroy"));
        }

        [Test]
        public async Task CleanupFailureDoesNotBlockWorldAndRuntimeAssetTeardown()
        {
            var asset = CreateAsset("cleanup-failure", addSystems: false);
            asset.addThrowingSystem = true;
            var service = CreateService();
            await service.InitializeAsync(
                Entries(asset),
                _context,
                CancellationToken.None);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Static ECS cleanup failed for Update systems"));

            service.Dispose();

            Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
            Assert.AreEqual(0, RuntimeAssets.Count);
            CollectionAssert.Contains(Log, "cleanup-failure:asset:destroy");
            Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task LifeTimeCleanupFailureDoesNotBlockSystemsAndWorldTeardown()
        {
            var asset = CreateAsset("lifetime-cleanup-failure", addSystems: true);
            var service = CreateService();
            await service.InitializeAsync(
                Entries(asset),
                _context,
                CancellationToken.None);
            World<TestWorld>.Handle.GetLifeTime().AddCleanUpAction(
                static () => throw new InvalidOperationException(
                    "Expected lifetime cleanup failure."));
            LogAssert.Expect(
                LogType.Error,
                new Regex("Expected lifetime cleanup failure"));

            service.Dispose();

            CollectionAssert.Contains(Log, "struct:destroy");
            CollectionAssert.Contains(Log, "class:destroy");
            CollectionAssert.Contains(Log, "world:destroy");
            CollectionAssert.Contains(Log, "lifetime-cleanup-failure:asset:destroy");
            Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
            Object.DestroyImmediate(asset);
        }

        private static LifecycleFeatureAsset CreateAsset(string id, bool addSystems)
        {
            var asset = ScriptableObject.CreateInstance<LifecycleFeatureAsset>();
            asset.id = id;
            asset.addSystems = addSystems;
            return asset;
        }

        private static List<StaticEcsFeatureEntry> Entries(
            params StaticEcsFeatureAssetBase[] assets)
        {
            var entries = new List<StaticEcsFeatureEntry>(assets.Length);
            for (var i = 0; i < assets.Length; i++)
            {
                entries.Add(new StaticEcsFeatureEntry
                {
                    enabled = true,
                    asset = assets[i],
                });
            }

            return entries;
        }

        private static EcsService<TestWorld> CreateService(
            StaticEcsWorldConfig? worldConfig = null)
        {
            var systems = StaticEcsSystemsConfig.Default;
            systems.update = true;
            systems.fixedUpdate = false;
            systems.lateUpdate = false;
            systems.cleanup = false;
            return new EcsService<TestWorld>(
                worldConfig ?? StaticEcsWorldConfig.Default,
                systems);
        }

        private static void DestroyWorld()
        {
            if (World<TestWorld>.Status == WorldStatus.Created)
            {
                World<TestWorld>.Initialize();
                World<TestWorld>.Destroy(withHooks: false);
            }
            else if (World<TestWorld>.Status == WorldStatus.Initialized)
            {
                World<TestWorld>.Destroy();
            }
        }

        private struct TestWorld : IWorldType { }

        private sealed class LifecycleFeatureAsset :
            StaticEcsFeatureAsset<TestWorld>
        {
            public string id;
            public bool addSystems;
            public bool addThrowingSystem;
            public bool failInitialization;

            public override string FeatureName => id;

            protected override async UniTask OnInitializeAsync(
                ILifeTime lifeTime)
            {
                RuntimeAssets.Add(this);
                LastFeatureLifeTime = lifeTime;
                LastWorldStatus = World<TestWorld>.Status;
                LastWorldLifeTime = World<TestWorld>.Handle.GetLifeTime();
                Log.Add($"{id}:initialize:begin");
                if (Gates.TryGetValue(id, out var gate))
                {
                    await gate.Task.AttachExternalCancellation(lifeTime.Token);
                }

                if (addSystems)
                {
                    World<TestWorld>.Systems<StaticEcsUpdateSystems>.Add(
                        new StructSystem(),
                        0);
                    World<TestWorld>.Systems<StaticEcsUpdateSystems>.Add(
                        new ClassSystem(),
                        1);
                }

                if (addThrowingSystem)
                {
                    World<TestWorld>.Systems<StaticEcsUpdateSystems>.Add(
                        new ThrowingDestroySystem(),
                        2);
                }

                if (failInitialization)
                {
                    throw new InvalidOperationException($"{id} failed.");
                }

                Log.Add($"{id}:initialize:end");
            }

            private void OnDestroy()
            {
                RuntimeAssets.Remove(this);
                if ((hideFlags & HideFlags.DontSave) != 0)
                {
                    Log.Add($"{id}:asset:destroy");
                }
            }
        }

        private struct StructSystem : ISystem
        {
            public void Init()
            {
                Log.Add("struct:init");
                var entity = World<TestWorld>.NewEntity<Default>();
                entity.Set(new LifecycleProbeComponent());
            }

            public void Update() => Log.Add("struct:update");
            public void Destroy() => Log.Add("struct:destroy");
        }

        private sealed class ClassSystem : ISystem
        {
            public void Init() => Log.Add("class:init");
            public void Update() => Log.Add("class:update");
            public void Destroy() => Log.Add("class:destroy");
        }

        private sealed class ThrowingDestroySystem : ISystem
        {
            public void Destroy()
            {
                throw new InvalidOperationException("Expected cleanup failure.");
            }
        }

        private struct LifecycleProbeComponent : IComponent
        {
            public void OnDelete<TWorld>(
                World<TWorld>.Entity entity,
                HookReason reason)
                where TWorld : struct, IWorldType
            {
                Log.Add("world:destroy");
            }
        }
    }
}
