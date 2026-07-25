namespace UniGame.StaticEcs.Unity
{
    using System;
    using Cysharp.Threading.Tasks;
    using FFS.Libraries.StaticEcs;
    using UniGame.Core.Runtime;
    using UnityEngine;

#if UNITY_EDITOR
    using UniModules.Editor;
#endif

#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    /// <summary>Non-generic base for feature assets stored by an ECS service source.</summary>
    public abstract class StaticEcsFeatureAssetBase : ScriptableObject
    {
        /// <summary>Gets the world marker type supported by this asset.</summary>
        public abstract Type WorldType { get; }

        /// <summary>Gets the display name used by editor tooling and startup reports.</summary>
        public virtual string FeatureName => name;

        /// <summary>Gets the programmatic feature type used for assembly discovery.</summary>
        public virtual Type ProgrammaticFeatureType => null;

        /// <summary>Opens the feature implementation in the configured script editor.</summary>
        public virtual void OpenFeatureScript()
        {
#if UNITY_EDITOR
            GetType().OpenScript();
#endif
        }

        internal StaticEcsFeatureAssetBase CreateRuntimeAsset()
        {
            var runtimeAsset = Instantiate(this);
            runtimeAsset.hideFlags = HideFlags.DontSave;
            return runtimeAsset;
        }

        internal static void DestroyRuntimeAsset(StaticEcsFeatureAssetBase runtimeAsset)
        {
            if (runtimeAsset == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeAsset);
                return;
            }

            DestroyImmediate(runtimeAsset);
        }
    }

    /// <summary>Runtime-cloned feature asset for a specific Static ECS world.</summary>
    public abstract class StaticEcsFeatureAsset<TWorld> :
        StaticEcsFeatureAssetBase,
        IStaticEcsFeature<TWorld>
        where TWorld : struct, IWorldType
    {
        /// <inheritdoc />
        public sealed override Type WorldType => typeof(TWorld);

        /// <inheritdoc />
        public UniTask InitializeAsync(ILifeTime lifeTime)
        {
            return OnInitializeAsync(lifeTime);
        }

        /// <summary>Publishes feature resources and adds its systems.</summary>
        protected abstract UniTask OnInitializeAsync(ILifeTime lifeTime);
    }

    /// <summary>Serializes and initializes a programmatic feature for a specific world.</summary>
    public abstract class StaticEcsFeatureAsset<TWorld, TFeature> :
        StaticEcsFeatureAsset<TWorld>
        where TWorld : struct, IWorldType
        where TFeature : class, IStaticEcsFeature<TWorld>, new()
    {
        /// <summary>Serialized programmatic feature implementation.</summary>
#if ODIN_INSPECTOR
        [HideLabel]
        [InlineProperty]
#endif
        public TFeature feature = new();

        /// <inheritdoc />
        public override Type ProgrammaticFeatureType =>
            typeof(TFeature);

        /// <inheritdoc />
        public sealed override void OpenFeatureScript()
        {
#if UNITY_EDITOR
            typeof(TFeature).OpenScript();
#endif
        }

        /// <inheritdoc />
        protected sealed override UniTask OnInitializeAsync(ILifeTime lifeTime)
        {
            return feature.InitializeAsync(lifeTime);
        }
    }
}
