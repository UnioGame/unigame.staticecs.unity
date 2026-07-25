using System;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Identifies the current stage of feature-first ECS startup.</summary>
    public enum EcsStartupStage
    {
        /// <summary>Startup has not begun.</summary>
        None,
        /// <summary>Enabled feature assets are being cloned.</summary>
        CreateFeatures,
        /// <summary>The Static ECS world is being created.</summary>
        CreateWorld,
        /// <summary>Bootstrap-owned resources are being published.</summary>
        PublishBootstrapResources,
        /// <summary>ECS types are being registered from active feature assemblies.</summary>
        RegisterTypes,
        /// <summary>Features are publishing resources and adding systems.</summary>
        InitializeFeatures,
        /// <summary>The Static ECS world is being initialized.</summary>
        InitializeWorld,
        /// <summary>System groups are being created.</summary>
        CreateSystems,
        /// <summary>System groups are being initialized.</summary>
        InitializeSystems,
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
        /// <summary>Whether bootstrap-owned resources were published.</summary>
        public bool bootstrapResourcesInstalled;
        /// <summary>Whether every enabled feature initialized.</summary>
        public bool featuresInitialized;
        /// <summary>Whether the world was initialized.</summary>
        public bool worldInitialized;
        /// <summary>Whether every enabled systems group was initialized.</summary>
        public bool systemsInitialized;
        /// <summary>Number of enabled runtime features.</summary>
        public int featureCount;
        /// <summary>Number of update ticks executed.</summary>
        public int updateCount;
        /// <summary>Whether a runner loop stopped because of an unhandled exception.</summary>
        public bool runtimeFaulted;
        /// <summary>Runner loop that raised the runtime fault.</summary>
        public string runtimeFaultGroup;
        /// <summary>Human-readable runtime fault details.</summary>
        public string runtimeFaultMessage;
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
            bootstrapResourcesInstalled &&
            featuresInitialized &&
            typesRegistered &&
            worldInitialized &&
            systemsInitialized &&
            !runtimeFaulted;
    }
}
