using FFS.Libraries.StaticEcs;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Provides Main-world access to centralized ECS authoring.</summary>
    public static class EcsAuthoringRegistry
    {
        /// <summary>Gets the number of registered Main-world providers.</summary>
        public static int Count => EcsAuthoringRegistry<Main>.Count;

        /// <summary>Gets the number of Main-world providers waiting for creation.</summary>
        public static int PendingCount => EcsAuthoringRegistry<Main>.PendingCount;

        /// <summary>Gets the number of Main-world providers with an active entity.</summary>
        public static int ActiveCount => EcsAuthoringRegistry<Main>.ActiveCount;

        /// <summary>Queues Main-world provider creation.</summary>
        public static bool RequestCreate(EcsEntityProvider<Main> provider) =>
            EcsAuthoringRegistry<Main>.RequestCreate(provider);

        /// <summary>Queues Main-world link resolution.</summary>
        public static bool RequestResolve(EcsEntityProvider<Main> provider) =>
            EcsAuthoringRegistry<Main>.RequestResolve(provider);

        /// <summary>Destroys a Main-world provider entity or cancels its pending intent.</summary>
        public static bool RequestDestroy(EcsEntityProvider<Main> provider) =>
            EcsAuthoringRegistry<Main>.RequestDestroy(provider);

        /// <summary>Applies a Main-world provider enable callback.</summary>
        public static void NotifyEnabled(EcsEntityProvider<Main> provider) =>
            EcsAuthoringRegistry<Main>.NotifyEnabled(provider);

        /// <summary>Applies a Main-world provider disable callback.</summary>
        public static void NotifyDisabled(EcsEntityProvider<Main> provider) =>
            EcsAuthoringRegistry<Main>.NotifyDisabled(provider);

        /// <summary>Applies a Main-world provider destruction callback.</summary>
        public static void NotifyDestroyed(
            EcsEntityProvider<Main> provider,
            FFS.Libraries.StaticEcs.Unity.OnDestroyType destroyType) =>
            EcsAuthoringRegistry<Main>.NotifyDestroyed(provider, destroyType);

        /// <summary>Creates one trusted Main-world factory entity atomically.</summary>
        public static bool TryCreateImmediate(
            EcsEntityProvider<Main> provider,
            out EntityGID gid,
            out string reason) =>
            EcsAuthoringRegistry<Main>.TryCreateImmediate(provider, out gid, out reason);

        /// <summary>Drains pending Main-world authoring requests.</summary>
        public static int Drain() => EcsAuthoringRegistry<Main>.Drain();

        /// <summary>Returns the pending diagnostic for one Main-world provider.</summary>
        public static bool TryGetDiagnostic(
            EcsEntityProvider<Main> provider,
            out string diagnostic) =>
            EcsAuthoringRegistry<Main>.TryGetDiagnostic(provider, out diagnostic);

        /// <summary>Finds the first active Main-world provider for one entity kind.</summary>
        public static bool TryGetActiveProvider(
            byte entityType,
            out EcsEntityProvider<Main> provider) =>
            EcsAuthoringRegistry<Main>.TryGetActiveProvider(entityType, out provider);

        /// <summary>Begins Main-world authoring after world initialization.</summary>
        public static void BeginWorld() => EcsAuthoringRegistry<Main>.BeginWorld();

        /// <summary>Stops Main-world authoring drains while teardown systems inspect entities.</summary>
        public static void StopWorld() => EcsAuthoringRegistry<Main>.StopWorld();

        /// <summary>Ends Main-world authoring before world teardown.</summary>
        public static void EndWorld() => EcsAuthoringRegistry<Main>.EndWorld();

        /// <summary>Clears all Main-world authoring state.</summary>
        public static void Clear() => EcsAuthoringRegistry<Main>.Clear();
    }
}
