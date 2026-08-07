using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity {
    public interface IEcsConverter<TWorld>
        where TWorld : struct, IWorldType {
        bool IsEnabled { get; }
        void Apply(World<TWorld>.Entity entity, GameObject host);
    }

    public interface IEcsConverterDestroyHandler<TWorld>
        where TWorld : struct, IWorldType {
        void OnEntityDestroyed(World<TWorld>.Entity entity, GameObject host);
    }

    public interface IEcsLinkResolver<TWorld>
        where TWorld : struct, IWorldType {
        void ResolveLinks(World<TWorld>.Entity entity, GameObject host);
    }

    /// <summary>Declares a prerequisite that must be ready before an entity is created.</summary>
    public interface IEcsConverterDependency<TWorld>
        where TWorld : struct, IWorldType {
        /// <summary>Returns whether the converter can be applied without creating a partial entity.</summary>
        bool IsReady(GameObject host, out string reason);
    }
}
