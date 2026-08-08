using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using FFS.Libraries.StaticEcs;
using FFS.Libraries.StaticEcs.Unity;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Authors one Unity GameObject as a typed Static ECS entity through the centralized registry.</summary>
    public abstract class EcsEntityProvider<TWorld> : StaticEcsEntityProvider<TWorld>
        where TWorld : struct, IWorldType
    {
        [SerializeReference]
        public List<IEcsConverter<TWorld>> serializableConverters = new();

        [SerializeField]
        public List<EcsConverterAsset<TWorld>> assetConverters = new();

        private readonly List<IEcsConverter<TWorld>> _runtime = new();
        private readonly List<IEcsConverter<TWorld>> _monoBuffer = new();
        private readonly List<IEcsConverter<TWorld>> _registered = new();

        /// <summary>Gets the converter sequence collected for the current entity lifecycle.</summary>
        public IReadOnlyList<IEcsConverter<TWorld>> RuntimeConverters => _runtime;

        /// <summary>Gets the Static ECS entity kind authored by this provider.</summary>
        public byte EntityTypeId => entityType;

        /// <summary>Collects enabled converters and validates their creation prerequisites.</summary>
        public bool CanCreate(out string reason)
        {
            CollectConverters();
            for (var index = 0; index < _runtime.Count; index++)
            {
                var converter = _runtime[index];
                if (converter == null || !converter.IsEnabled)
                    continue;

                if (converter is IEcsConverterDependency<TWorld> dependency &&
                    !dependency.IsReady(gameObject, out reason))
                    return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>Adds one runtime converter without duplicating the same instance.</summary>
        public void RegisterRuntime(IEcsConverter<TWorld> converter)
        {
            if (converter == null || _registered.Contains(converter))
                return;

            _registered.Add(converter);
        }

        /// <summary>Removes one previously registered runtime converter.</summary>
        public bool UnregisterRuntime(IEcsConverter<TWorld> converter) =>
            converter != null && _registered.Remove(converter);

        /// <summary>Queues centralized creation; success means the request was accepted, not that a GID is active.</summary>
        public override bool CreateEntity() =>
            EcsAuthoringRegistry<TWorld>.RequestCreate(this);

        /// <summary>Removes pending intent or destroys the active provider-owned entity exactly once.</summary>
        public bool DestroyEntity() =>
            EcsAuthoringRegistry<TWorld>.RequestDestroy(this);

        /// <summary>Queues link resolution for the active entity at the next authoring boundary.</summary>
        public override void ResolveLinks() =>
            EcsAuthoringRegistry<TWorld>.RequestResolve(this);

        internal bool CreateEntityNow()
        {
            if (World<TWorld>.Status != WorldStatus.Initialized)
                return false;
            if (entityGid.Status<TWorld>() == GIDStatus.Active)
                return false;
            if (!CanCreate(out _))
                return false;

            var entity = World<TWorld>.NewEntity(entityType);
            entityGid = entity.GID;

            try
            {
                if (providers != null)
                {
                    for (var index = 0; index < providers.Count; index++)
                    {
                        var provider = providers[index];
                        if (provider == null)
                            throw new InvalidOperationException(
                                "[EcsEntityProvider] NULL component or tag provider.");

                        provider.Apply(entity);
                    }
                }

                for (var index = 0; index < _runtime.Count; index++)
                {
                    var converter = _runtime[index];
                    if (converter == null || !converter.IsEnabled)
                        continue;

                    converter.Apply(entity, gameObject);
                }

                if (disableEntityOnCreate)
                    entity.Disable();
            }
            catch
            {
                try
                {
                    DestroyEntityNow();
                }
                catch
                {
                    // Preserve the creation failure after best-effort teardown.
                }

                throw;
            }

            return true;
        }

        internal bool DestroyEntityNow()
        {
            if (World<TWorld>.Status != WorldStatus.Initialized ||
                entityGid.Status<TWorld>() != GIDStatus.Active ||
                !entityGid.TryUnpack<TWorld>(out var entity))
            {
                DetachEntityNow();
                return false;
            }

            Exception failure = null;
            for (var index = 0; index < _runtime.Count; index++)
            {
                var converter = _runtime[index];
                if (converter == null || !converter.IsEnabled ||
                    converter is not IEcsConverterDestroyHandler<TWorld> handler)
                    continue;

                try
                {
                    handler.OnEntityDestroyed(entity, gameObject);
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }
            }

            try
            {
                entity.Destroy();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
            finally
            {
                DetachEntityNow();
            }

            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();

            return true;
        }

        internal void ResolveLinksNow()
        {
            if (World<TWorld>.Status != WorldStatus.Initialized ||
                entityGid.Status<TWorld>() != GIDStatus.Active ||
                !entityGid.TryUnpack<TWorld>(out var entity))
                return;

            base.ResolveLinks();
            for (var index = 0; index < _runtime.Count; index++)
            {
                var converter = _runtime[index];
                if (converter == null || !converter.IsEnabled)
                    continue;

                if (converter is IEcsLinkResolver<TWorld> resolver)
                    resolver.ResolveLinks(entity, gameObject);
            }
        }

        internal void InvokeCreatedNow() => InvokeOnCreate();

        internal void SetEntityEnabledNow(bool enabled)
        {
            if (World<TWorld>.Status != WorldStatus.Initialized ||
                !entityGid.TryUnpack<TWorld>(out var entity))
                return;

            if (enabled)
                entity.Enable();
            else
                entity.Disable();
        }

        internal void DetachEntityNow()
        {
            entityGid = default;
            _runtime.Clear();
            _monoBuffer.Clear();
        }

        private new void Awake()
        {
            if (UsageType == UsageType.OnAwake)
                EcsAuthoringRegistry<TWorld>.RequestCreate(this);
        }

        private new void Start()
        {
            if (UsageType == UsageType.OnStart)
                EcsAuthoringRegistry<TWorld>.RequestCreate(this);
        }

        private new void OnEnable() =>
            EcsAuthoringRegistry<TWorld>.NotifyEnabled(this);

        private new void OnDisable() =>
            EcsAuthoringRegistry<TWorld>.NotifyDisabled(this);

        private new void OnDestroy() =>
            EcsAuthoringRegistry<TWorld>.NotifyDestroyed(this, onDestroyType);

        private void CollectConverters()
        {
            _runtime.Clear();
            _monoBuffer.Clear();
            GetComponents(_monoBuffer);

            for (var index = 0; index < _monoBuffer.Count; index++)
            {
                var converter = _monoBuffer[index];
                if (converter == null || ReferenceEquals(converter, this))
                    continue;

                _runtime.Add(converter);
            }

            if (serializableConverters != null)
            {
                for (var index = 0; index < serializableConverters.Count; index++)
                {
                    var converter = serializableConverters[index];
                    if (converter != null)
                        _runtime.Add(converter);
                }
            }

            if (assetConverters != null)
            {
                for (var index = 0; index < assetConverters.Count; index++)
                {
                    var converter = assetConverters[index];
                    if (converter != null)
                        _runtime.Add(converter);
                }
            }

            for (var index = 0; index < _registered.Count; index++)
            {
                var converter = _registered[index];
                if (converter != null)
                    _runtime.Add(converter);
            }
        }
    }
}
