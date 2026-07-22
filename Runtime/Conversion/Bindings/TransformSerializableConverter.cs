namespace UniGame.StaticEcs.Unity
{
    using System;
    using FFS.Libraries.StaticEcs;
    using UnityEngine;

    /// <summary>Creates a transform component from inline authoring data.</summary>
    [Serializable]
    public class TransformSerializableConverter<TWorld>
        : EcsComponentSerializableConverter<TWorld, TransformComponent>
        where TWorld : struct, IWorldType
    {
        [SerializeField]
        private Transform _target;

        /// <summary>Gets or sets the explicit transform, or <see langword="null"/> to use the host transform.</summary>
        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        /// <inheritdoc />
        protected override TransformComponent Build(GameObject host)
        {
            return TransformConverterUtility.Build(host, _target);
        }
    }
}
