using System;
using FFS.Libraries.StaticEcs;

namespace UniGame.StaticEcs.Unity {
    /// <summary>Controls how enabled Static ECS features overlap during startup.</summary>
    public enum StaticEcsFeatureInitializationMode
    {
        /// <summary>Starts every feature pipeline and awaits their completion together.</summary>
        Parallel = 0,

        /// <summary>Resolves and initializes features one at a time in configured order.</summary>
        Sequential = 1,
    }

    /// <summary>Configures Static ECS world capacity and feature dependency timeouts.</summary>
    [Serializable]
    public struct StaticEcsWorldConfig : IResource {
        /// <summary>Initial entity capacity.</summary>
        public uint baseEntitiesCapacity;
        /// <summary>Initial component-type capacity.</summary>
        public uint baseComponentTypesCount;
        /// <summary>Initial cluster capacity.</summary>
        public ushort baseClustersCapacity;
        /// <summary>Worker-thread count.</summary>
        public int threadCount;
        /// <summary>Worker spin count.</summary>
        public uint workerSpinCount;
        /// <summary>Whether the world owns independent worker state.</summary>
        public bool independent;
        /// <summary>Whether created entities are tracked.</summary>
        public bool trackCreated;
        /// <summary>Entity tracking buffer size.</summary>
        public byte trackingBufferSize;
        /// <summary>Feature dependency timeout used in the Unity Editor.</summary>
        public int editorDependencyTimeoutMs;
        /// <summary>Feature dependency timeout used in a player.</summary>
        public int playerDependencyTimeoutMs;
        /// <summary>Controls whether asynchronous feature startup pipelines may overlap.</summary>
        public StaticEcsFeatureInitializationMode featureInitializationMode;

        /// <summary>Gets the default world configuration.</summary>
        public static StaticEcsWorldConfig Default => new() {
            baseEntitiesCapacity = 4096,
            baseComponentTypesCount = 64,
            baseClustersCapacity = 16,
            threadCount = 0,
            workerSpinCount = 256,
            independent = true,
            trackCreated = false,
            trackingBufferSize = 8,
            editorDependencyTimeoutMs = 5000,
            playerDependencyTimeoutMs = 10000,
            featureInitializationMode = StaticEcsFeatureInitializationMode.Parallel
        };

        /// <summary>Creates the upstream Static ECS configuration.</summary>
        public WorldConfig CreateWorldConfig() {
            return new WorldConfig {
                BaseComponentTypesCount = baseComponentTypesCount,
                BaseClustersCapacity = baseClustersCapacity,
                ThreadCount = threadCount < 0 ? WorldConfig.MaxThreadCount : (uint)threadCount,
                WorkerSpinCount = workerSpinCount,
                Independent = independent,
                TrackCreated = trackCreated,
                TrackingBufferSize = trackingBufferSize
            };
        }

        internal TimeSpan GetDependencyTimeout()
        {
#if UNITY_EDITOR
            var milliseconds = editorDependencyTimeoutMs;
            if (milliseconds <= 0)
            {
                milliseconds = 5000;
            }
#else
            var milliseconds = playerDependencyTimeoutMs;
            if (milliseconds <= 0)
            {
                milliseconds = 10000;
            }
#endif
            return TimeSpan.FromMilliseconds(milliseconds);
        }
    }
}
