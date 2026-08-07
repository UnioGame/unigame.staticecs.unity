using System.Collections.Generic;
using FFS.Libraries.StaticEcs;
using FFS.Libraries.StaticEcs.Unity;
using UnityEngine;

namespace UniGame.StaticEcs.Unity {
    public abstract class EcsEntityProvider<TWorld> : StaticEcsEntityProvider<TWorld>
        where TWorld : struct, IWorldType {
        [SerializeReference]
        public List<IEcsConverter<TWorld>> serializableConverters = new();

        [SerializeField]
        public List<EcsConverterAsset<TWorld>> assetConverters = new();

        private readonly List<IEcsConverter<TWorld>> _runtime = new();
        private readonly List<IEcsConverter<TWorld>> _monoBuf = new();
        private readonly List<IEcsConverter<TWorld>> _registered = new();

        private bool _pendingDeferredCreate;

        public IReadOnlyList<IEcsConverter<TWorld>> RuntimeConverters => _runtime;

        /// <summary>Collects enabled converters and validates their creation prerequisites.</summary>
        public bool CanCreate(out string reason) {
            CollectConverters();
            for (var i = 0; i < _runtime.Count; i++) {
                var converter = _runtime[i];
                if (converter == null || !converter.IsEnabled)
                    continue;
                if (converter is IEcsConverterDependency<TWorld> dependency &&
                    !dependency.IsReady(gameObject, out reason))
                    return false;
            }
            reason = string.Empty;
            return true;
        }

        public void RegisterRuntime(IEcsConverter<TWorld> converter) {
            if (converter == null || _registered.Contains(converter))
                return;
            _registered.Add(converter);
        }

        public bool UnregisterRuntime(IEcsConverter<TWorld> converter) {
            return converter != null && _registered.Remove(converter);
        }

        private new void Awake() {
            if (UsageType != UsageType.OnAwake) return;
            TryCreateOrDefer();
        }

        private new void Start() {
            if (UsageType != UsageType.OnStart) return;
            TryCreateOrDefer();
        }

        private void Update() {
            if (!_pendingDeferredCreate) return;
            if (World<TWorld>.Status != WorldStatus.Initialized) return;
            _pendingDeferredCreate = false;
            CreateEntity();
        }

        private void TryCreateOrDefer() {
            if (World<TWorld>.Status == WorldStatus.Initialized)
                CreateEntity();
            else
                _pendingDeferredCreate = true;
        }

        public override bool CreateEntity() {
            if (World<TWorld>.Status != WorldStatus.Initialized)
                return false;
            if (entityGid.Status<TWorld>() == GIDStatus.Active)
                return false;
            if (!CanCreate(out _))
                return false;
            if (!base.CreateEntity())
                return false;

            if (!entityGid.TryUnpack<TWorld>(out var entity))
                return true;

            for (var i = 0; i < _runtime.Count; i++) {
                var c = _runtime[i];
                if (c == null || !c.IsEnabled)
                    continue;
                c.Apply(entity, gameObject);
            }

            return true;
        }

        /// <summary>Runs converter teardown, destroys the entity exactly once, and clears its GID.</summary>
        public bool DestroyEntity() {
            _pendingDeferredCreate = false;
            if (World<TWorld>.Status != WorldStatus.Initialized ||
                entityGid.Status<TWorld>() != GIDStatus.Active ||
                !entityGid.TryUnpack<TWorld>(out var entity)) {
                entityGid = default;
                ClearRuntime();
                return false;
            }

            for (var i = 0; i < _runtime.Count; i++) {
                var converter = _runtime[i];
                if (converter == null || !converter.IsEnabled)
                    continue;
                if (converter is IEcsConverterDestroyHandler<TWorld> handler)
                    handler.OnEntityDestroyed(entity, gameObject);
            }

            entity.Destroy();
            entityGid = default;
            ClearRuntime();
            return true;
        }

        public override void ResolveLinks() {
            base.ResolveLinks();

            if (World<TWorld>.Status != WorldStatus.Initialized) return;
            if (entityGid.Status<TWorld>() != GIDStatus.Active) return;
            if (!entityGid.TryUnpack<TWorld>(out var entity)) return;

            for (var i = 0; i < _runtime.Count; i++) {
                var converter = _runtime[i];
                if (converter == null || !converter.IsEnabled)
                    continue;

                if (converter is IEcsLinkResolver<TWorld> r)
                    r.ResolveLinks(entity, gameObject);
            }
        }

        private new void OnDestroy() {
            if (onDestroyType == OnDestroyType.DestroyEntity)
                DestroyEntity();
            else {
                entityGid = default;
                ClearRuntime();
            }
        }

        private void CollectConverters() {
            _runtime.Clear();
            _monoBuf.Clear();
            GetComponents(_monoBuf);

            for (var i = 0; i < _monoBuf.Count; i++) {
                var c = _monoBuf[i];
                if (c == null || ReferenceEquals(c, this)) continue;
                _runtime.Add(c);
            }

            if (serializableConverters != null)
                for (var i = 0; i < serializableConverters.Count; i++) {
                    var c = serializableConverters[i];
                    if (c != null) _runtime.Add(c);
                }

            if (assetConverters != null)
                for (var i = 0; i < assetConverters.Count; i++) {
                    var c = assetConverters[i];
                    if (c != null) _runtime.Add(c);
                }

            for (var i = 0; i < _registered.Count; i++) {
                var c = _registered[i];
                if (c != null) _runtime.Add(c);
            }
        }

        private void ClearRuntime() {
            _runtime.Clear();
            _monoBuf.Clear();
        }
    }
}
