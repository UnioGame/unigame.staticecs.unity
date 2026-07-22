namespace UniGame.StaticEcs.Unity
{
    using FFS.Libraries.StaticEcs;
    using UnityEngine;

    /// <summary>Creates a transform component from a Unity entity host.</summary>
    public abstract class TransformConverter<TWorld> : EcsMonoConverter<TWorld, TransformComponent>
        where TWorld : struct, IWorldType
    {
        [SerializeField]
        private Transform _target;

        /// <inheritdoc />
        protected override TransformComponent Build(GameObject host)
        {
            return TransformConverterUtility.Build(host, _target);
        }
    }

    /// <summary>Main-world transform converter.</summary>
    [AddComponentMenu("Static ECS/Transform Converter")]
    public sealed class TransformConverter : TransformConverter<Main> { }

    internal static class TransformConverterUtility
    {
        public static TransformComponent Build(GameObject host, Transform target)
        {
            return new TransformComponent
            {
                Transform =
                    target != null ? target
                    : host != null ? host.transform
                    : null,
            };
        }
    }
}
