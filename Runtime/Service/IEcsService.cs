using System;
using UniGame.GameFlow.Runtime;

namespace UniGame.StaticEcs.Unity {
    /// <summary>Owns the lifecycle and player-loop operations for one Static ECS world.</summary>
    public interface IEcsService : IGameService {
        /// <summary>Gets the exact world type owned by this service.</summary>
        Type WorldType { get; }

        /// <summary>Gets the latest startup and runtime report for this service.</summary>
        EcsStartupReport Report { get; }

        /// <summary>Gets whether the owned world is initialized.</summary>
        bool IsInitialized { get; }

        /// <summary>Updates the regular player-loop systems.</summary>
        void Update();

        /// <summary>Updates the fixed player-loop systems.</summary>
        void FixedUpdate();

        /// <summary>Updates the late player-loop systems.</summary>
        void LateUpdate();

        /// <summary>Updates the cleanup player-loop systems.</summary>
        void CleanupUpdate();

        /// <summary>Advances the owned world's tick when it is initialized.</summary>
        void AdvanceTick();
    }
}
