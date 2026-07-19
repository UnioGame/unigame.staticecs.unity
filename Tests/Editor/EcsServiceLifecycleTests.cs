using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;
using NUnit.Framework;
using UniGame.Core.Runtime;
using UnityEngine;

namespace UniGame.StaticEcs.Unity.Tests
{
    public sealed class EcsServiceLifecycleTests
    {
        private static readonly List<string> Log = new();

        [SetUp]
        public void SetUp()
        {
            Log.Clear();
            DestroyWorld();
        }

        [TearDown]
        public void TearDown() => DestroyWorld();

        [Test]
        public async Task InitializeAsync_IsSequential_AndSupportsStructAndClassSystems()
        {
            var gate = new UniTaskCompletionSource();
            var first = new FakeFeature("first", gate, addSystems: true);
            var second = new FakeFeature("second", null, addSystems: false);
            var assets = CreateEntries(first, second);
            var service = CreateService();

            var initialization = service.InitializeAsync(assets, null, CancellationToken.None);
            if (!Log.Contains("first:systems:begin"))
            {
                await initialization.AsTask();
            }
            CollectionAssert.Contains(Log, "first:systems:begin");
            CollectionAssert.DoesNotContain(Log, "second:systems:begin");

            gate.TrySetResult();
            await initialization.AsTask();

            Assert.That(service.Report.IsSuccess, Is.True);
            Assert.That(service.Report.updateCount, Is.Zero, "No runner tick may occur during startup.");
            Assert.That(Log.IndexOf("first:systems:end"), Is.LessThan(Log.IndexOf("second:systems:begin")));
            Assert.That(Log.IndexOf("class:init"), Is.LessThan(Log.IndexOf("first:start")));
            Assert.That(Log.IndexOf("struct:init"), Is.LessThan(Log.IndexOf("first:start")));

            service.Update();
            Assert.That(Log.IndexOf("struct:update"), Is.LessThan(Log.IndexOf("class:update")));
            service.Dispose();

            CollectionAssert.Contains(Log, "struct:destroy");
            CollectionAssert.Contains(Log, "class:destroy");
            Assert.That(World<TestWorld>.Status, Is.EqualTo(WorldStatus.NotCreated));
            DestroyAssets(assets);
        }

        [Test]
        public void InitializeAsync_CancellationRollsBackRuntimeFeatures()
        {
            var feature = new FakeFeature("cancelled", null, addSystems: false);
            var assets = CreateEntries(feature);
            var service = CreateService();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () =>
                await service.InitializeAsync(assets, null, cancellation.Token).AsTask());

            CollectionAssert.Contains(Log, "cancelled:dispose");
            Assert.That(World<TestWorld>.Status, Is.EqualTo(WorldStatus.NotCreated));
            service.Dispose();
            DestroyAssets(assets);
        }

        [Test]
        public void InitializeAsync_StartupFailureDestroysSystemsBeforeWorld()
        {
            var feature = new FakeFeature("broken", null, addSystems: true, failStartup: true);
            var assets = CreateEntries(feature);
            var service = CreateService();

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await service.InitializeAsync(assets, null, CancellationToken.None).AsTask());

            Assert.That(service.Report.failedFeature, Is.EqualTo("broken"));
            Assert.That(service.Report.failedStage, Is.EqualTo(EcsStartupStage.StartFeatures));
            CollectionAssert.Contains(Log, "struct:destroy");
            CollectionAssert.Contains(Log, "class:destroy");
            Assert.That(World<TestWorld>.Status, Is.EqualTo(WorldStatus.NotCreated));
            service.Dispose();
            DestroyAssets(assets);
        }

        private static EcsService<TestWorld> CreateService()
        {
            var systems = StaticEcsSystemsConfig.Default;
            systems.update = true;
            systems.fixedUpdate = true;
            systems.lateUpdate = true;
            systems.cleanup = true;
            return new EcsService<TestWorld>(StaticEcsWorldConfig.Default, systems);
        }

        private static List<StaticEcsFeatureEntry> CreateEntries(params FakeFeature[] features)
        {
            var result = new List<StaticEcsFeatureEntry>();
            foreach (var feature in features)
            {
                var asset = ScriptableObject.CreateInstance<FakeFeatureAsset>();
                asset.runtime = feature;
                result.Add(new StaticEcsFeatureEntry { enabled = true, asset = asset });
            }

            return result;
        }

        private static void DestroyAssets(List<StaticEcsFeatureEntry> entries)
        {
            foreach (var entry in entries)
            {
                UnityEngine.Object.DestroyImmediate(entry.asset);
            }
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
        private struct TestComponent : IComponent { }

        private sealed class FakeFeatureAsset : StaticEcsFeatureAsset<TestWorld>
        {
            public FakeFeature runtime;
            public override IStaticEcsFeature<TestWorld> CreateFeature(IContext context) => runtime;
        }

        private sealed class FakeFeature : StaticEcsFeature<TestWorld>,
            IStaticEcsSystemsFeature<TestWorld, StaticEcsUpdateSystems>,
            IStaticEcsStartupFeature<TestWorld>,
            IDisposable
        {
            private readonly string _name;
            private readonly UniTaskCompletionSource _gate;
            private readonly bool _addSystems;
            private readonly bool _failStartup;

            public FakeFeature(string name, UniTaskCompletionSource gate, bool addSystems, bool failStartup = false)
            {
                _name = name;
                _gate = gate;
                _addSystems = addSystems;
                _failStartup = failStartup;
            }

            public override string FeatureName => _name;

            public override void RegisterTypes(World<TestWorld>.TypeRegistrar types)
            {
                Log.Add($"{_name}:types");
                if (_name == "first" || _name == "broken")
                {
                    types.Component<TestComponent>();
                }
            }

            public async UniTask RegisterSystemsAsync(
                StaticEcsSystemsBuilder<TestWorld, StaticEcsUpdateSystems> systems,
                CancellationToken cancellationToken)
            {
                Log.Add($"{_name}:systems:begin");
                if (_gate != null)
                {
                    await _gate.Task.AttachExternalCancellation(cancellationToken);
                }

                if (_addSystems)
                {
                    systems.Add(new StructSystem(), -10).Add(new ClassSystem(), 10);
                }

                Log.Add($"{_name}:systems:end");
            }

            public UniTask StartAsync(CancellationToken cancellationToken)
            {
                Log.Add($"{_name}:start");
                if (_failStartup)
                {
                    throw new InvalidOperationException("Expected startup failure.");
                }

                return UniTask.CompletedTask;
            }

            public void Dispose() => Log.Add($"{_name}:dispose");
        }

        private struct StructSystem : ISystem
        {
            public void Init() => Log.Add("struct:init");
            public void Update() => Log.Add("struct:update");
            public void Destroy() => Log.Add("struct:destroy");
        }

        private sealed class ClassSystem : ISystem
        {
            public void Init() => Log.Add("class:init");
            public void Update() => Log.Add("class:update");
            public void Destroy() => Log.Add("class:destroy");
        }
    }
}
