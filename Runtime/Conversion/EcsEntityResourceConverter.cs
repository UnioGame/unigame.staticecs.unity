using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity {
    public abstract class EcsEntityResourceConverter<TWorld, TResource> :
        EcsMonoConverter<TWorld>,
        IEcsConverterDestroyHandler<TWorld>
        where TWorld : struct, IWorldType
        where TResource : struct, IEcsEntityRefResource {
        public override void Apply(World<TWorld>.Entity entity, GameObject host) {
            if (!World<TWorld>.HasResource<TResource>()) {
                World<TWorld>.SetResource(default(TResource));
            }

            ref var resource = ref World<TWorld>.GetResource<TResource>();
            resource.Gid = entity.GID;
        }

        public void OnEntityDestroyed(World<TWorld>.Entity entity, GameObject host) {
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
