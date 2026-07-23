using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;
using UniGame.Context.Runtime;
using UniGame.Core.Runtime;
using UniGame.Runtime.DataFlow;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Owns one Static ECS world and its Unity player-loop system groups.</summary>
    public sealed class EcsService<TWorld> : IEcsService
        where TWorld : struct, IWorldType
    {
        private readonly LifeTime _lifeTime = new();
        private readonly List<IStaticEcsFeature<TWorld>> _runtimeFeatures = new();
        private readonly List<StaticEcsFeatureAssetBase> _runtimeFeatureAssets = new();
        private readonly StaticEcsWorldConfig _worldConfig;
        private readonly StaticEcsSystemsConfig _systemsConfig;
        private bool _updateSystemsCreated;
        private bool _fixedSystemsCreated;
        private bool _lateSystemsCreated;
        private bool _cleanupSystemsCreated;
        private bool _registered;

        /// <summary>Creates an uninitialized ECS service.</summary>
        public EcsService(StaticEcsWorldConfig worldConfig, StaticEcsSystemsConfig systemsConfig)
        {
            _worldConfig = worldConfig;
            _systemsConfig = systemsConfig;
            Report = new EcsStartupReport();
        }

        /// <inheritdoc />
        public EcsStartupReport Report { get; }

        /// <summary>Gets the lifetime owned by this service.</summary>
        public ILifeTime LifeTime => _lifeTime;

        /// <inheritdoc />
        public bool IsInitialized => World<TWorld>.Status == WorldStatus.Initialized;

        /// <summary>Initializes enabled features in their configured order.</summary>
        public async UniTask InitializeAsync(
            IReadOnlyList<StaticEcsFeatureEntry> entries,
            IContext context,
            CancellationToken cancellationToken)
        {
            DestroySystems();
            var previousDestroyException = DestroyRuntimeFeatures();
            DestroyWorldIfNeeded();
            DestroyRuntimeFeatureAssets();
            if (previousDestroyException != null)
            {
                throw new InvalidOperationException(
                    "Static ECS feature cleanup failed before initialization.",
                    previousDestroyException);
            }

            ResetReport();

            var assemblies = new HashSet<Assembly>();
            try
            {
                Report.stage = EcsStartupStage.CreateFeatures;
                CreateRuntimeFeatures(entries, context, assemblies);
                cancellationToken.ThrowIfCancellationRequested();

                Report.stage = EcsStartupStage.CreateWorld;
                World<TWorld>.Create(_worldConfig.CreateWorldConfig());
                Report.worldCreated = true;

                Report.stage = EcsStartupStage.RegisterTypes;
                var types = World<TWorld>.Types();
                for (var i = 0; i < _runtimeFeatures.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var feature = _runtimeFeatures[i];
                    Report.currentFeature = feature.FeatureName;
                    if (feature is IStaticEcsTypeFeature<TWorld> typeFeature)
                    {
                        typeFeature.RegisterTypes(types);
                    }
                }

                RegisterFeatureAssemblies(types, assemblies);
                Report.typesRegistered = true;

                Report.stage = EcsStartupStage.InitializeWorld;
                Report.currentFeature = null;
                World<TWorld>.Initialize(_worldConfig.baseEntitiesCapacity);
                Report.worldInitialized = true;

                Report.stage = EcsStartupStage.CreateSystems;
                CreateSystems();

                Report.stage = EcsStartupStage.RegisterSystems;
                for (var i = 0; i < _runtimeFeatures.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var feature = _runtimeFeatures[i];
                    Report.currentFeature = feature.FeatureName;
                    await RegisterFeatureSystemsAsync(feature, cancellationToken);
                }

                Report.stage = EcsStartupStage.InitializeSystems;
                Report.currentFeature = null;
                InitializeSystems();

                Report.stage = EcsStartupStage.StartFeatures;
                for (var i = 0; i < _runtimeFeatures.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var feature = _runtimeFeatures[i];
                    Report.currentFeature = feature.FeatureName;
                    if (feature is IStaticEcsStartupFeature<TWorld> startupFeature)
                    {
                        await startupFeature.StartAsync(cancellationToken);
                    }
                }

                Report.stage = EcsStartupStage.Completed;
                Report.currentFeature = null;
                Report.featuresRegistered = _runtimeFeatures.Count;
                Report.message =
                    $"Static ECS world `{typeof(TWorld).Name}` initialized. Features: {_runtimeFeatures.Count}.";
                EcsServiceRegistry.Register(this);
                _registered = true;
            }
            catch (Exception exception)
            {
                Report.failedFeature = Report.currentFeature;
                Report.failedStage = Report.stage;
                Report.message =
                    $"Static ECS startup failed during {Report.stage} for `{Report.currentFeature ?? "world"}`: " +
                    exception.Message;
                DestroySystems();
                var destroyException = DestroyRuntimeFeatures();
                DestroyWorldIfNeeded();
                DestroyRuntimeFeatureAssets();
                if (destroyException != null)
                {
                    exception.Data["StaticEcsFeatureDestroyException"] = destroyException;
                }

                throw;
            }
        }

        /// <summary>Adds a system to the update group.</summary>
        public EcsService<TWorld> AddUpdateSystem<TSystem>(TSystem system, short order = 0)
            where TSystem : ISystem
        {
            World<TWorld>.Systems<StaticEcsUpdateSystems>.Add(system, order);
            return this;
        }

        /// <summary>Adds a system to the fixed-update group.</summary>
        public EcsService<TWorld> AddFixedUpdateSystem<TSystem>(TSystem system, short order = 0)
            where TSystem : ISystem
        {
            World<TWorld>.Systems<StaticEcsFixedUpdateSystems>.Add(system, order);
            return this;
        }

        /// <summary>Adds a system to the late-update group.</summary>
        public EcsService<TWorld> AddLateUpdateSystem<TSystem>(TSystem system, short order = 0)
            where TSystem : ISystem
        {
            World<TWorld>.Systems<StaticEcsLateUpdateSystems>.Add(system, order);
            return this;
        }

        /// <summary>Adds a system to the cleanup group.</summary>
        public EcsService<TWorld> AddCleanupSystem<TSystem>(TSystem system, short order = 0)
            where TSystem : ISystem
        {
            World<TWorld>.Systems<StaticEcsCleanupSystems>.Add(system, order);
            return this;
        }

        /// <inheritdoc />
        public void Update()
        {
            if (!_updateSystemsCreated)
            {
                return;
            }

            World<TWorld>.Systems<StaticEcsUpdateSystems>.Update();
            World<TWorld>.Tick();
            Report.updateCount++;
        }

        /// <inheritdoc />
        public void FixedUpdate()
        {
            if (_fixedSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsFixedUpdateSystems>.Update();
            }
        }

        /// <inheritdoc />
        public void LateUpdate()
        {
            if (_lateSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsLateUpdateSystems>.Update();
            }
        }

        /// <inheritdoc />
        public void CleanupUpdate()
        {
            if (_cleanupSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsCleanupSystems>.Update();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _lifeTime.Terminate();
            DestroySystems();
            var destroyException = DestroyRuntimeFeatures();
            DestroyWorldIfNeeded();
            DestroyRuntimeFeatureAssets();
            if (_registered)
            {
                EcsServiceRegistry.Unregister(this);
                _registered = false;
            }

            if (destroyException != null)
            {
                throw new InvalidOperationException(
                    "Static ECS feature cleanup failed during service disposal.",
                    destroyException);
            }
        }

        private void CreateRuntimeFeatures(
            IReadOnlyList<StaticEcsFeatureEntry> entries,
            IContext context,
            HashSet<Assembly> assemblies)
        {
            if (entries == null)
            {
                return;
            }

            var assets = new HashSet<StaticEcsFeatureAssetBase>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.IsEnabled || entry.asset == null)
                {
                    continue;
                }

                var asset = entry.asset;
                if (!assets.Add(asset))
                {
                    throw new InvalidOperationException($"Feature asset `{asset.name}` is listed more than once.");
                }

                if (asset.WorldType != typeof(TWorld))
                {
                    throw new InvalidOperationException(
                        $"Feature asset `{asset.name}` targets `{asset.WorldType.Name}`, expected `{typeof(TWorld).Name}`.");
                }

                Report.currentFeature = asset.FeatureName;
                var runtimeInstance = asset.CreateRuntimeFeature(context);
                if (runtimeInstance.Feature is not IStaticEcsFeature<TWorld> runtime)
                {
                    StaticEcsFeatureAssetBase.DestroyRuntimeAsset(runtimeInstance.Asset);
                    throw new InvalidOperationException(
                        $"Feature asset `{asset.name}` did not create an IStaticEcsFeature<{typeof(TWorld).Name}> instance.");
                }

                _runtimeFeatures.Add(runtime);
                _runtimeFeatureAssets.Add(runtimeInstance.Asset);
                assemblies.Add(asset.GetType().Assembly);
                assemblies.Add(runtime.GetType().Assembly);
            }

            Report.featuresRegistered = _runtimeFeatures.Count;
        }

        private static void RegisterFeatureAssemblies(
            World<TWorld>.TypeRegistrar types,
            HashSet<Assembly> assemblySet)
        {
            if (assemblySet.Count == 0)
            {
                return;
            }

            var assemblies = new Assembly[assemblySet.Count];
            assemblySet.CopyTo(assemblies);
            Array.Sort(assemblies, static (left, right) =>
                string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));

            if (assemblies.Length == 1)
            {
                types.RegisterAll(assemblies[0]);
                return;
            }

            var rest = new Assembly[assemblies.Length - 1];
            Array.Copy(assemblies, 1, rest, 0, rest.Length);
            types.RegisterAll(assemblies[0], rest);
        }

        private async UniTask RegisterFeatureSystemsAsync(
            IStaticEcsFeature<TWorld> feature,
            CancellationToken cancellationToken)
        {
            if (_updateSystemsCreated &&
                feature is IStaticEcsSystemsFeature<TWorld, StaticEcsUpdateSystems> updateFeature)
            {
                await updateFeature.RegisterSystemsAsync(
                    new StaticEcsSystemsBuilder<TWorld, StaticEcsUpdateSystems>(),
                    cancellationToken);
            }

            if (_fixedSystemsCreated &&
                feature is IStaticEcsSystemsFeature<TWorld, StaticEcsFixedUpdateSystems> fixedFeature)
            {
                await fixedFeature.RegisterSystemsAsync(
                    new StaticEcsSystemsBuilder<TWorld, StaticEcsFixedUpdateSystems>(),
                    cancellationToken);
            }

            if (_lateSystemsCreated &&
                feature is IStaticEcsSystemsFeature<TWorld, StaticEcsLateUpdateSystems> lateFeature)
            {
                await lateFeature.RegisterSystemsAsync(
                    new StaticEcsSystemsBuilder<TWorld, StaticEcsLateUpdateSystems>(),
                    cancellationToken);
            }

            if (_cleanupSystemsCreated &&
                feature is IStaticEcsSystemsFeature<TWorld, StaticEcsCleanupSystems> cleanupFeature)
            {
                await cleanupFeature.RegisterSystemsAsync(
                    new StaticEcsSystemsBuilder<TWorld, StaticEcsCleanupSystems>(),
                    cancellationToken);
            }
        }

        private void CreateSystems()
        {
            if (_systemsConfig.update)
            {
                World<TWorld>.Systems<StaticEcsUpdateSystems>.Create(
                    _systemsConfig.baseSize,
                    StaticEcsSystemGroupIds.Update);
                _updateSystemsCreated = true;
            }

            if (_systemsConfig.fixedUpdate)
            {
                World<TWorld>.Systems<StaticEcsFixedUpdateSystems>.Create(
                    _systemsConfig.baseSize,
                    StaticEcsSystemGroupIds.FixedUpdate);
                _fixedSystemsCreated = true;
            }

            if (_systemsConfig.lateUpdate)
            {
                World<TWorld>.Systems<StaticEcsLateUpdateSystems>.Create(
                    _systemsConfig.baseSize,
                    StaticEcsSystemGroupIds.LateUpdate);
                _lateSystemsCreated = true;
            }

            if (_systemsConfig.cleanup)
            {
                World<TWorld>.Systems<StaticEcsCleanupSystems>.Create(
                    _systemsConfig.baseSize,
                    StaticEcsSystemGroupIds.Cleanup);
                _cleanupSystemsCreated = true;
            }
        }

        private void InitializeSystems()
        {
            if (_updateSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsUpdateSystems>.Initialize();
            }

            if (_fixedSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsFixedUpdateSystems>.Initialize();
            }

            if (_lateSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsLateUpdateSystems>.Initialize();
            }

            if (_cleanupSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsCleanupSystems>.Initialize();
            }

            Report.systemsInitialized = true;
        }

        private void DestroySystems()
        {
            if (_cleanupSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsCleanupSystems>.Destroy();
                _cleanupSystemsCreated = false;
            }

            if (_lateSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsLateUpdateSystems>.Destroy();
                _lateSystemsCreated = false;
            }

            if (_fixedSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsFixedUpdateSystems>.Destroy();
                _fixedSystemsCreated = false;
            }

            if (_updateSystemsCreated)
            {
                World<TWorld>.Systems<StaticEcsUpdateSystems>.Destroy();
                _updateSystemsCreated = false;
            }
        }

        private Exception DestroyRuntimeFeatures()
        {
            Exception firstException = null;
            for (var i = _runtimeFeatures.Count - 1; i >= 0; i--)
            {
                try
                {
                    var feature = _runtimeFeatures[i];
                    if (feature is IStaticEcsDestroyFeature<TWorld> destroyFeature &&
                        (feature is not IDisposable || HasCustomDestroy(feature)))
                    {
                        destroyFeature.Destroy();
                    }
                    else if (feature is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    else if (feature is IStaticEcsDestroyFeature<TWorld> defaultDestroyFeature)
                    {
                        defaultDestroyFeature.Destroy();
                    }
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }

            _runtimeFeatures.Clear();
            return firstException;
        }

        private static bool HasCustomDestroy(IStaticEcsFeature<TWorld> feature)
        {
            var method = feature.GetType().GetMethod(
                nameof(IStaticEcsDestroyFeature<TWorld>.Destroy),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            return method?.DeclaringType != typeof(StaticEcsFeature<TWorld>);
        }

        private void DestroyRuntimeFeatureAssets()
        {
            for (var i = _runtimeFeatureAssets.Count - 1; i >= 0; i--)
            {
                StaticEcsFeatureAssetBase.DestroyRuntimeAsset(_runtimeFeatureAssets[i]);
            }

            _runtimeFeatureAssets.Clear();
        }

        private void ResetReport()
        {
            Report.worldCreated = false;
            Report.typesRegistered = false;
            Report.worldInitialized = false;
            Report.systemsInitialized = false;
            Report.featuresRegistered = 0;
            Report.updateCount = 0;
            Report.stage = EcsStartupStage.None;
            Report.failedStage = EcsStartupStage.None;
            Report.currentFeature = null;
            Report.failedFeature = null;
            Report.message = null;
        }

        private void DestroyWorldIfNeeded()
        {
            if (World<TWorld>.Status == WorldStatus.Created)
            {
                // Static ECS 2.2.x registers pool destroy handles before allocating their
                // instances. Initialize the empty world first so rollback can release a
                // partially registered Created world safely.
                World<TWorld>.Initialize(_worldConfig.baseEntitiesCapacity);
                World<TWorld>.Destroy(withHooks: false);
                return;
            }

            if (World<TWorld>.Status == WorldStatus.Initialized)
            {
                World<TWorld>.Destroy();
            }
        }
    }
}
