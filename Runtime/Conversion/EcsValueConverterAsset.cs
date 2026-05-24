using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace unigame.staticecs.unity {
    public abstract class EcsValueConverterAsset<TWorld, TComponent, TValue> : EcsConverterAsset<TWorld>
        where TWorld : struct, IWorldType
        where TComponent : struct, IComponent {
        [SerializeField]
        protected TValue _value;

        public TValue Value {
            get => _value;
            set => _value = value;
        }

        public sealed override void Apply(World<TWorld>.Entity entity, GameObject host) {
            entity.Set(Convert(host, _value));
        }

        protected abstract TComponent Convert(GameObject host, TValue value);
    }
}
