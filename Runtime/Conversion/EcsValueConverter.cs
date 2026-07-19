using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity {
    public abstract class EcsValueConverter<TWorld, TComponent, TValue> : EcsMonoConverter<TWorld, TComponent>
        where TWorld : struct, IWorldType
        where TComponent : struct, IComponent {
        [SerializeField]
        protected TValue _value;

        public TValue Value {
            get => _value;
            set => _value = value;
        }

        protected sealed override TComponent Build(GameObject host) => Convert(host, _value);

        protected abstract TComponent Convert(GameObject host, TValue value);
    }
}
