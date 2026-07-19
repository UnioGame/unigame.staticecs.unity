using System;
using FFS.Libraries.StaticEcs;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Stable snapshot identities for the built-in Unity player-loop system groups.</summary>
    public static class StaticEcsSystemGroupIds
    {
        /// <summary>Update pipeline snapshot identity.</summary>
        public static readonly Guid Update = new("b8395e45-7ad4-4a90-9af0-8f141ef792b9");

        /// <summary>Fixed-update pipeline snapshot identity.</summary>
        public static readonly Guid FixedUpdate = new("2308fe07-6f41-4e5d-900d-a918d903a743");

        /// <summary>Late-update pipeline snapshot identity.</summary>
        public static readonly Guid LateUpdate = new("ea306839-e164-4d50-8bb3-264a380135cd");

        /// <summary>Cleanup pipeline snapshot identity.</summary>
        public static readonly Guid Cleanup = new("8351185f-cae8-4015-b551-65b1469ee3bd");
    }

    /// <summary>Main-world update systems alias.</summary>
    public abstract class GameSys : World<Main>.Systems<StaticEcsUpdateSystems>
    {
    }

    /// <summary>Main-world fixed-update systems alias.</summary>
    public abstract class FixedSys : World<Main>.Systems<StaticEcsFixedUpdateSystems>
    {
    }

    /// <summary>Main-world late-update systems alias.</summary>
    public abstract class LateSys : World<Main>.Systems<StaticEcsLateUpdateSystems>
    {
    }

    /// <summary>Main-world cleanup systems alias.</summary>
    public abstract class CleanupSys : World<Main>.Systems<StaticEcsCleanupSystems>
    {
    }
}
