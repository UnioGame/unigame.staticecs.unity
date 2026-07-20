using System;
using FFS.Libraries.StaticEcs;
using UniGame.Core.Runtime;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Non-generic base for feature assets stored by an ECS service source.</summary>
    public abstract class StaticEcsFeatureAssetBase : ScriptableObject
    {
        /// <summary>Gets the world marker type supported by this asset.</summary>
        public abstract Type WorldType { get; }

        /// <summary>Gets the display name used by editor tooling and startup reports.</summary>
        public virtual string FeatureName => name;

        internal abstract object CreateRuntimeFeature(IContext context);
    }

    /// <summary>Factory asset that creates a fresh runtime feature for a specific world.</summary>
    public abstract class StaticEcsFeatureAsset<TWorld> : StaticEcsFeatureAssetBase
        where TWorld : struct, IWorldType
    {
        /// <inheritdoc />
        public sealed override Type WorldType => typeof(TWorld);

        /// <summary>Creates a non-shared runtime feature instance.</summary>
        public abstract IStaticEcsFeature<TWorld> CreateFeature(IContext context);

        internal sealed override object CreateRuntimeFeature(IContext context)
        {
            return CreateFeature(context);
        }
    }

    /// <summary>One ordered feature reference in an ECS service configuration.</summary>
    [Serializable]
    public sealed class StaticEcsFeatureEntry
    {
        /// <summary>Controls whether this feature participates in startup.</summary>
        public bool enabled = true;

        /// <summary>Asset factory used to create the runtime feature.</summary>
        public StaticEcsFeatureAssetBase asset;

        public string Name => asset == null ? "EMPTY" : asset.name;
    }
}
