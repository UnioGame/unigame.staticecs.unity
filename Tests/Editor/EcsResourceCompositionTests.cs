namespace UniGame.StaticEcs.Unity.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using FFS.Libraries.StaticEcs;
    using NUnit.Framework;
    using UniGame.Context.Runtime;
    using UniGame.Core.Runtime;
    using UniGame.Runtime.DataFlow;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    [TestFixture]
    public sealed class EcsResourceCompositionTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroyWorld<ResourceWorld>();
            DestroyWorld<Main>();
        }

        [Test]
        public async UniTask ResourceHandleGetAsyncWaitsWithoutClosureState()
        {
            World<ResourceWorld>.Create(WorldConfig.Default());
            World<ResourceWorld>.Resource<FirstResource> handle = default;
            var pending = handle.GetAsync(
                TimeSpan.FromSeconds(1),
                CancellationToken.None);

            await UniTask.Yield();
            var resource = new FirstResource { Value = 42 };
            World<ResourceWorld>.SetResource(resource);

            var result = await pending;
            Assert.AreEqual(42, result.Value);
        }

        [Test]
        public void DependencyTimeoutDefaultsMatchEditorAndPlayerPolicy()
        {
            var config = StaticEcsWorldConfig.Default;

            Assert.AreEqual(5000, config.editorDependencyTimeoutMs);
            Assert.AreEqual(10000, config.playerDependencyTimeoutMs);
            Assert.AreEqual(
                StaticEcsFeatureInitializationMode.Parallel,
                config.featureInitializationMode);
        }

        [Test]
        public void ResourceHandleGetAsyncTimeoutNamesResourceType()
        {
            World<ResourceWorld>.Create(WorldConfig.Default());
            World<ResourceWorld>.Resource<SecondResource> handle = default;

            var exception = Assert.ThrowsAsync<TimeoutException>(async () =>
                await handle.GetAsync(
                    TimeSpan.FromMilliseconds(20),
                    CancellationToken.None));

            StringAssert.Contains(typeof(SecondResource).FullName, exception.Message);
        }

        [Test]
        public void ResourceHandleGetAsyncPreservesExternalCancellation()
        {
            World<ResourceWorld>.Create(WorldConfig.Default());
            World<ResourceWorld>.Resource<SecondResource> handle = default;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await handle.GetAsync(
                    TimeSpan.FromSeconds(1),
                    cancellation.Token));
        }

        [Test]
        public void FeatureResourceTimeoutReportsExactResourceAndFeature()
        {
            var asset = ScriptableObject.CreateInstance<MissingDependenciesAsset>();
            var config = StaticEcsWorldConfig.Default;
            config.editorDependencyTimeoutMs = 20;
            config.playerDependencyTimeoutMs = 20;
            var service = CreateService(config);
            try
            {
                using var context = new EntityContext();
                var exception = Assert.ThrowsAsync<TimeoutException>(async () =>
                    await service.InitializeAsync(
                        Entries(asset),
                        context,
                        CancellationToken.None));

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
        public async UniTask EarlierFeatureProvidesDependenciesToLaterFeature()
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
        public async UniTask ParallelModeAllowsLaterFeatureToProvideDependency()
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
        public void SequentialModeRequiresProviderBeforeConsumer()
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

                Assert.ThrowsAsync<TimeoutException>(async () =>
                    await service.InitializeAsync(
                        Entries(consumer, provider),
                        context,
                        CancellationToken.None));
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(provider);
                Object.DestroyImmediate(consumer);
            }
        }

        [Test]
        public async UniTask GenericAndStandaloneAssetsReceiveWorldLifetime()
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
        public void GenericFeatureFieldSurvivesUnitySerialization()
        {
            var source = ScriptableObject.CreateInstance<AdapterAsset>();
            var target = ScriptableObject.CreateInstance<AdapterAsset>();
            try
            {
                source.feature.value = 42;

                var json = EditorJsonUtility.ToJson(source);
                EditorJsonUtility.FromJsonOverwrite(json, target);

                Assert.AreEqual(42, target.feature.value);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public async UniTask ProgrammaticFeatureInitializesWithoutAsset()
        {
            World<ResourceWorld>.Create(WorldConfig.Default());
            var lifeTime = new LifeTime();
            try
            {
                var feature = new AdapterFeature
                {
                    value = 31,
                };

                await feature.InitializeAsync(lifeTime);

                var resource = World<ResourceWorld>
                    .GetResource<FeatureInitializationResource>();
                Assert.AreEqual(31, resource.Value);
                Assert.AreSame(lifeTime, resource.LifeTime);
            }
            finally
            {
                lifeTime.Terminate();
            }
        }

        [Test]
        public async UniTask ContextIsAvailableThroughMainAndGenericHelpers()
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

            World<Main>.Create(WorldConfig.Default());
            using var mainContext = new EntityContext();
            var contextResource = new EcsContextResource(mainContext);
            World<Main>.SetResource(contextResource);
            Assert.AreSame(mainContext, StaticEcsContext.Get());
        }

        [Test]
        public void WorldHandleLifeTimeExtensionSupportsMainAndCustomWorlds()
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

            World<Main>.Create(WorldConfig.Default());
            var mainLifeTime = new LifeTime();
            var mainResource = new EcsWorldLifeTimeResource(mainLifeTime);
            World<Main>.SetResource(mainResource);

            Assert.AreSame(mainLifeTime, World<Main>.Handle.GetLifeTime());

            customLifeTime.Terminate();
            mainLifeTime.Terminate();
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
            {
                World<TWorld>.Destroy();
            }
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

            public IContext Context { get; }
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
