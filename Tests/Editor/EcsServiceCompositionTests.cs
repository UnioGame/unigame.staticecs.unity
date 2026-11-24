namespace UniGame.StaticEcs.Unity.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cysharp.Threading.Tasks;
    using FFS.Libraries.StaticEcs;
    using NUnit.Framework;
    using UniGame.Context.Runtime;
    using UniGame.Core.Runtime;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public sealed class EcsServiceCompositionTests
    {
        private static readonly List<string> Log = new();
        private static readonly List<ProbeFeature> CreatedProbes = new();
        private static ILifeTime LastProbeLifeTime;
        private EntityContext _context;

        [SetUp]
        public void SetUp()
        {
            Log.Clear();
            CreatedProbes.Clear();
            LastProbeLifeTime = null;
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
        public async Task CodeFeatureInitializesWithoutAssetAndRegistersItsAssemblyTypes()
        {
            var composition = new StaticEcsComposition<TestWorld>();
            composition.Add<ProbeFeature>();
            var service = CreateService();
            try
            {
                await service.InitializeAsync(composition, _context, CancellationToken.None);

                Assert.IsTrue(service.Report.IsSuccess, service.Report.message);
                Assert.AreEqual(1, service.Report.featureCount);
                service.Update();
                CollectionAssert.Contains(Log, "probe:system:init");
                CollectionAssert.Contains(Log, "probe:system:update");
                Assert.AreSame(LastProbeLifeTime, World<TestWorld>.Handle.GetLifeTime());
            }
            finally
            {
                service.Dispose();
            }

            CollectionAssert.Contains(Log, "probe:system:destroy");
            Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
        }

        [Test]
        public async Task CodeFeatureIsCreatedFreshForEveryInitialization()
        {
            var composition = new StaticEcsComposition<TestWorld>();
            composition.Add<ProbeFeature>();
            var service = CreateService();
            try
            {
                await service.InitializeAsync(composition, _context, CancellationToken.None);
                var firstLifeTime = LastProbeLifeTime;

                await service.InitializeAsync(composition, _context, CancellationToken.None);

                Assert.AreEqual(2, CreatedProbes.Count);
                Assert.AreNotSame(CreatedProbes[0], CreatedProbes[1]);
                Assert.IsTrue(firstLifeTime.IsTerminated);
                Assert.IsFalse(LastProbeLifeTime.IsTerminated);
                Assert.IsTrue(service.Report.IsSuccess, service.Report.message);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public async Task CodeFeatureFailureReportsFeatureAndRollsBack()
        {
            var composition = new StaticEcsComposition<TestWorld>();
            composition.Add<FailingFeature>();
            var service = CreateService();
            try
            {
                var exception = await CaptureInitializationFailure(service, composition);

                Assert.IsInstanceOf<InvalidOperationException>(exception);
                Assert.AreEqual(EcsStartupStage.InitializeFeatures, service.Report.failedStage);
                Assert.AreEqual(nameof(FailingFeature), service.Report.failedFeature);
                Assert.AreEqual(WorldStatus.NotCreated, World<TestWorld>.Status);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Test]
        public void DuplicateCodeFeatureFailsInAdd()
        {
            var composition = new StaticEcsComposition<TestWorld>();
            composition.Add<ProbeFeature>();

            var exception = Assert.Throws<InvalidOperationException>(
                () => composition.Add<ProbeFeature>());

            StringAssert.Contains(nameof(ProbeFeature), exception.Message);
            Assert.AreEqual(1, composition.Count);
        }

        [Test]
        public void CodeFeatureDuplicatingAssetAdapterFeatureTypeFailsInAdd()
        {
            var asset = ScriptableObject.CreateInstance<ProbeFeatureAsset>();
            try
            {
                var composition = new StaticEcsComposition<TestWorld>();
                composition.Add(asset);

                var exception = Assert.Throws<InvalidOperationException>(
                    () => composition.Add<ProbeFeature>());

                StringAssert.Contains(nameof(ProbeFeature), exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void SameAssetListedTwiceFailsInAdd()
        {
            var asset = ScriptableObject.CreateInstance<OrderedFeatureAsset>();
            asset.name = "ordered-asset";
            try
            {
                var composition = new StaticEcsComposition<TestWorld>();
                composition.Add(asset);

                var exception = Assert.Throws<InvalidOperationException>(
                    () => composition.Add(asset));

                StringAssert.Contains("ordered-asset", exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void NullAssetFailsInAdd()
        {
            var composition = new StaticEcsComposition<TestWorld>();

            Assert.Throws<ArgumentNullException>(() => composition.Add((StaticEcsFeatureAssetBase)null));
        }

        [Test]
        public async Task MixedCompositionStartsFeaturesInDeclarationOrder()
        {
            var asset = ScriptableObject.CreateInstance<OrderedFeatureAsset>();
            asset.name = "ordered-asset";
            var composition = new StaticEcsComposition<TestWorld>();
            composition.Add<OrderedFirstFeature>();
            composition.Add(asset);
            composition.Add<OrderedLastFeature>();
            var config = StaticEcsWorldConfig.Default;
            config.featureInitializationMode = StaticEcsFeatureInitializationMode.Sequential;
            var service = CreateService(config);
            try
            {
                await service.InitializeAsync(composition, _context, CancellationToken.None);

                CollectionAssert.AreEqual(
                    new[] { "first", "asset", "last" },
                    Log);
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task DeclaredResourceIsPublishedAsCopyBeforeFeaturesInitialize()
        {
            var asset = ScriptableObject.CreateInstance<ConfigResource>();
            asset.name = "config";
            asset.value = 7;
            ConfigResource.Read = null;
            var composition = new StaticEcsComposition<TestWorld>();
            composition.AddResource(asset);
            composition.Add<ConfigReaderFeature>();
            var service = CreateService();
            ConfigResource published;
            try
            {
                await service.InitializeAsync(composition, _context, CancellationToken.None);

                published = ConfigResource.Read;
                Assert.IsNotNull(published);
                Assert.AreNotSame(asset, published);
                Assert.AreEqual(7, published.value);
                Assert.AreSame(published, World<TestWorld>.GetResource<ConfigResource>());

                published.value = 99;
                Assert.AreEqual(7, asset.value, "The source asset must not change.");
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }

            Assert.IsTrue(published == null, "The runtime copy is destroyed with the world.");
        }

        [Test]
        public void DuplicateResourceTypeFailsInAddResource()
        {
            var first = ScriptableObject.CreateInstance<ConfigResource>();
            var second = ScriptableObject.CreateInstance<ConfigResource>();
            try
            {
                var composition = new StaticEcsComposition<TestWorld>();
                composition.AddResource(first);

                var exception = Assert.Throws<InvalidOperationException>(
                    () => composition.AddResource(second));

                StringAssert.Contains(nameof(ConfigResource), exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void NullResourceFailsInAddResource()
        {
            var composition = new StaticEcsComposition<TestWorld>();

            Assert.Throws<ArgumentNullException>(() => composition.AddResource<ConfigResource>(null));
        }

        private async Task<Exception> CaptureInitializationFailure(
            EcsService<TestWorld> service,
            StaticEcsComposition<TestWorld> composition)
        {
            try
            {
                await service.InitializeAsync(composition, _context, CancellationToken.None);
            }
            catch (Exception exception)
            {
                return exception;
            }

            Assert.Fail("Initialization was expected to fail.");
            return null;
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

        private sealed class ProbeFeature : StaticEcsFeature<TestWorld>
        {
            public ProbeFeature() => CreatedProbes.Add(this);

            public override UniTask InitializeAsync(ILifeTime lifeTime)
            {
                LastProbeLifeTime = lifeTime;
                World<TestWorld>.Systems<StaticEcsUpdateSystems>.Add(
                    new ProbeSystem(),
                    0);
                return UniTask.CompletedTask;
            }
        }

        private sealed class ConfigResource : ScriptableObject, IResource
        {
            public static ConfigResource Read;

            public int value;
        }

        private sealed class ConfigReaderFeature : StaticEcsFeature<TestWorld>
        {
            public override UniTask InitializeAsync(ILifeTime lifeTime)
            {
                ConfigResource.Read = World<TestWorld>.GetResource<ConfigResource>();
                return UniTask.CompletedTask;
            }
        }

        private sealed class FailingFeature : StaticEcsFeature<TestWorld>
        {
            public override UniTask InitializeAsync(ILifeTime lifeTime) =>
                throw new InvalidOperationException("Expected code feature failure.");
        }

        private sealed class OrderedFirstFeature : StaticEcsFeature<TestWorld>
        {
            public override UniTask InitializeAsync(ILifeTime lifeTime)
            {
                Log.Add("first");
                return UniTask.CompletedTask;
            }
        }

        private sealed class OrderedLastFeature : StaticEcsFeature<TestWorld>
        {
            public override UniTask InitializeAsync(ILifeTime lifeTime)
            {
                Log.Add("last");
                return UniTask.CompletedTask;
            }
        }

        private sealed class ProbeFeatureAsset :
            StaticEcsFeatureAsset<TestWorld, ProbeFeature>
        {
        }

        private sealed class OrderedFeatureAsset : StaticEcsFeatureAsset<TestWorld>
        {
            protected override UniTask OnInitializeAsync(ILifeTime lifeTime)
            {
                Log.Add("asset");
                return UniTask.CompletedTask;
            }
        }

        private struct ProbeSystem : ISystem
        {
            public void Init()
            {
                Log.Add("probe:system:init");
                var entity = World<TestWorld>.NewEntity<Default>();
                entity.Set(new CompositionProbeComponent());
            }

            public void Update() => Log.Add("probe:system:update");
            public void Destroy() => Log.Add("probe:system:destroy");
        }

        private struct CompositionProbeComponent : IComponent
        {
        }
    }
}
