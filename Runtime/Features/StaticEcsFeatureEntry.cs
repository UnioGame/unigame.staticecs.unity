namespace UniGame.StaticEcs.Unity
{
    using System;

#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    /// <summary>One ordered feature reference in an ECS service configuration.</summary>
    [Serializable]
    public class StaticEcsFeatureEntry
    {
        /// <summary>Controls whether this feature participates in startup.</summary>
        public bool enabled = true;

        /// <summary>Feature asset cloned for runtime initialization.</summary>
#if ODIN_INSPECTOR
        [InlineButton(nameof(OpenFeatureScript), SdfIconType.Folder2Open)]
#endif
        public StaticEcsFeatureAssetBase asset;

        /// <summary>Returns whether this feature participates in startup.</summary>
        public bool IsEnabled => enabled;

        /// <summary>Gets the configured asset name.</summary>
        public string Name => asset == null ? "EMPTY" : asset.name;

        /// <summary>Opens the configured feature implementation in the script editor.</summary>
        public void OpenFeatureScript()
        {
            asset?.OpenFeatureScript();
        }
    }
}
