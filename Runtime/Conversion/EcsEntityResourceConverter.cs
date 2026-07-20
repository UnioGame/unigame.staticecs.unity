using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity {
    public abstract class EcsEntityResourceConverter<TWorld, TResource> :
        EcsMonoConverter<TWorld>,
        IEcsConverterDestroyHandler<TWorld>
        where TWorld : struct, IWorldType
        where TResource : struct, IEcsEntityRefResource {
        public override void Apply(World<TWorld>.Entity entity, GameObject host) {
            EcsEntityResourceConverterUtility<TWorld, TResource>.Apply(entity);
        }

        public void OnEntityDestroyed(World<TWorld>.Entity entity, GameObject host) {
            EcsEntityResourceConverterUtility<TWorld, TResource>.OnEntityDestroyed(entity);
        }
    }

    internal static class EcsEntityResourceConverterUtility<TWorld, TResource>
        where TWorld : struct, IWorldType
        where TResource : struct, IEcsEntityRefResource {
        public static void Apply(World<TWorld>.Entity entity) {
            if (!World<TWorld>.HasResource<TResource>()) {
                World<TWorld>.SetResource(default(TResource));
            }

            ref var resource = ref World<TWorld>.GetResource<TResource>();
            resource.Gid = entity.GID;
        }

        public static void OnEntityDestroyed(World<TWorld>.Entity entity) {
            if (!World<TWorld>.HasResource<TResource>()) {
                return;
            }

            ref var resource = ref World<TWorld>.GetResource<TResource>();
            if (resource.Gid.Equals(entity.GID)) {
                resource.Gid = default;
            }
        }
    }
}
