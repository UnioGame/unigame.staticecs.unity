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
    using UnityEditor;
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
        private static bool _fixtureAborted;
        private static string _fixtureAbortReason;
        private EntityContext _context;

        [SetUp]
        public void SetUp()
        {
            if (_fixtureAborted)
                Assert.Ignore(_fixtureAbortReason);

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
            if (_fixtureAborted)
                return;

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
            Task initialization = null;
            using var initializationCancellation = new CancellationTokenSource();
            try
            {
                initialization = service.InitializeAsync(
                    Entries(first, second),
                    _context,
                    initializationCancellation.Token)
                    .AsTask();
                Exception waitFailure = null;
                Exception cleanupFailure = null;
                try
                {
                    using var waitDeadline =
                        new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    try
                    {
                        await UniTask.WaitUntil(
                            static () => Log.Contains("first:initialize:begin"),
                            cancellationToken: waitDeadline.Token);
                    }
                    catch (OperationCanceledException)
                        when (waitDeadline.IsCancellationRequested)
                    {
                        waitFailure = new TimeoutException(
                            "The first feature did not start within 10 seconds.");
                    }

                    if (waitFailure == null)
                    {
                        CollectionAssert.DoesNotContain(
                            Log,
                            "second:initialize:begin");
                        gate.TrySetResult();
                        await initialization;

                        Assert.IsTrue(service.Report.IsSuccess);
                        Assert.Less(
                            Log.IndexOf("first:initialize:end"),
                            Log.IndexOf("second:initialize:begin"));
                        Assert.Less(
                            Log.IndexOf("struct:init"),
                            Log.IndexOf("class:init"));

                        service.Update();
                        Assert.Less(
                            Log.IndexOf("struct:update"),
                            Log.IndexOf("class:update"));
                    }
                }
                finally
                {
                    gate.TrySetResult();
                    initializationCancellation.Cancel();
                    cleanupFailure = await ObserveInitializationCompletion(
                        initialization,
                        nameof(SequentialModePreservesConfiguredFeatureOrder),
                        initializationCancellation);
                }

                if (waitFailure != null)
                {
                    Assert.IsNull(
                        cleanupFailure,
                        $"Initialization cleanup failed: {cleanupFailure}");
                    Assert.Fail(waitFailure.Message);
                }

                Assert.IsNull(
                    cleanupFailure,
                    $"Initialization cleanup failed: {cleanupFailure}");
            }
            finally
            {
                if (initialization?.IsCompleted == false)
                {
                    AbortFixture(
                        "Sequential feature initialization did not complete; leaving the service, world, and runtime assets owned by the editor process.");
                }
                else
                {
                    service.Dispose();
                    Object.DestroyImmediate(second);
                    Object.DestroyImmediate(first);
                }
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
            Task initialization = null;
            using var initializationCancellation = new CancellationTokenSource();
            try
            {
                initialization = service.InitializeAsync(
                    Entries(first, second),
                    _context,
                    initializationCancellation.Token)
                    .AsTask();
                Exception waitFailure = null;
                Exception cleanupFailure = null;
                try
                {
                    using var waitDeadline =
                        new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    try
                    {
                        await UniTask.WaitUntil(
                            static () =>
                                Log.Contains("parallel-first:initialize:begin") &&
                                Log.Contains("parallel-second:initialize:end"),
                            cancellationToken: waitDeadline.Token);
                    }
                    catch (OperationCanceledException)
                        when (waitDeadline.IsCancellationRequested)
                    {
                        waitFailure = new TimeoutException(
                            "Parallel feature initialization did not overlap within 10 seconds.");
                    }

                    if (waitFailure == null)
                    {
                        CollectionAssert.DoesNotContain(
                            Log,
                            "parallel-first:initialize:end");
                        gate.TrySetResult();
                        await initialization;

                        Assert.IsTrue(service.Report.IsSuccess);
                    }
                }
                finally
                {
                    gate.TrySetResult();
                    initializationCancellation.Cancel();
                    cleanupFailure = await ObserveInitializationCompletion(
                        initialization,
                        nameof(ParallelModeOverlapsFeatureInitializationByDefault),
                        initializationCancellation);
                }

                if (waitFailure != null)
                {
                    Assert.IsNull(
                        cleanupFailure,
                        $"Initialization cleanup failed: {cleanupFailure}");
                    Assert.Fail(waitFailure.Message);
                }

                Assert.IsNull(
                    cleanupFailure,
                    $"Initialization cleanup failed: {cleanupFailure}");
            }
            finally
            {
                if (initialization?.IsCompleted == false)
                {
                    AbortFixture(
                        "Parallel feature initialization did not complete; leaving the service, world, and runtime assets owned by the editor process.");
                }
                else
                {
                    service.Dispose();
                    Object.DestroyImmediate(second);
                    Object.DestroyImmediate(first);
                }
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
        public async Task UpdateDependentFeatureWithoutUpdateGroupRollsBack()
        {
            var asset = CreateAsset("requires-update", addSystems: true);
            var systems = StaticEcsSystemsConfig.Default;
            systems.update = false;
            var service = new EcsService<TestWorld>(
                StaticEcsWorldConfig.Default,
                systems);
            try
            {
                var initializationFailed = false;
                try
                {
                    await service.InitializeAsync(
                        Entries(asset),
                        _context,
                        CancellationToken.None);
                }
                catch (Exception)
                {
                    initializationFailed = true;
                }

                Assert.IsTrue(
                    initializationFailed,
                    "Initialization was expected to fail without an update group.");
                Assert.AreEqual(
                    EcsStartupStage.InitializeFeatures,
                    service.Report.failedStage);
                Assert.AreEqual("requires-update", service.Report.failedFeature);
                Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
                Assert.AreEqual(0, RuntimeAssets.Count);
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task CancellationRollsBackWorldAndRuntimeAssetClone()
        {
            var asset = CreateAsset("cancelled", addSystems: false);
            var service = CreateService();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var initializationCancelled = false;
            try
            {
                await service.InitializeAsync(
                    Entries(asset),
                    _context,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                initializationCancelled = true;
            }

            Assert.IsTrue(
                initializationCancelled,
                "Initialization was expected to be cancelled.");
            Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
            Assert.AreEqual(0, RuntimeAssets.Count);
            service.Dispose();
            Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task InitializationFailureReportsFeatureStageAndRollsBack()
        {
            var asset = CreateAsset("broken", addSystems: false);
            asset.failInitialization = true;
            var service = CreateService();

            Exception exception = null;
            try
            {
                await service.InitializeAsync(
                    Entries(asset),
                    _context,
                    CancellationToken.None);
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            Assert.IsNotNull(
                exception,
                "Initialization was expected to fail for the broken feature.");
            Assert.AreEqual(typeof(InvalidOperationException), exception.GetType());
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
            Task initialization = null;
            using var cancellation = new CancellationTokenSource();
            try
            {
                initialization = service.InitializeAsync(
                    Entries(asset, nextAsset),
                    _context,
                    cancellation.Token)
                    .AsTask();
                Exception waitFailure = null;
                Exception cleanupFailure = null;
                try
                {
                    using var waitDeadline =
                        new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    try
                    {
                        await UniTask.WaitUntil(
                            static () => LastWorldLifeTime != null,
                            cancellationToken: waitDeadline.Token);
                    }
                    catch (OperationCanceledException)
                        when (waitDeadline.IsCancellationRequested)
                    {
                        waitFailure = new TimeoutException(
                            "The world lifetime was not created within 10 seconds.");
                    }

                    if (waitFailure == null)
                    {
                        var worldLifeTime = LastWorldLifeTime;
                        cancellation.Cancel();
                        var initializationCancelled = false;
                        try
                        {
                            await initialization;
                        }
                        catch (OperationCanceledException)
                        {
                            initializationCancelled = true;
                        }

                        Assert.IsTrue(
                            initializationCancelled,
                            "Initialization was expected to be cancelled.");
                        Assert.IsTrue(worldLifeTime.IsTerminated);
                        Assert.AreEqual(
                            WorldStatus.NotCreated,
                            World<TestWorld>.Status);
                        CollectionAssert.DoesNotContain(
                            Log,
                            "must-not-initialize:initialize:begin");
                    }
                }
                finally
                {
                    cancellation.Cancel();
                    cleanupFailure = await ObserveInitializationCompletion(
                        initialization,
                        nameof(CancellationDuringFeatureInitializationTerminatesWorldLifeTime),
                        allowCancellation: true);
                }

                if (waitFailure != null)
                {
                    Assert.IsNull(
                        cleanupFailure,
                        $"Initialization cleanup failed: {cleanupFailure}");
                    Assert.Fail(waitFailure.Message);
                }

                Assert.IsNull(
                    cleanupFailure,
                    $"Initialization cleanup failed: {cleanupFailure}");
            }
            finally
            {
                if (initialization?.IsCompleted == false)
                {
                    AbortFixture(
                        "Cancellation feature initialization did not complete; leaving the service, world, and runtime assets owned by the editor process.");
                }
                else
                {
                    service.Dispose();
                    Object.DestroyImmediate(nextAsset);
                    Object.DestroyImmediate(asset);
                }
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
            var serviceDisposed = false;
            var assetDestroyed = false;
            try
            {
                await service.InitializeAsync(
                    Entries(asset),
                    _context,
                    CancellationToken.None);
                LogAssert.Expect(
                    LogType.Error,
                    new Regex("Static ECS cleanup failed for Update systems"));

                service.Dispose();
                serviceDisposed = true;

                Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
                Assert.AreEqual(0, RuntimeAssets.Count);
                CollectionAssert.Contains(Log, "cleanup-failure:asset:destroy");
                Object.DestroyImmediate(asset);
                assetDestroyed = true;
            }
            finally
            {
                try
                {
                    if (!serviceDisposed)
                    {
                        service.Dispose();
                        serviceDisposed = true;
                    }
                }
                finally
                {
                    try
                    {
                        if (!assetDestroyed)
                        {
                            Object.DestroyImmediate(asset);
                            assetDestroyed = true;
                        }
                    }
                    finally
                    {
                        ResetResidualUpdateSystems();
                    }
                }
            }
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

        private static async Task<Exception> ObserveInitializationCompletion(
            Task initialization,
            string testName,
            CancellationTokenSource cancellation = null,
            bool allowCancellation = false)
        {
            using var observationDeadline =
                new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await UniTask.WaitUntil(
                    () => initialization.IsCompleted,
                    cancellationToken: observationDeadline.Token);
            }
            catch (OperationCanceledException)
                when (!initialization.IsCompleted)
            {
                cancellation?.Cancel();
                using var cancellationDeadline =
                    new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    await UniTask.WaitUntil(
                        () => initialization.IsCompleted,
                        cancellationToken: cancellationDeadline.Token);
                }
                catch (OperationCanceledException)
                    when (!initialization.IsCompleted)
                {
                    return new TimeoutException(
                        $"Initialization cleanup for {testName} did not complete within 10 seconds.");
                }
            }

            try
            {
                await initialization;
            }
            catch (Exception exception)
            {
                if (!allowCancellation || exception is not OperationCanceledException)
                    return exception;
            }

            return null;
        }

        private static void AbortFixture(string reason)
        {
            _fixtureAborted = true;
            _fixtureAbortReason = reason;
            Debug.LogError(reason);
            // A pending initialization still owns the service, world, and runtime assets.
            // Keep them reachable in the interactive editor and skip fixture teardown.
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        private static void ResetResidualUpdateSystems()
        {
            var systems = World<TestWorld>.Systems<StaticEcsUpdateSystems>.Handle;
            if (!systems.IsValid || systems.Status() == SystemsStatus.NotCreated)
                return;

            if (World<TestWorld>.Status != WorldStatus.NotCreated)
                throw new InvalidOperationException(
                    "Cannot reset residual update systems while the test world is still alive.");

            var temporaryWorldCreated = false;
            try
            {
                World<TestWorld>.Create(WorldConfig.Default());
                temporaryWorldCreated = true;
                World<TestWorld>.Initialize();
                World<TestWorld>.Systems<StaticEcsUpdateSystems>.Destroy();

                if (World<TestWorld>.Systems<StaticEcsUpdateSystems>.Handle.IsValid)
                    throw new InvalidOperationException(
                        "Residual update systems remained registered after cleanup.");
            }
            finally
            {
                if (temporaryWorldCreated &&
                    World<TestWorld>.Status != WorldStatus.NotCreated)
                    World<TestWorld>.Destroy(withHooks: false);
            }
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
                World<TestWorld>.Destroy();
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
                    await gate.Task.AttachExternalCancellation(lifeTime.Token);

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
                    World<TestWorld>.Systems<StaticEcsUpdateSystems>.Add(
                        new ThrowingDestroySystem(),
                        2);

                if (failInitialization)
                    throw new InvalidOperationException($"{id} failed.");

                Log.Add($"{id}:initialize:end");
            }

            private void OnDestroy()
            {
                RuntimeAssets.Remove(this);
                if ((hideFlags & HideFlags.DontSave) != 0)
                    Log.Add($"{id}:asset:destroy");
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
            private bool _hasThrown;

            public void Destroy()
            {
                if (_hasThrown)
                    return;

                _hasThrown = true;
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
