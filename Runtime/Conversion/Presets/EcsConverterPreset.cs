using System.Collections.Generic;
using FFS.Libraries.StaticEcs;
using FFS.Libraries.StaticEcs.Unity;
using UnityEngine;

namespace unigame.staticecs.unity {
    public abstract class EcsConverterPresetBase : EcsConverterAssetBase { }

    public abstract class EcsConverterPreset<TWorld> : EcsConverterAsset<TWorld>
        where TWorld : struct, IWorldType {
        [SerializeReference]
        protected List<IComponentOrTagProvider> providers = new();

        [SerializeReference]
        protected List<IEcsConverter<TWorld>> nestedConverters = new();

        public IReadOnlyList<IComponentOrTagProvider> Providers => providers;
        public IReadOnlyList<IEcsConverter<TWorld>> NestedConverters => nestedConverters;

        public override void Apply(World<TWorld>.Entity entity, GameObject host) {
            if (providers != null) {
                for (var i = 0; i < providers.Count; i++) {
                    providers[i]?.Apply(entity);
                }
            }

            if (nestedConverters != null) {
                for (var i = 0; i < nestedConverters.Count; i++) {
                    var c = nestedConverters[i];
                    if (c == null || !c.IsEnabled) continue;
                    c.Apply(entity, host);
                }
            }
        }
    }

    public abstract class EcsConverterPreset : EcsConverterPreset<Main> { }
}
