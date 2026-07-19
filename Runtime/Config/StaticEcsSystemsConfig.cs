using System;
using Cysharp.Threading.Tasks;

namespace UniGame.StaticEcs.Unity {
    [Serializable]
    public struct StaticEcsSystemsConfig {
        public bool update;
        public bool fixedUpdate;
        public bool lateUpdate;
        public bool cleanup;
        public uint baseSize;

        public PlayerLoopTiming updateTiming;
        public PlayerLoopTiming fixedUpdateTiming;
        public PlayerLoopTiming lateUpdateTiming;
        public PlayerLoopTiming cleanupTiming;

        public bool disableEcsTime;
        public bool disableEcsRng;

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
            disableEcsTime = false,
            disableEcsRng = false
        };
    }
}
