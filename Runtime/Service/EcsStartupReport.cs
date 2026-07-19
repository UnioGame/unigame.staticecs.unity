using System;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Identifies the current stage of feature-first ECS startup.</summary>
    public enum EcsStartupStage
    {
        /// <summary>Startup has not begun.</summary>
        None,
        /// <summary>Runtime feature instances are being created.</summary>
        CreateFeatures,
        /// <summary>The Static ECS world is being created.</summary>
        CreateWorld,
        /// <summary>ECS types and resources are being registered.</summary>
        RegisterTypes,
        /// <summary>The Static ECS world is being initialized.</summary>
        InitializeWorld,
        /// <summary>System groups are being created.</summary>
        CreateSystems,
        /// <summary>Features are registering systems.</summary>
        RegisterSystems,
        /// <summary>System groups are being initialized.</summary>
        InitializeSystems,
        /// <summary>Features are running post-system startup.</summary>
        StartFeatures,
        /// <summary>Startup completed successfully.</summary>
        Completed,
    }

    /// <summary>Serializable diagnostics for ECS startup and runtime ticks.</summary>
    [Serializable]
    public sealed class EcsStartupReport
    {
        /// <summary>Whether the world was created.</summary>
        public bool worldCreated;
        /// <summary>Whether type registration completed.</summary>
        public bool typesRegistered;
        /// <summary>Whether the world was initialized.</summary>
        public bool worldInitialized;
        /// <summary>Whether every enabled systems group was initialized.</summary>
        public bool systemsInitialized;
        /// <summary>Number of enabled runtime features.</summary>
        public int featuresRegistered;
        /// <summary>Number of update ticks executed.</summary>
        public int updateCount;
        /// <summary>Current or last startup stage.</summary>
        public EcsStartupStage stage;
        /// <summary>Stage that failed, if any.</summary>
        public EcsStartupStage failedStage;
        /// <summary>Feature currently being initialized.</summary>
        public string currentFeature;
        /// <summary>Feature that failed startup.</summary>
        public string failedFeature;
        /// <summary>Human-readable startup status.</summary>
        public string message;

        /// <summary>Gets whether every required startup phase completed.</summary>
        public bool IsSuccess =>
            stage == EcsStartupStage.Completed &&
            worldCreated &&
            typesRegistered &&
            worldInitialized &&
            systemsInitialized;
    }
}
