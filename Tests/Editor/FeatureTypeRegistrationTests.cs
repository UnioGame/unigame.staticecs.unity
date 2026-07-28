[assembly: UniGame.StaticEcs.Unity.StaticEcsTypeRegistrar(
    typeof(UniGame.StaticEcs.Unity.Tests.RegistrationWorldClosedGenericTypes))]

namespace UniGame.StaticEcs.Unity.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using FFS.Libraries.StaticEcs;
    using NUnit.Framework;
    using UniGame.Context.Runtime;
    using UniGame.Core.Runtime;
    using UniGame.StaticEcs.Unity.Tests.DisabledSupport;
    using UnityEngine;
    using Object = UnityEngine.Object;

    [TestFixture]
    public sealed class FeatureTypeRegistrationTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroyWorld<RegistrationWorld>();
            DestroyWorld<ProgrammaticFeatureWorld>();
            DestroyWorld<Main>();
        }

        [Test]
        public void ActiveFeatureAssemblyRegistersConcreteTypesAndClosedRegistrar()
        {
            var asset = ScriptableObject.CreateInstance<RegistrationFeatureAsset>();
            var service = CreateService<RegistrationWorld>();
            try
            {
                using var context = new EntityContext();
                service.InitializeAsync(
                        Entries(asset),
                        context,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                var entity = World<RegistrationWorld>.NewEntity<Default>();
                Assert.DoesNotThrow(() => entity.Add<AutoRegisteredComponent>());
                Assert.DoesNotThrow(() => entity.Set<AutoRegisteredTag>());
                Assert.DoesNotThrow(() => entity.Add<ClosedGenericComponent<int>>());
                Assert.DoesNotThrow(() =>
                    entity.Add<World<RegistrationWorld>.Multi<AutoRegisteredMulti>>());
                var target = World<RegistrationWorld>.NewEntity<Default>();
                Assert.AreEqual(
                    LinkOppStatus.Ok,
                    entity.GID.TryAddLink<RegistrationWorld, AutoRegisteredLink>(target));
                Assert.AreEqual(
                    LinkOppStatus.Ok,
                    entity.GID.TryAddLinkItem<RegistrationWorld, AutoRegisteredLinks>(target));
                Assert.DoesNotThrow(() =>
                    World<RegistrationWorld>.NewEntity<AutoRegisteredEntityType>());
                var receiver =
                    World<RegistrationWorld>.RegisterEventReceiver<AutoRegisteredEvent>();
                World<RegistrationWorld>.DeleteEventReceiver(ref receiver);
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ConcreteEntityTypesExposePublicIdForEditorMetadata()
        {
            var invalidTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(static assembly => !assembly.IsDynamic)
                .SelectMany(GetLoadableTypes)
                .Where(static type =>
                    type is { IsAbstract: false, ContainsGenericParameters: false } &&
                    typeof(IEntityType).IsAssignableFrom(type))
                .Where(static type =>
                    type.GetMethod(
                        nameof(IEntityType.Id),
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        Type.EmptyTypes,
                        null) == null)
                .Select(static type => type.FullName)
                .OrderBy(static typeName => typeName)
                .ToArray();

            Assert.That(
                invalidTypes,
                Is.Empty,
                "Static ECS editor metadata requires a public parameterless IEntityType.Id() method.");
        }

        [Test]
        public void RegisterAllSkipsUnlistedClosedGenericConstruction()
        {
            var asset = ScriptableObject.CreateInstance<RegistrationFeatureAsset>();
            var service = CreateService<RegistrationWorld>();
            try
            {
                using var context = new EntityContext();
                service.InitializeAsync(
                        Entries(asset),
                        context,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                var entity = World<RegistrationWorld>.NewEntity<Default>();

                Assert.Catch<System.Exception>(() =>
                    entity.Add<ClosedGenericComponent<string>>());
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ProgrammaticFeatureBaseAssemblyIsScanned()
        {
            var asset =
                ScriptableObject.CreateInstance<DerivedRegistrationFeatureAsset>();
            var service = CreateService<ProgrammaticFeatureWorld>();
            try
            {
                using var context = new EntityContext();
                service.InitializeAsync(
                        Entries(asset),
                        context,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                var entity =
                    World<ProgrammaticFeatureWorld>.NewEntity<Default>();
                Assert.DoesNotThrow(() =>
                    entity.Add<ProgrammaticFeatureBaseComponent>());
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ActiveFeatureCanRegisterOwnedClosedGenericTypes()
        {
            var asset =
                ScriptableObject.CreateInstance<FeatureOwnedRegistrationAsset>();
            var service = CreateService<RegistrationWorld>();
            try
            {
                using var context = new EntityContext();
                service.InitializeAsync(
                        Entries(asset),
                        context,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                var entity = World<RegistrationWorld>.NewEntity<Default>();
                Assert.DoesNotThrow(() =>
                    entity.Add<ClosedGenericComponent<long>>());
            }
            finally
            {
                service.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void DisabledFeatureAssemblyIsNotScannedOrInitialized()
        {
            var asset = ScriptableObject.CreateInstance<DisabledFeatureAsset>();
            var service = CreateService<Main>();
            try
            {
                using var context = new EntityContext();
                service.InitializeAsync(
                        new List<StaticEcsFeatureEntry>
                        {
                            new() { enabled = false, asset = asset },
                        },
                        context,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                var entity = World<Main>.NewEntity<Default>();
                Assert.Catch<System.Exception>(() =>
                    entity.Add<DisabledAutoComponent>());
                Assert.IsFalse(
                    World<Main>.HasResource<DisabledFeatureInitializedResource>());
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
            systems.update = false;
            return new EcsService<TWorld>(StaticEcsWorldConfig.Default, systems);
        }

        private static List<StaticEcsFeatureEntry> Entries(
            StaticEcsFeatureAssetBase asset)
        {
            return new List<StaticEcsFeatureEntry>
            {
                new() { enabled = true, asset = asset },
            };
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

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(static type => type != null);
            }
        }
    }

    public struct RegistrationWorld : IWorldType { }

    public struct AutoRegisteredComponent : IComponent { }

    public struct AutoRegisteredTag : ITag { }

    public struct AutoRegisteredEvent : IEvent { }

    public struct AutoRegisteredMulti : IMultiComponent { }

    public struct AutoRegisteredLink : ILinkType { }

    public struct AutoRegisteredLinks : ILinksType { }

    public struct AutoRegisteredEntityType : IEntityType
    {
        public byte Id()
        {
            return 1;
        }
    }

    public struct ClosedGenericComponent<T> : IComponent { }

    public sealed class RegistrationFeatureAsset :
        StaticEcsFeatureAsset<RegistrationWorld>
    {
        protected override UniTask OnInitializeAsync(
            ILifeTime lifeTime)
        {
            return UniTask.CompletedTask;
        }
    }

    public sealed class FeatureOwnedRegistrationAsset :
        StaticEcsFeatureAsset<RegistrationWorld>,
        IStaticEcsFeatureTypeRegistrar<RegistrationWorld>
    {
        public void RegisterTypes(
            World<RegistrationWorld>.TypeRegistrar types)
        {
            types.Component<ClosedGenericComponent<long>>();
        }

        protected override UniTask OnInitializeAsync(ILifeTime lifeTime)
        {
            return UniTask.CompletedTask;
        }
    }

    public sealed class DerivedRegistrationFeature :
        ProgrammaticFeatureBase
    {
    }

    public sealed class DerivedRegistrationFeatureAsset :
        StaticEcsFeatureAsset<ProgrammaticFeatureWorld, DerivedRegistrationFeature>
    {
    }

    public sealed class RegistrationWorldClosedGenericTypes :
        IStaticEcsTypeRegistrar<RegistrationWorld>
    {
        public void Register(World<RegistrationWorld>.TypeRegistrar types)
        {
            types.Component<ClosedGenericComponent<int>>();
        }
    }
}
