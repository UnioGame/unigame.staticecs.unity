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
        private static readonly Dictionary<string, UniTaskCompletionSource> Gates = new();
        private static readonly List<SerializableFeature> SerializableInstances = new();

        [SetUp]
        public void SetUp()
        {
            Log.Clear();
            Gates.Clear();
            SerializableInstances.Clear();
            SerializableFeature.DestroyLog.Clear();
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

            CollectionAssert.Contains(Log, "cancelled:destroy");
            CollectionAssert.DoesNotContain(Log, "cancelled:dispose");
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

        [Test]
        public async Task SerializableFeatureAsset_ClonesFeature_AndDestroysInReverseOrderBeforeWorld()
        {
            var first = ScriptableObject.CreateInstance<SerializableFeatureAsset>();
            var second = ScriptableObject.CreateInstance<SerializableFeatureAsset>();
            var unityReference = new GameObject("SerializableFeatureReference");
            first.feature.id = "first";
            first.feature.value = 10;
            first.feature.unityReference = unityReference;
            first.feature.node = new SerializableNode { value = 30 };
            second.feature.id = "second";
            second.feature.value = 20;
            var entries = new List<StaticEcsFeatureEntry>
            {
                new() { enabled = true, asset = first },
                new() { enabled = true, asset = second },
            };
            var service = CreateService();

            await service.InitializeAsync(entries, null, CancellationToken.None);

            Assert.That(SerializableInstances, Has.Count.EqualTo(2));
            Assert.That(SerializableInstances[0], Is.Not.SameAs(first.feature));
            Assert.That(SerializableInstances[1], Is.Not.SameAs(second.feature));
            Assert.That(SerializableInstances[0].value, Is.EqualTo(10));
            Assert.That(SerializableInstances[1].value, Is.EqualTo(20));
            Assert.That(SerializableInstances[0].unityReference, Is.SameAs(unityReference));
            Assert.That(SerializableInstances[0].node, Is.Not.SameAs(first.feature.node));
            Assert.That(SerializableInstances[0].node.value, Is.EqualTo(30));
            Assert.That(
                Resources.FindObjectsOfTypeAll<SerializableFeatureAsset>(),
                Has.Length.EqualTo(4));

            service.Dispose();

            CollectionAssert.AreEqual(
                new[] { "second:Initialized", "first:Initialized" },
                SerializableFeature.DestroyLog);
            Assert.That(first.feature.destroyCount, Is.Zero);
            Assert.That(second.feature.destroyCount, Is.Zero);
            Assert.That(
                Resources.FindObjectsOfTypeAll<SerializableFeatureAsset>(),
                Has.Length.EqualTo(2));
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(unityReference);
        }

        [Test]
        public async Task SerializableFeatureAsset_CreatesFreshFeatureForEachServiceRun()
        {
            var asset = ScriptableObject.CreateInstance<SerializableFeatureAsset>();
            asset.feature.id = "repeat";
            var entries = new List<StaticEcsFeatureEntry>
            {
                new() { enabled = true, asset = asset },
            };
            var firstService = CreateService();
            await firstService.InitializeAsync(entries, null, CancellationToken.None);
            var firstRuntime = SerializableInstances[0];
            firstService.Dispose();

            SerializableInstances.Clear();
            SerializableFeature.DestroyLog.Clear();
            var secondService = CreateService();
            await secondService.InitializeAsync(entries, null, CancellationToken.None);
            var secondRuntime = SerializableInstances[0];
            secondService.Dispose();

            Assert.That(secondRuntime, Is.Not.SameAs(firstRuntime));
            Assert.That(secondRuntime, Is.Not.SameAs(asset.feature));
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task Dispose_UsesDisposableFallbackForFeatureWithDefaultDestroy()
        {
            LegacyDisposableFeature.disposeCount = 0;
            var asset = ScriptableObject.CreateInstance<LegacyDisposableFeatureAsset>();
            var entries = new List<StaticEcsFeatureEntry>
            {
                new() { enabled = true, asset = asset },
            };
            var service = CreateService();

            await service.InitializeAsync(entries, null, CancellationToken.None);
            service.Dispose();

            Assert.That(LegacyDisposableFeature.disposeCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(asset);
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
                asset.featureName = feature.Name;
                asset.addSystems = feature.AddSystems;
                asset.failStartup = feature.FailStartup;
                if (feature.Gate != null)
                {
                    Gates[feature.Name] = feature.Gate;
                }

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
            public string featureName;
            public bool addSystems;
            public bool failStartup;

            public override IStaticEcsFeature<TestWorld> CreateFeature(IContext context)
            {
                Gates.TryGetValue(featureName, out var gate);
                return new FakeFeature(featureName, gate, addSystems, failStartup);
            }
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

            public string Name => _name;

            public UniTaskCompletionSource Gate => _gate;

            public bool AddSystems => _addSystems;

            public bool FailStartup => _failStartup;

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

            public override void Destroy() => Log.Add($"{_name}:destroy");

            public void Dispose() => Log.Add($"{_name}:dispose");
        }

        private sealed class SerializableFeatureAsset :
            StaticEcsFeatureAsset<TestWorld, SerializableFeature> { }

        private sealed class LegacyDisposableFeatureAsset : StaticEcsFeatureAsset<TestWorld>
        {
            public override IStaticEcsFeature<TestWorld> CreateFeature(IContext context)
            {
                return new LegacyDisposableFeature();
            }
        }

        private sealed class LegacyDisposableFeature : StaticEcsFeature<TestWorld>, IDisposable
        {
            public static int disposeCount;

            public override void RegisterTypes(World<TestWorld>.TypeRegistrar types) { }

            public void Dispose()
            {
                disposeCount++;
            }
        }

        [Serializable]
        private sealed class SerializableFeature : StaticEcsFeature<TestWorld>
        {
            public static readonly List<string> DestroyLog = new();

            public string id;
            public int value;
            public int destroyCount;
            public UnityEngine.Object unityReference;

            [SerializeReference]
            public SerializableNode node;

            public override void RegisterTypes(World<TestWorld>.TypeRegistrar types)
            {
                SerializableInstances.Add(this);
            }

            public override void Destroy()
            {
                destroyCount++;
                DestroyLog.Add($"{id}:{World<TestWorld>.Status}");
            }
        }

        [Serializable]
        private sealed class SerializableNode
        {
            public int value;
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
