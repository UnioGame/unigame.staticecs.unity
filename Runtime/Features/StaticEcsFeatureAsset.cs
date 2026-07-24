using System;
using FFS.Libraries.StaticEcs;
using UniGame.Core.Runtime;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
#if UNITY_EDITOR
    using UniModules.Editor;
#endif

#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    /// <summary>Controls when a configured ECS feature participates in startup.</summary>
    public enum StaticEcsFeatureActivation
    {
        /// <summary>The feature follows its enabled flag in every build.</summary>
        Always,

        /// <summary>The feature participates only when GAME_DEBUG is defined.</summary>
        GameDebug,
    }

    /// <summary>Non-generic base for feature assets stored by an ECS service source.</summary>
    public abstract class StaticEcsFeatureAssetBase : ScriptableObject
    {
        /// <summary>Gets the world marker type supported by this asset.</summary>
        public abstract Type WorldType { get; }

        /// <summary>Gets the display name used by editor tooling and startup reports.</summary>
        public virtual string FeatureName => name;

        /// <summary>Opens the feature implementation in the configured script editor.</summary>
        public virtual void OpenFeatureScript()
        {
#if UNITY_EDITOR
            GetType().OpenScript();
#endif
        }

        internal StaticEcsRuntimeFeature CreateRuntimeFeature(IContext context)
        {
            var runtimeAsset = Instantiate(this);
            runtimeAsset.hideFlags = HideFlags.DontSave;

            try
            {
                var runtimeFeature = runtimeAsset.CreateFeatureObject(context);
                if (runtimeFeature == null)
                {
                    throw new InvalidOperationException(
                        $"Feature asset `{name}` created a null runtime feature.");
                }

                return new StaticEcsRuntimeFeature(runtimeFeature, runtimeAsset);
            }
            catch
            {
                DestroyRuntimeAsset(runtimeAsset);
                throw;
            }
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

        internal abstract object CreateFeatureObject(IContext context);
    }

    /// <summary>Factory asset resolved from a fresh runtime clone for a specific world.</summary>
    public abstract class StaticEcsFeatureAsset<TWorld> : StaticEcsFeatureAssetBase
        where TWorld : struct, IWorldType
    {
        /// <inheritdoc />
        public sealed override Type WorldType => typeof(TWorld);

        /// <summary>Creates or exposes the feature owned by this runtime asset instance.</summary>
        public abstract IStaticEcsFeature<TWorld> CreateFeature(IContext context);

        internal sealed override object CreateFeatureObject(IContext context)
        {
            return CreateFeature(context);
        }
    }

    /// <summary>Feature asset that exposes a serializable pure feature for a specific world.</summary>
    public abstract class StaticEcsFeatureAsset<TWorld, TFeature> : StaticEcsFeatureAsset<TWorld>
        where TWorld : struct, IWorldType
        where TFeature : class, IStaticEcsFeature<TWorld>, new()
    {
        /// <summary>Serializable authoring feature cloned with the runtime asset instance.</summary>
        public TFeature feature = new();

        /// <inheritdoc />
        public sealed override void OpenFeatureScript()
        {
#if UNITY_EDITOR
            typeof(TFeature).OpenScript();
#endif
        }

        /// <inheritdoc />
        public sealed override IStaticEcsFeature<TWorld> CreateFeature(IContext context)
        {
            return feature;
        }
    }

    internal sealed class StaticEcsRuntimeFeature
    {
        internal StaticEcsRuntimeFeature(object feature, StaticEcsFeatureAssetBase asset)
        {
            Feature = feature;
            Asset = asset;
        }

        internal object Feature { get; }

        internal StaticEcsFeatureAssetBase Asset { get; }
    }

    /// <summary>One ordered feature reference in an ECS service configuration.</summary>
    [Serializable]
    public sealed class StaticEcsFeatureEntry
    {
        /// <summary>Controls whether this feature participates in startup.</summary>
        public bool enabled = true;

        /// <summary>Controls the build configuration in which the feature can participate.</summary>
        public StaticEcsFeatureActivation activation;

        /// <summary>Asset factory used to create the runtime feature.</summary>
#if ODIN_INSPECTOR
        [InlineButton(nameof(OpenFeatureScript), SdfIconType.Folder2Open)]
#endif
        public StaticEcsFeatureAssetBase asset;

        /// <summary>Returns whether the feature is enabled for the current build.</summary>
        public bool IsEnabled
        {
            get
            {
                if (!enabled)
                {
                    return false;
                }

                if (activation != StaticEcsFeatureActivation.GameDebug)
                {
                    return true;
                }

#if GAME_DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Opens the configured feature implementation in the script editor.</summary>
        public void OpenFeatureScript()
        {
            asset?.OpenFeatureScript();
        }

        public string Name => asset == null ? "EMPTY" : asset.name;
    }
}
