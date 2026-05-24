using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace unigame.staticecs.unity {
    public abstract class StaticEcsModuleConfig : ScriptableObject {
        public bool enabled = true;
        public string moduleName;
    }

    public abstract class StaticEcsModuleConfig<TWorld> : StaticEcsModuleConfig
        where TWorld : struct, IWorldType {
        public virtual void RegisterTypes(World<TWorld>.TypeRegistrar types) { }

        public virtual void RegisterUpdateSystems(EcsService<TWorld> service) { }

        public virtual void RegisterFixedUpdateSystems(EcsService<TWorld> service) { }

        public virtual void RegisterLateUpdateSystems(EcsService<TWorld> service) { }

        public virtual void RegisterCleanupSystems(EcsService<TWorld> service) { }

        public virtual void OnWorldInitialized(EcsService<TWorld> service) { }
    }
}
