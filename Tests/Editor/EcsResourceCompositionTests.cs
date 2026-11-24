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
    using UniGame.Runtime.DataFlow;
    using UnityEngine;
    using Object = UnityEngine.Object;

    [TestFixture]
    public sealed class EcsResourceCompositionTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroyWorld<ResourceWorld>();
        }

        [Test]
        public async Task ResourceHandleGetAsyncTimeoutNamesResourceType()
        {
            World<ResourceWorld>.Create(WorldConfig.Default());
            World<ResourceWorld>.Resource<SecondResource> handle = default;

            Exception exception = null;
            try
            {
                await handle.GetAsync(
                    TimeSpan.FromMilliseconds(20),
                    CancellationToken.None);
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            Assert.IsNotNull(exception, "The resource wait was expected to time out.");
            Assert.AreEqual(typeof(TimeoutException), exception.GetType());
            StringAssert.Contains(typeof(SecondResource).FullName, exception.Message);
        }

        [Test]
        public async Task ResourceHandleGetAsyncPreservesExternalCancellation()
        {
            World<ResourceWorld>.Create(WorldConfig.Default());
            World<ResourceWorld>.Resource<SecondResource> handle = default;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Exception exception = null;
            try
            {
                await handle.GetAsync(
                    TimeSpan.FromSeconds(1),
                    cancellation.Token);
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            Assert.IsNotNull(
                exception,
                "The resource wait was expected to be cancelled.");
            Assert.AreEqual(typeof(OperationCanceledException), exception.GetType());
        }

        [Test]
        public async Task FeatureResourceTimeoutReportsExactResourceAndFeature()
        {
            var asset = ScriptableObject.CreateInstance<MissingDependenciesAsset>();
            var config = StaticEcsWorldConfig.Default;
            config.editorDependencyTimeoutMs = 20;
            config.playerDependencyTimeoutMs = 20;
            var service = CreateService(config);
            try
            {
                using var context = new EntityContext();
                Exception exception = null;
                try
                {
                    await service.InitializeAsync(
                        Entries(asset),
                        context,
                        CancellationToken.None);
                }
                catch (Exception caught)
                {
                    exception = caught;
                }

                Assert.IsNotNull(
                    exception,
                    "Feature initialization was expected to time out.");
                Assert.AreEqual(typeof(TimeoutException), exception.GetType());
                StringAssert.Contains(typeof(FirstResource).FullName, exception.Message);
                Assert.AreEqual(
                    EcsStartupStage.InitializeFeatures,
                    service.Report.failedStage);
                StringAssert.Contains(asset.name, service.Report.failedFeature);
                Assert.IsFalse(asset.Initialized);
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task EarlierFeatureProvidesDependenciesToLaterFeature()
        {
            var provider = ScriptableObject.CreateInstance<ResourceProviderAsset>();
            var consumer = ScriptableObject.CreateInstance<ResourceConsumerAsset>();
            var service = CreateService(StaticEcsWorldConfig.Default);
            try
            {
                using var context = new EntityContext();
                await service.InitializeAsync(
                    Entries(provider, consumer),
                    context,
                    CancellationToken.None);

                Assert.AreEqual(
                    7,
                    World<ResourceWorld>.GetResource<FirstResource>().Value);
                Assert.IsTrue(
                    World<ResourceWorld>.HasResource<ConsumerInitializedResource>());
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(consumer);
                Object.DestroyImmediate(provider);
            }
        }

        [Test]
        public async Task ParallelModeAllowsLaterFeatureToProvideDependency()
        {
            var consumer = ScriptableObject.CreateInstance<ResourceConsumerAsset>();
            var provider = ScriptableObject.CreateInstance<ResourceProviderAsset>();
            var service = CreateService(StaticEcsWorldConfig.Default);
            try
            {
                using var context = new EntityContext();
                await service.InitializeAsync(
                    Entries(consumer, provider),
                    context,
                    CancellationToken.None);

                Assert.IsTrue(
                    World<ResourceWorld>.HasResource<ConsumerInitializedResource>());
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(provider);
                Object.DestroyImmediate(consumer);
            }
        }

        [Test]
        public async Task SequentialModeRequiresProviderBeforeConsumer()
        {
            var consumer = ScriptableObject.CreateInstance<ResourceConsumerAsset>();
            var provider = ScriptableObject.CreateInstance<ResourceProviderAsset>();
            var config = StaticEcsWorldConfig.Default;
            config.featureInitializationMode =
                StaticEcsFeatureInitializationMode.Sequential;
            config.editorDependencyTimeoutMs = 20;
            config.playerDependencyTimeoutMs = 20;
            var service = CreateService(config);
            try
            {
                using var context = new EntityContext();

                Exception exception = null;
                try
                {
                    await service.InitializeAsync(
                        Entries(consumer, provider),
                        context,
                        CancellationToken.None);
                }
                catch (Exception caught)
                {
                    exception = caught;
                }

                Assert.IsNotNull(
                    exception,
                    "Sequential feature initialization was expected to time out.");
                Assert.AreEqual(typeof(TimeoutException), exception.GetType());
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(provider);
                Object.DestroyImmediate(consumer);
            }
        }

        [Test]
        public async Task GenericAndStandaloneAssetsReceiveWorldLifetime()
        {
            var adapter = ScriptableObject.CreateInstance<AdapterAsset>();
            var standalone = ScriptableObject.CreateInstance<StandaloneAsset>();
            var service = CreateService(StaticEcsWorldConfig.Default);
            try
            {
                using var context = new EntityContext();
                adapter.feature.value = 17;

                await service.InitializeAsync(
                    Entries(adapter),
                    context,
                    CancellationToken.None);

                var adapted = World<ResourceWorld>
                    .GetResource<FeatureInitializationResource>();
                Assert.AreEqual(17, adapted.Value);
                Assert.AreSame(
                    World<ResourceWorld>.Handle.GetLifeTime(),
                    adapted.LifeTime);

                await service.InitializeAsync(
                    Entries(standalone),
                    context,
                    CancellationToken.None);

                var direct = World<ResourceWorld>
                    .GetResource<FeatureInitializationResource>();
                Assert.AreEqual(23, direct.Value);
                Assert.AreSame(
                    World<ResourceWorld>.Handle.GetLifeTime(),
                    direct.LifeTime);
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(standalone);
                Object.DestroyImmediate(adapter);
            }
        }

        [Test]
        public async Task ContextIsAvailableThroughGenericHelpers()
        {
            var asset = ScriptableObject.CreateInstance<ContextReaderAsset>();
            var service = CreateService(StaticEcsWorldConfig.Default);
            try
            {
                using var context = new EntityContext();
                await service.InitializeAsync(
                    Entries(asset),
                    context,
                    CancellationToken.None);

                Assert.AreSame(
                    context,
                    World<ResourceWorld>.GetResource<ContextReadResource>().Context);
                Assert.AreSame(context, StaticEcsContext.Get<ResourceWorld>());
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void WorldHandleLifeTimeExtensionSupportsCustomWorlds()
        {
            World<ResourceWorld>.Create(WorldConfig.Default());
            var customLifeTime = new LifeTime();
            var customResource =
                new EcsWorldLifeTimeResource(customLifeTime);
            World<ResourceWorld>.SetResource(customResource);

            Assert.AreSame(
                customLifeTime,
                World<ResourceWorld>.Handle.GetLifeTime());
            Assert.AreSame(
                customLifeTime,
                World<ResourceWorld>
                    .GetResource<EcsWorldLifeTimeResource>()
                    .LifeTime);

            customLifeTime.Terminate();
        }

        [Test]
        public void WorldHandleLifeTimeExtensionReportsInactiveWorld()
        {
            DestroyWorld<ResourceWorld>();

            var exception = Assert.Throws<InvalidOperationException>(
                static () => World<ResourceWorld>.Handle.GetLifeTime());

            StringAssert.Contains("world is not active", exception.Message);
        }

        [Test]
        public void WorldHandleLifeTimeExtensionReportsMissingBootstrapResource()
        {
            World<ResourceWorld>.Create(WorldConfig.Default());

            var exception = Assert.Throws<InvalidOperationException>(
                static () => World<ResourceWorld>.Handle.GetLifeTime());

            StringAssert.Contains(typeof(ResourceWorld).FullName, exception.Message);
            StringAssert.Contains(
                nameof(EcsWorldLifeTimeResource),
                exception.Message);
        }

        private static EcsService<ResourceWorld> CreateService(
            StaticEcsWorldConfig config)
        {
            var systems = StaticEcsSystemsConfig.Default;
            systems.update = false;
            return new EcsService<ResourceWorld>(config, systems);
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

        private static void DestroyWorld<TWorld>()
            where TWorld : struct, IWorldType
        {
            if (World<TWorld>.Status == WorldStatus.Created)
            {
                World<TWorld>.Initialize();
                World<TWorld>.Destroy(withHooks: false);
            }
            else if (World<TWorld>.Status == WorldStatus.Initialized)
                World<TWorld>.Destroy();
        }

        private struct ResourceWorld : IWorldType { }

        private struct FirstResource : IResource
        {
            public int Value;
        }

        private struct SecondResource : IResource { }

        private struct ConsumerInitializedResource : IResource { }

        private struct FeatureInitializationResource : IResource
        {
            public ILifeTime LifeTime;
            public int Value;
        }

        private sealed class ContextReadResource : IResource
        {
            public ContextReadResource(IContext context)
            {
                Context = context;
            }

            public IContext Context;
        }

        private sealed class MissingDependenciesAsset :
            StaticEcsFeatureAsset<ResourceWorld>
        {
            public bool Initialized { get; private set; }

            protected override async UniTask OnInitializeAsync(
                ILifeTime lifeTime)
            {
                World<ResourceWorld>.Resource<FirstResource> first = default;
                World<ResourceWorld>.Resource<SecondResource> second = default;

                await first.GetAsync(lifeTime);
                await second.GetAsync(lifeTime);
                Initialized = true;
            }
        }

        private sealed class ResourceProviderAsset :
            StaticEcsFeatureAsset<ResourceWorld>
        {
            protected override UniTask OnInitializeAsync(
                ILifeTime lifeTime)
            {
                var resource = new FirstResource { Value = 7 };
                World<ResourceWorld>.SetResource(resource);
                return UniTask.CompletedTask;
            }
        }

        private sealed class ResourceConsumerAsset :
            StaticEcsFeatureAsset<ResourceWorld>
        {
            protected override async UniTask OnInitializeAsync(
                ILifeTime lifeTime)
            {
                World<ResourceWorld>.Resource<FirstResource> dependency = default;
                await dependency.GetAsync(lifeTime);

                var resource = new ConsumerInitializedResource();
                World<ResourceWorld>.SetResource(resource);
            }
        }

        private sealed class ContextReaderAsset :
            StaticEcsFeatureAsset<ResourceWorld>
        {
            protected override UniTask OnInitializeAsync(
                ILifeTime lifeTime)
            {
                var resource = new ContextReadResource(
                    StaticEcsContext.Get<ResourceWorld>());
                World<ResourceWorld>.SetResource(resource);
                return UniTask.CompletedTask;
            }
        }

        [Serializable]
        private sealed class AdapterFeature :
            StaticEcsFeature<ResourceWorld>
        {
            public int value;

            public override UniTask InitializeAsync(ILifeTime lifeTime)
            {
                var resource = new FeatureInitializationResource
                {
                    LifeTime = lifeTime,
                    Value = value,
                };

                World<ResourceWorld>.SetResource(resource);
                return UniTask.CompletedTask;
            }
        }

        private sealed class AdapterAsset :
            StaticEcsFeatureAsset<ResourceWorld, AdapterFeature>
        {
        }

        private sealed class StandaloneAsset :
            StaticEcsFeatureAsset<ResourceWorld>
        {
            protected override UniTask OnInitializeAsync(ILifeTime lifeTime)
            {
                var resource = new FeatureInitializationResource
                {
                    LifeTime = lifeTime,
                    Value = 23,
                };

                World<ResourceWorld>.SetResource(resource);
                return UniTask.CompletedTask;
            }
        }
    }
}
