using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
    public abstract class StaticEcsModuleConfig : ScriptableObject
    {
        public bool enabled = true;
        public string moduleName;
    }

    public abstract class StaticEcsModuleConfig<TWorld> : StaticEcsModuleConfig, IStaticEcsModuleConfig<TWorld> where TWorld : struct, IWorldType
    {
        public virtual void RegisterTypes(World<TWorld>.TypeRegistrar types)
        {
        }

        public virtual void RegisterUpdateSystems(EcsService<TWorld> service)
        {
        }

        public virtual void RegisterFixedUpdateSystems(EcsService<TWorld> service)
        {
        }

        public virtual void RegisterLateUpdateSystems(EcsService<TWorld> service)
        {
        }

        public virtual void RegisterCleanupSystems(EcsService<TWorld> service)
        {
        }

        public virtual void OnWorldInitialized(EcsService<TWorld> service)
        {
        }
    }
    
    
    public interface IStaticEcsModuleConfig<TWorld> where TWorld : struct, IWorldType
    {
        void RegisterTypes(World<TWorld>.TypeRegistrar types);
        void RegisterUpdateSystems(EcsService<TWorld> service);
        void RegisterFixedUpdateSystems(EcsService<TWorld> service);
        void RegisterLateUpdateSystems(EcsService<TWorld> service);
        void RegisterCleanupSystems(EcsService<TWorld> service);
        void OnWorldInitialized(EcsService<TWorld> service);
    }
}