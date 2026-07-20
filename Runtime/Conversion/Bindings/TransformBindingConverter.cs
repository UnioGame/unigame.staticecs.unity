using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity {
    public abstract class TransformBindingConverter<TWorld> : EcsMonoConverter<TWorld, TransformBindingComponent>
        where TWorld : struct, IWorldType {
        [SerializeField]
        private Transform _target;

        protected override TransformBindingComponent Build(GameObject host) {
            return TransformBindingConverterUtility.Build(host, _target);
        }
    }

    [AddComponentMenu("Static ECS/Transform Binding Converter")]
    public sealed class TransformBindingConverter : TransformBindingConverter<Main> { }

    internal static class TransformBindingConverterUtility {
        public static TransformBindingComponent Build(GameObject host, Transform target) {
            return new TransformBindingComponent {
                Transform = target != null ? target : host != null ? host.transform : null,
            };
        }
    }
}
