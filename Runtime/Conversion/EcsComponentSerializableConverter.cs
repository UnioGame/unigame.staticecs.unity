using System;
using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Base class for inline converters that build and assign one ECS component.</summary>
    [Serializable]
    public abstract class EcsComponentSerializableConverter<TWorld, TComponent> : EcsSerializableConverter<TWorld>
        where TWorld : struct, IWorldType
        where TComponent : struct, IComponent
    {
        /// <inheritdoc />
        public sealed override void Apply(World<TWorld>.Entity entity, GameObject host)
        {
            entity.Set(Build(host));
        }

        /// <summary>Builds the component assigned during conversion.</summary>
        protected abstract TComponent Build(GameObject host);
    }
}
