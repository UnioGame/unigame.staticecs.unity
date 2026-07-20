using System;
using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Base class for inline converters stored through <see cref="SerializeReference"/>.</summary>
    [Serializable]
    public abstract class EcsSerializableConverter<TWorld> : IEcsConverter<TWorld>
        where TWorld : struct, IWorldType
    {
        [SerializeField]
        private bool _isEnabled = true;

        /// <inheritdoc />
        public virtual bool IsEnabled => _isEnabled;

        /// <inheritdoc />
        public abstract void Apply(World<TWorld>.Entity entity, GameObject host);
    }

}
