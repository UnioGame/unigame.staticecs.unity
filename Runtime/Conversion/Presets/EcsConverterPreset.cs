using System.Collections.Generic;
using FFS.Libraries.StaticEcs;
using FFS.Libraries.StaticEcs.Unity;
using UnityEngine;

namespace UniGame.StaticEcs.Unity {
    public abstract class EcsConverterPresetBase : EcsConverterAssetBase { }

    public abstract class EcsConverterPreset<TWorld> :
        EcsConverterAsset<TWorld>,
        IEcsLinkResolver<TWorld>,
        IEcsConverterDestroyHandler<TWorld>
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

        /// <inheritdoc />
        public void ResolveLinks(World<TWorld>.Entity entity, GameObject host) {
            if (nestedConverters == null) {
                return;
            }

            for (var i = 0; i < nestedConverters.Count; i++) {
                var converter = nestedConverters[i];
                if (converter == null || !converter.IsEnabled) {
                    continue;
                }

                if (converter is IEcsLinkResolver<TWorld> resolver) {
                    resolver.ResolveLinks(entity, host);
                }
            }
        }

        /// <inheritdoc />
        public void OnEntityDestroyed(World<TWorld>.Entity entity, GameObject host) {
            if (nestedConverters == null) {
                return;
            }

            for (var i = 0; i < nestedConverters.Count; i++) {
                var converter = nestedConverters[i];
                if (converter == null || !converter.IsEnabled) {
                    continue;
                }

                if (converter is IEcsConverterDestroyHandler<TWorld> handler) {
                    handler.OnEntityDestroyed(entity, host);
                }
            }
        }
    }

    /// <summary>Main-world converter preset containing component providers and inline converters.</summary>
    [CreateAssetMenu(menuName = "Static ECS/Converter Preset", fileName = nameof(EcsConverterPreset))]
    public class EcsConverterPreset : EcsConverterPreset<Main> { }
}
