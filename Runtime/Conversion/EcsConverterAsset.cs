using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace unigame.staticecs.unity {
    public abstract class EcsConverterAssetBase : ScriptableObject { }

    public abstract class EcsConverterAsset<TWorld> : EcsConverterAssetBase, IEcsConverter<TWorld>
        where TWorld : struct, IWorldType {
        [SerializeField]
        protected bool _isEnabled = true;

        public virtual bool IsEnabled => _isEnabled;

        public abstract void Apply(World<TWorld>.Entity entity, GameObject host);
    }

    public abstract class EcsConverterAsset : EcsConverterAsset<Main> { }
}
