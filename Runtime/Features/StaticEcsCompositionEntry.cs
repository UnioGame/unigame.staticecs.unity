namespace UniGame.StaticEcs.Unity
{
    using System;

    /// <summary>Read-only view of one feature declared in a <see cref="StaticEcsComposition{TWorld}"/>.</summary>
    public readonly struct StaticEcsCompositionEntry
    {
        /// <summary>Creates a composition entry view.</summary>
        public StaticEcsCompositionEntry(Type featureType, StaticEcsFeatureAssetBase asset)
        {
            FeatureType = featureType;
            Asset = asset;
        }

        /// <summary>Gets the programmatic feature type, or null for a standalone asset feature.</summary>
        public Type FeatureType { get; }

        /// <summary>Gets the declared asset, or null for a code feature.</summary>
        public StaticEcsFeatureAssetBase Asset { get; }
    }
}
