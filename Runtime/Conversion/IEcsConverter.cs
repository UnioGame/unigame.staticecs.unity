using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace unigame.staticecs.unity {
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
}
