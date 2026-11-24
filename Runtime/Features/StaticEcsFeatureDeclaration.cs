namespace UniGame.StaticEcs.Unity
{
    using System;
    using FFS.Libraries.StaticEcs;

    internal sealed class StaticEcsFeatureDeclaration<TWorld>
        where TWorld : struct, IWorldType
    {
        private readonly Func<IStaticEcsFeature<TWorld>> _factory;

        public StaticEcsFeatureDeclaration(
            StaticEcsFeatureAssetBase asset,
            Func<IStaticEcsFeature<TWorld>> factory,
            Type featureType,
            string name)
        {
            Asset = asset;
            _factory = factory;
            FeatureType = featureType;
            Name = name;
        }

        public StaticEcsFeatureAssetBase Asset { get; }

        /// <summary>Gets the programmatic feature type, or null for a standalone asset feature.</summary>
        public Type FeatureType { get; }

        public string Name { get; }

        public IStaticEcsFeature<TWorld> CreateFeature() => _factory();
    }
}
