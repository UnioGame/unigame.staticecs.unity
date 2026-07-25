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
    public class EcsService<TWorld> : IEcsService
        where TWorld : struct, IWorldType
    {
        private readonly LifeTime _lifeTime = new();
        private readonly List<StaticEcsFeatureAsset<TWorld>> _runtimeFeatures = new();
        private readonly StaticEcsWorldConfig _worldConfig;
        private readonly StaticEcsSystemsConfig _systemsConfig;
        private LifeTime _worldLifeTime;
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

        /// <summary>Starts enabled feature pipelines in configured order using the selected initialization mode.</summary>
        public async UniTask InitializeAsync(
            IReadOnlyList<StaticEcsFeatureEntry> entries,
            IContext context,
            CancellationToken cancellationToken)
        {
            TeardownWorld("reinitialization");

            ResetReport();

            var assemblies = new HashSet<Assembly>();
            try
            {
                Report.stage = EcsStartupStage.CreateFeatures;
                if (context == null)
                {
                    throw new ArgumentNullException(
                        nameof(context),
                        "Static ECS initialization requires an application context.");
                }

                CreateRuntimeFeatures(entries, assemblies);
                cancellationToken.ThrowIfCancellationRequested();

                Report.stage = EcsStartupStage.CreateWorld;
                World<TWorld>.Create(_worldConfig.CreateWorldConfig());
                Report.worldCreated = true;

                Report.stage = EcsStartupStage.PublishBootstrapResources;
                _worldLifeTime = new LifeTime();
                var worldLifeTimeResource =
                    new EcsWorldLifeTimeResource(_worldLifeTime);
                var contextResource = new EcsContextResource(context);
                var worldConfig = _worldConfig;
                var systemsConfig = _systemsConfig;

                World<TWorld>.SetResource(worldLifeTimeResource);
                World<TWorld>.SetResource(contextResource);
                World<TWorld>.SetResource(worldConfig);
                World<TWorld>.SetResource(systemsConfig);

                Report.bootstrapResourcesInstalled = true;

                Report.stage = EcsStartupStage.CreateSystems;
                CreateSystems();

                Report.stage = EcsStartupStage.RegisterTypes;
                var types = World<TWorld>.Types();
                var activeAssemblies = RegisterFeatureAssemblies(types, assemblies);
                RegisterClosedGenericTypes(types, activeAssemblies);
                Report.typesRegistered = true;

                using var startupCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        _lifeTime.Token,
                        context.LifeTime.Token);
                using var worldCancellationRegistration =
                    startupCancellation.Token.Register(
                        static state => ((LifeTime)state).Terminate(),
                        _worldLifeTime);

                var worldCancellationToken = _worldLifeTime.Token;
                await InitializeFeaturesAsync(worldCancellationToken);

                Report.featuresInitialized = true;

                Report.stage = EcsStartupStage.InitializeWorld;
                Report.currentFeature = null;
                World<TWorld>.Initialize(_worldConfig.baseEntitiesCapacity);
                Report.worldInitialized = true;

                Report.stage = EcsStartupStage.InitializeSystems;
                Report.currentFeature = null;
                InitializeSystems();

                Report.stage = EcsStartupStage.Completed;
                Report.currentFeature = null;
                Report.featureCount = _runtimeFeatures.Count;
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
                TeardownWorld("startup rollback");

                throw;
            }
        }

        private UniTask InitializeFeaturesAsync(CancellationToken cancellationToken)
        {
            return _worldConfig.featureInitializationMode ==
                   StaticEcsFeatureInitializationMode.Sequential
                ? InitializeFeaturesSequentiallyAsync(cancellationToken)
                : InitializeFeaturesInParallelAsync(cancellationToken);
        }

        private async UniTask InitializeFeaturesSequentiallyAsync(
            CancellationToken cancellationToken)
        {
            Report.stage = EcsStartupStage.InitializeFeatures;
            for (var i = 0; i < _runtimeFeatures.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var feature = _runtimeFeatures[i];
                Report.currentFeature = feature.FeatureName;
                await feature.InitializeAsync(_worldLifeTime);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private async UniTask InitializeFeaturesInParallelAsync(
            CancellationToken cancellationToken)
        {
            var count = _runtimeFeatures.Count;
            if (count == 0)
            {
                return;
            }

            Report.currentFeature = null;
            Report.stage = EcsStartupStage.InitializeFeatures;
            var tasks = new UniTask[count];
            var failures = new string[count];
            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tasks[i] = InitializeFeatureAsync(
                    _runtimeFeatures[i],
                    i,
                    failures);
            }

            try
            {
                await UniTask.WhenAll(tasks);
            }
            catch
            {
                for (var i = 0; i < failures.Length; i++)
                {
                    var failure = failures[i];
                    if (string.IsNullOrEmpty(failure))
                    {
                        continue;
                    }

                    Report.currentFeature = failure;
                    break;
                }

                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private async UniTask InitializeFeatureAsync(
            StaticEcsFeatureAsset<TWorld> feature,
            int index,
            string[] failures)
        {
            try
            {
                await feature.InitializeAsync(_worldLifeTime);
            }
            catch
            {
                failures[index] = feature.FeatureName;
                throw;
            }
        }

        /// <inheritdoc />
        public void Update()
        {
            if (!_updateSystemsCreated)
            {
                return;
            }

            World<TWorld>.Systems<StaticEcsUpdateSystems>.Update();
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
        public void AdvanceTick()
        {
            if (!IsInitialized)
            {
                return;
            }

            World<TWorld>.Tick();
            Report.updateCount++;
        }

        internal void RecordRuntimeFault(string group, Exception exception)
        {
            Report.runtimeFaulted = true;
            Report.runtimeFaultGroup = group;
            Report.runtimeFaultMessage = exception?.ToString();
            Report.message =
                $"Static ECS runner stopped after a fault in `{group}`: {exception?.Message}";
        }

        /// <inheritdoc />
        public void Dispose()
        {
            TryCleanup("service lifetime", "service disposal", _lifeTime.Terminate);
            TeardownWorld("service disposal");
            if (_registered)
            {
                TryCleanup(
                    "service registry",
                    "service disposal",
                    () => EcsServiceRegistry.Unregister(this));
                _registered = false;
            }
        }

        private void CreateRuntimeFeatures(
            IReadOnlyList<StaticEcsFeatureEntry> entries,
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
                var runtimeAsset = asset.CreateRuntimeAsset();
                if (runtimeAsset is not StaticEcsFeatureAsset<TWorld> runtime)
                {
                    StaticEcsFeatureAssetBase.DestroyRuntimeAsset(runtimeAsset);
                    throw new InvalidOperationException(
                        $"Feature asset `{asset.name}` did not clone as " +
                        $"{nameof(StaticEcsFeatureAsset<TWorld>)}.");
                }

                _runtimeFeatures.Add(runtime);
                AddFeatureAssemblies(runtime, assemblies);
            }

            Report.featureCount = _runtimeFeatures.Count;
        }

        private static void AddFeatureAssemblies(
            StaticEcsFeatureAsset<TWorld> runtime,
            HashSet<Assembly> assemblies)
        {
            assemblies.Add(runtime.GetType().Assembly);

            var featureType = runtime.ProgrammaticFeatureType;
            while (featureType != null &&
                   featureType != typeof(StaticEcsFeature<TWorld>) &&
                   typeof(IStaticEcsFeature<TWorld>).IsAssignableFrom(featureType))
            {
                assemblies.Add(featureType.Assembly);
                featureType = featureType.BaseType;
            }
        }

        private static Assembly[] RegisterFeatureAssemblies(
            World<TWorld>.TypeRegistrar types,
            HashSet<Assembly> assemblySet)
        {
            if (assemblySet.Count == 0)
            {
                return Array.Empty<Assembly>();
            }

            var assemblies = new Assembly[assemblySet.Count];
            assemblySet.CopyTo(assemblies);
            Array.Sort(assemblies, static (left, right) =>
                string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));

            if (assemblies.Length == 1)
            {
                types.RegisterAll(assemblies[0]);
                return assemblies;
            }

            var rest = new Assembly[assemblies.Length - 1];
            Array.Copy(assemblies, 1, rest, 0, rest.Length);
            types.RegisterAll(assemblies[0], rest);
            return assemblies;
        }

        private static void RegisterClosedGenericTypes(
            World<TWorld>.TypeRegistrar types,
            IReadOnlyList<Assembly> assemblies)
        {
            for (var assemblyIndex = 0; assemblyIndex < assemblies.Count; assemblyIndex++)
            {
                Type registrarType = null;
                var attributes = assemblies[assemblyIndex]
                    .GetCustomAttributes<StaticEcsTypeRegistrarAttribute>();
                foreach (var attribute in attributes)
                {
                    if (!typeof(IStaticEcsTypeRegistrar<TWorld>)
                            .IsAssignableFrom(attribute.RegistrarType))
                    {
                        continue;
                    }

                    if (registrarType != null)
                    {
                        throw new InvalidOperationException(
                            $"Assembly `{assemblies[assemblyIndex].GetName().Name}` declares " +
                            $"more than one Static ECS type registrar for " +
                            $"`{typeof(TWorld).FullName}`: `{registrarType.FullName}` and " +
                            $"`{attribute.RegistrarType.FullName}`.");
                    }

                    registrarType = attribute.RegistrarType;
                    var registrar = Activator.CreateInstance(
                        attribute.RegistrarType,
                        nonPublic: true) as IStaticEcsTypeRegistrar<TWorld>;
                    if (registrar == null)
                    {
                        throw new InvalidOperationException(
                            $"Unable to create Static ECS type registrar " +
                            $"`{attribute.RegistrarType.FullName}`.");
                    }

                    registrar.Register(types);
                }
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

        private void TeardownWorld(string reason)
        {
            TerminateWorldLifeTime(reason);
            DestroySystems(reason);
            DestroyWorldIfNeeded(reason);
            DestroyRuntimeFeatures(reason);
        }

        private void TerminateWorldLifeTime(string reason)
        {
            var lifeTime = _worldLifeTime;
            _worldLifeTime = null;
            if (lifeTime != null)
            {
                TryCleanup("world lifetime", reason, lifeTime.Terminate);
            }
        }

        private void DestroySystems(string reason)
        {
            if (_cleanupSystemsCreated)
            {
                _cleanupSystemsCreated = false;
                TryCleanup(
                    "Cleanup systems",
                    reason,
                    static () => World<TWorld>.Systems<StaticEcsCleanupSystems>.Destroy());
            }

            if (_lateSystemsCreated)
            {
                _lateSystemsCreated = false;
                TryCleanup(
                    "LateUpdate systems",
                    reason,
                    static () => World<TWorld>.Systems<StaticEcsLateUpdateSystems>.Destroy());
            }

            if (_fixedSystemsCreated)
            {
                _fixedSystemsCreated = false;
                TryCleanup(
                    "FixedUpdate systems",
                    reason,
                    static () => World<TWorld>.Systems<StaticEcsFixedUpdateSystems>.Destroy());
            }

            if (_updateSystemsCreated)
            {
                _updateSystemsCreated = false;
                TryCleanup(
                    "Update systems",
                    reason,
                    static () => World<TWorld>.Systems<StaticEcsUpdateSystems>.Destroy());
            }
        }

        private void DestroyRuntimeFeatures(string reason)
        {
            for (var i = _runtimeFeatures.Count - 1; i >= 0; i--)
            {
                var runtimeFeature = _runtimeFeatures[i];
                TryCleanup(
                    $"runtime feature `{runtimeFeature.FeatureName}`",
                    reason,
                    () => StaticEcsFeatureAssetBase.DestroyRuntimeAsset(runtimeFeature));
            }

            _runtimeFeatures.Clear();
        }

        private void ResetReport()
        {
            Report.worldCreated = false;
            Report.bootstrapResourcesInstalled = false;
            Report.featuresInitialized = false;
            Report.typesRegistered = false;
            Report.worldInitialized = false;
            Report.systemsInitialized = false;
            Report.featureCount = 0;
            Report.updateCount = 0;
            Report.runtimeFaulted = false;
            Report.runtimeFaultGroup = null;
            Report.runtimeFaultMessage = null;
            Report.stage = EcsStartupStage.None;
            Report.failedStage = EcsStartupStage.None;
            Report.currentFeature = null;
            Report.failedFeature = null;
            Report.message = null;
        }

        private void DestroyWorldIfNeeded(string reason)
        {
            if (World<TWorld>.Status == WorldStatus.Created)
            {
                // Static ECS 2.2.x registers pool destroy handles before allocating their
                // instances. Initialize the empty world first so rollback can release a
                // partially registered Created world safely.
                TryCleanup(
                    $"world `{typeof(TWorld).Name}`",
                    reason,
                    () =>
                    {
                        World<TWorld>.Initialize(_worldConfig.baseEntitiesCapacity);
                        World<TWorld>.Destroy(withHooks: false);
                    });
                return;
            }

            if (World<TWorld>.Status == WorldStatus.Initialized)
            {
                TryCleanup(
                    $"world `{typeof(TWorld).Name}`",
                    reason,
                    static () => World<TWorld>.Destroy());
            }
        }

        private static void TryCleanup(
            string operation,
            string reason,
            Action cleanup)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError(
                    $"Static ECS cleanup failed for {operation} during {reason}: {exception}");
            }
        }

    }
}
