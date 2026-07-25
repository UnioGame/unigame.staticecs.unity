using System;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;

namespace UniGame.StaticEcs.Unity {
    /// <summary>Configures enabled Static ECS system groups and their Unity loop timings.</summary>
    [Serializable]
    public struct StaticEcsSystemsConfig : IResource {
        public bool update;
        public bool fixedUpdate;
        public bool lateUpdate;
        public bool cleanup;
        public uint baseSize;

        public PlayerLoopTiming updateTiming;
        public PlayerLoopTiming fixedUpdateTiming;
        public PlayerLoopTiming lateUpdateTiming;
        public PlayerLoopTiming cleanupTiming;
        public PlayerLoopTiming tickTiming;

        public static StaticEcsSystemsConfig Default => new() {
            update = true,
            fixedUpdate = false,
            lateUpdate = false,
            cleanup = false,
            baseSize = 16,
            updateTiming = PlayerLoopTiming.Update,
            fixedUpdateTiming = PlayerLoopTiming.FixedUpdate,
            lateUpdateTiming = PlayerLoopTiming.PostLateUpdate,
            cleanupTiming = PlayerLoopTiming.LastPostLateUpdate,
            tickTiming = PlayerLoopTiming.LastPostLateUpdate
        };
    }
}
