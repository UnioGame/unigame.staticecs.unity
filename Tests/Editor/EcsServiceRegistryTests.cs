namespace UniGame.StaticEcs.Unity.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using FFS.Libraries.StaticEcs;
    using NUnit.Framework;
    using UniGame.Context.Runtime;
    using UniGame.Core.Runtime;
    using UniGame.Runtime.DataFlow;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class EcsServiceRegistryTests
    {
        private readonly List<IEcsService> _registeredServices = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = _registeredServices.Count - 1; i >= 0; i--)
                EcsServiceRegistry.Unregister(_registeredServices[i]);

            _registeredServices.Clear();
            DestroyWorld<ReinitializationWorld>();
            DestroyWorld<CancellationWorld>();
            DestroyWorld<DisposalWorld>();
        }

        [Test]
        public void ActivePrioritizesMainThenUsesLatestLiveFallback()
        {
            var first = Register(new StubService<FirstWorld>("first"));
            var main = Register(new StubService<Main>("main"));
            var second = Register(new StubService<SecondWorld>("second"));

            Assert.AreSame(main, EcsServiceRegistry.Active);

            EcsServiceRegistry.Unregister(main);
            Assert.AreSame(second, EcsServiceRegistry.Active);

            EcsServiceRegistry.Unregister(second);
            Assert.AreSame(first, EcsServiceRegistry.Active);

            EcsServiceRegistry.Unregister(first);
            Assert.IsNull(EcsServiceRegistry.Active);
        }

        [Test]
        public void TypedLookupUsesExactWorldType()
        {
            var first = Register(new StubService<FirstWorld>("first"));
            var second = Register(new StubService<SecondWorld>("second"));

            Assert.AreSame(first, EcsServiceRegistry.Get<FirstWorld>());
            Assert.AreSame(second, EcsServiceRegistry.Get<SecondWorld>());
            Assert.IsNull(EcsServiceRegistry.Get<UnregisteredWorld>());
            Assert.IsTrue(EcsServiceRegistry.TryGet<FirstWorld>(out var resolved));
            Assert.AreSame(first, resolved);
            Assert.IsFalse(EcsServiceRegistry.TryGet<UnregisteredWorld>(out resolved));
            Assert.IsNull(resolved);
        }

        [Test]
        public void RegistrationIsIdempotentAndRejectsAnotherWorldOwner()
        {
            var owner = Register(new StubService<FirstWorld>("owner"));
            var contender = new StubService<FirstWorld>("contender");

            EcsServiceRegistry.Register(owner);
            EcsServiceRegistry.Unregister(null);
            EcsServiceRegistry.Unregister(contender);

            Assert.AreSame(owner, EcsServiceRegistry.Get<FirstWorld>());
            Assert.Throws<InvalidOperationException>(
                () => EcsServiceRegistry.Register(contender));
            Assert.AreSame(owner, EcsServiceRegistry.Get<FirstWorld>());

            EcsServiceRegistry.Unregister(owner);
            Assert.IsNull(EcsServiceRegistry.Get<FirstWorld>());
        }

        [Test]
        public void LastReportSurvivesUnregistration()
        {
            var first = Register(new StubService<FirstWorld>("first"));
            var second = Register(new StubService<SecondWorld>("second"));

            Assert.AreSame(second.Report, EcsServiceRegistry.LastReport);

            EcsServiceRegistry.Unregister(second);
            Assert.AreSame(second.Report, EcsServiceRegistry.LastReport);
            Assert.AreSame(first, EcsServiceRegistry.Active);
        }

        [Test]
        public async Task FailedReinitializationRemovesThePreviousRegistration()
        {
            var service = CreateService<ReinitializationWorld>();
            var context = new EntityContext();
            try
            {
                await service.InitializeAsync(
                    Array.Empty<StaticEcsFeatureEntry>(),
                    context,
                    CancellationToken.None);
                Assert.AreSame(service, EcsServiceRegistry.Get<ReinitializationWorld>());

                Assert.ThrowsAsync<ArgumentNullException>(async () =>
                    await service.InitializeAsync(
                        Array.Empty<StaticEcsFeatureEntry>(),
                        null,
                        CancellationToken.None));

                Assert.IsNull(EcsServiceRegistry.Get<ReinitializationWorld>());
                Assert.AreEqual(
                    WorldStatus.NotCreated,
                    World<ReinitializationWorld>.Status);
            }
            finally
            {
                service.Dispose();
                context.Dispose();
            }
        }

        [Test]
        public async Task CancelledReinitializationLeavesNoRegistration()
        {
            var service = CreateService<CancellationWorld>();
            var context = new EntityContext();
            using var cancellation = new CancellationTokenSource();
            try
            {
                await service.InitializeAsync(
                    Array.Empty<StaticEcsFeatureEntry>(),
                    context,
                    CancellationToken.None);
                cancellation.Cancel();

                Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    await service.InitializeAsync(
                        Array.Empty<StaticEcsFeatureEntry>(),
                        context,
                        cancellation.Token));

                Assert.IsNull(EcsServiceRegistry.Get<CancellationWorld>());
                Assert.AreEqual(WorldStatus.NotCreated, World<CancellationWorld>.Status);
            }
            finally
            {
                service.Dispose();
                context.Dispose();
            }
        }

        [Test]
        public async Task DisposeUnregistersTheExactServiceAndPreservesItsReport()
        {
            var service = CreateService<DisposalWorld>();
            var context = new EntityContext();
            try
            {
                await service.InitializeAsync(
                    Array.Empty<StaticEcsFeatureEntry>(),
                    context,
                    CancellationToken.None);
                var report = service.Report;
                World<DisposalWorld>.Handle.GetLifeTime().AddCleanUpAction(
                    static () => throw new InvalidOperationException(
                        "Expected registry disposal cleanup failure."));
                LogAssert.Expect(
                    LogType.Error,
                    new Regex("Expected registry disposal cleanup failure"));

                service.Dispose();

                Assert.IsNull(EcsServiceRegistry.Get<DisposalWorld>());
                Assert.AreSame(report, EcsServiceRegistry.LastReport);
                Assert.AreEqual(WorldStatus.NotCreated, World<DisposalWorld>.Status);
            }
            finally
            {
                service.Dispose();
                context.Dispose();
            }
        }

        private TService Register<TService>(TService service)
            where TService : IEcsService
        {
            EcsServiceRegistry.Register(service);
            _registeredServices.Add(service);
            return service;
        }

        private static EcsService<TWorld> CreateService<TWorld>()
            where TWorld : struct, IWorldType
        {
            var systems = StaticEcsSystemsConfig.Default;
            systems.update = false;
            systems.fixedUpdate = false;
            systems.lateUpdate = false;
            systems.cleanup = false;
            return new EcsService<TWorld>(StaticEcsWorldConfig.Default, systems);
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

        private struct FirstWorld : IWorldType { }
        private struct SecondWorld : IWorldType { }
        private struct UnregisteredWorld : IWorldType { }
        private struct ReinitializationWorld : IWorldType { }
        private struct CancellationWorld : IWorldType { }
        private struct DisposalWorld : IWorldType { }

        private sealed class StubService<TWorld> : IEcsService
            where TWorld : struct, IWorldType
        {
            private readonly LifeTime _lifeTime = new();

            public StubService(string message)
            {
                Report = new EcsStartupReport { message = message };
            }

            public Type WorldType => typeof(TWorld);
            public EcsStartupReport Report { get; }
            public ILifeTime LifeTime => _lifeTime;
            public bool IsInitialized => false;

            public void Update() { }
            public void FixedUpdate() { }
            public void LateUpdate() { }
            public void CleanupUpdate() { }
            public void AdvanceTick() { }

            public void Dispose()
            {
                EcsServiceRegistry.Unregister(this);
                _lifeTime.Terminate();
            }
        }
    }
}
