using System;
using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Stores an entity reference in a world resource from inline authoring data.</summary>
    [Serializable]
    public class EcsEntityResourceSerializableConverter<TWorld, TResource> :
        EcsSerializableConverter<TWorld>,
        IEcsConverterDestroyHandler<TWorld>
        where TWorld : struct, IWorldType
        where TResource : struct, IEcsEntityRefResource
    {
        /// <inheritdoc />
        public override void Apply(World<TWorld>.Entity entity, GameObject host)
        {
            EcsEntityResourceConverterUtility<TWorld, TResource>.Apply(entity);
        }

        /// <inheritdoc />
        public void OnEntityDestroyed(World<TWorld>.Entity entity, GameObject host)
        {
            EcsEntityResourceConverterUtility<TWorld, TResource>.OnEntityDestroyed(entity);
        }
    }
}
