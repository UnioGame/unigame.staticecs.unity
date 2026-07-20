using System;
using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Creates a transform binding from inline authoring data.</summary>
    [Serializable]
    public class TransformBindingSerializableConverter<TWorld> :
        EcsComponentSerializableConverter<TWorld, TransformBindingComponent>
        where TWorld : struct, IWorldType
    {
        [SerializeField]
        private Transform _target;

        /// <summary>Gets or sets the explicit transform, or <see langword="null"/> to use the host transform.</summary>
        public Transform Target {
            get => _target;
            set => _target = value;
        }

        /// <inheritdoc />
        protected override TransformBindingComponent Build(GameObject host)
        {
            return TransformBindingConverterUtility.Build(host, _target);
        }
    }
}
