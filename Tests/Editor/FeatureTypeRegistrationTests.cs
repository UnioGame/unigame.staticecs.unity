using System.Collections.Generic;
using System.Threading;
using FFS.Libraries.StaticEcs;
using NUnit.Framework;
using UniGame.Core.Runtime;
using UniGame.StaticEcs.Unity.Tests.DisabledSupport;
using UnityEngine;

namespace UniGame.StaticEcs.Unity.Tests
{
    [TestFixture]
    public sealed class FeatureTypeRegistrationTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroyWorld<RegistrationWorld>();
            DestroyWorld<Main>();
        }

        [Test]
        public void ManualRegistrationAndRegisterAllAreCombined()
        {
            var asset = ScriptableObject.CreateInstance<RegistrationFeatureAsset>();
            asset.registerClosedGeneric = true;
            var service = CreateService<RegistrationWorld>();
            try
            {
                service.InitializeAsync(Entries(asset), null, CancellationToken.None).GetAwaiter().GetResult();
                var entity = World<RegistrationWorld>.NewEntity<Default>();

                Assert.DoesNotThrow(() => entity.Add<AutoRegisteredComponent>());
                Assert.DoesNotThrow(() => entity.Add<ClosedGenericComponent<int>>());
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void RegisterAllSkipsOpenGenericDefinitions()
        {
            var asset = ScriptableObject.CreateInstance<RegistrationFeatureAsset>();
            asset.registerClosedGeneric = false;
            var service = CreateService<RegistrationWorld>();
            try
            {
                service.InitializeAsync(Entries(asset), null, CancellationToken.None).GetAwaiter().GetResult();
                var entity = World<RegistrationWorld>.NewEntity<Default>();

                Assert.Catch<System.Exception>(() => entity.Add<ClosedGenericComponent<int>>());
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void DisabledFeatureAssemblyIsNotScanned()
        {
            var asset = ScriptableObject.CreateInstance<DisabledFeatureAsset>();
            var service = CreateService<Main>();
            try
            {
                service.InitializeAsync(
                    new List<StaticEcsFeatureEntry>
                    {
                        new() { enabled = false, asset = asset },
                    },
                    null,
                    CancellationToken.None).GetAwaiter().GetResult();
                var entity = World<Main>.NewEntity<Default>();

                Assert.Catch<System.Exception>(() => entity.Add<DisabledAutoComponent>());
                Assert.Catch<System.Exception>(() => entity.Add<DisabledManualComponent>());
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        private static EcsService<TWorld> CreateService<TWorld>()
            where TWorld : struct, IWorldType
        {
            var systems = StaticEcsSystemsConfig.Default;
            systems.update = true;
            systems.fixedUpdate = true;
            systems.lateUpdate = true;
            systems.cleanup = true;
            return new EcsService<TWorld>(StaticEcsWorldConfig.Default, systems);
        }

        private static List<StaticEcsFeatureEntry> Entries(StaticEcsFeatureAssetBase asset) =>
            new() { new StaticEcsFeatureEntry { enabled = true, asset = asset } };

        private static void DestroyWorld<TWorld>() where TWorld : struct, IWorldType
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

        private struct RegistrationWorld : IWorldType { }

        private struct AutoRegisteredComponent : IComponent { }

        private struct ClosedGenericComponent<T> : IComponent { }

        private sealed class RegistrationFeatureAsset : StaticEcsFeatureAsset<RegistrationWorld>
        {
            public bool registerClosedGeneric;

            public override IStaticEcsFeature<RegistrationWorld> CreateFeature(IContext context) =>
                new RegistrationFeature(registerClosedGeneric);
        }

        private sealed class RegistrationFeature : StaticEcsFeature<RegistrationWorld>
        {
            private readonly bool _registerClosedGeneric;

            public RegistrationFeature(bool registerClosedGeneric)
            {
                _registerClosedGeneric = registerClosedGeneric;
            }

            public override void RegisterTypes(World<RegistrationWorld>.TypeRegistrar types)
            {
                if (_registerClosedGeneric)
                {
                    types.Component<ClosedGenericComponent<int>>();
                }
            }
        }
    }
}
