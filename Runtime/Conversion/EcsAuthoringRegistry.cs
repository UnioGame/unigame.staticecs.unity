using System;
using System.Collections.Generic;
using System.Text;
using FFS.Libraries.StaticEcs;
using FFS.Libraries.StaticEcs.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Coordinates deterministic provider intent and owns provider-created entities for one world.</summary>
    public static class EcsAuthoringRegistry<TWorld>
        where TWorld : struct, IWorldType
    {
        private static readonly Dictionary<EcsEntityProvider<TWorld>, Entry> Entries = new();
        private static readonly List<Entry> Batch = new();
        private static readonly List<Entry> Created = new();
        private static bool _worldAcceptsAuthoring;

        /// <summary>Gets the number of registered providers.</summary>
        public static int Count => Entries.Count;

        /// <summary>Gets the number of providers waiting for creation.</summary>
        public static int PendingCount
        {
            get
            {
                var count = 0;
                foreach (var pair in Entries)
                {
                    RefreshActiveState(pair.Value);
                    if (pair.Value.PendingCreate)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Gets the number of providers with an active entity.</summary>
        public static int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var pair in Entries)
                {
                    RefreshActiveState(pair.Value);
                    if (pair.Value.Active)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Registers persistent creation intent without mutating the world.</summary>
        public static bool RequestCreate(EcsEntityProvider<TWorld> provider)
        {
            if (provider == null)
                return false;

            var entry = GetOrCreate(provider);
            entry.CreationRequested = true;
            entry.RetryBlocked = false;
            entry.Diagnostic = string.Empty;
            RefreshActiveState(entry);
            entry.PendingCreate = provider.isActiveAndEnabled && !entry.Active;
            return true;
        }

        /// <summary>Queues link resolution for one active provider.</summary>
        public static bool RequestResolve(EcsEntityProvider<TWorld> provider)
        {
            if (provider == null || !Entries.TryGetValue(provider, out var entry) || !entry.Active)
                return false;

            entry.PendingResolve = true;
            return true;
        }

        /// <summary>Destroys active state or cancels pending intent through the registry.</summary>
        public static bool RequestDestroy(EcsEntityProvider<TWorld> provider)
        {
            if (provider == null)
                return false;

            Entries.Remove(provider, out var entry);
            if (entry == null)
            {
                provider.DetachEntityNow();
                return false;
            }

            return Destroy(entry);
        }

        /// <summary>Creates one trusted factory entity atomically outside scene batch ordering.</summary>
        public static bool TryCreateImmediate(
            EcsEntityProvider<TWorld> provider,
            out EntityGID gid,
            out string reason)
        {
            gid = default;
            reason = string.Empty;
            if (provider == null)
            {
                reason = "Provider is missing.";
                return false;
            }

            if (World<TWorld>.Status != WorldStatus.Initialized)
            {
                reason = $"World {typeof(TWorld).Name} is not initialized.";
                return false;
            }

            var entry = GetOrCreate(provider);
            entry.CreationRequested = true;
            entry.RetryBlocked = false;
            entry.Diagnostic = string.Empty;
            RefreshActiveState(entry);
            if (entry.Active)
            {
                gid = provider.EntityGid;
                return gid != default;
            }

            if (!provider.CanCreate(out reason))
            {
                entry.Diagnostic = reason;
                return false;
            }

            try
            {
                if (!provider.CreateEntityNow())
                {
                    reason = "Provider did not create an entity.";
                    entry.Diagnostic = reason;
                    return false;
                }

                provider.ResolveLinksNow();
                provider.InvokeCreatedNow();
                entry.Active = true;
                entry.PendingCreate = false;
                entry.PendingResolve = false;
                entry.Diagnostic = string.Empty;
                gid = provider.EntityGid;
                return true;
            }
            catch (Exception exception)
            {
                TryRollback(entry);
                reason = exception.Message;
                entry.Diagnostic = reason;
                entry.PendingCreate = false;
                entry.RetryBlocked = true;
                return false;
            }
        }

        /// <summary>Marks an enabled provider for its previously requested lifecycle.</summary>
        public static void NotifyEnabled(EcsEntityProvider<TWorld> provider)
        {
            if (provider == null || !Entries.TryGetValue(provider, out var entry))
                return;

            if (entry.Active)
            {
                if (provider.onEnableAndDisable)
                    entry.PendingEnable = true;
                return;
            }

            if (entry.CreationRequested)
            {
                entry.RetryBlocked = false;
                entry.Diagnostic = string.Empty;
                entry.PendingCreate = true;
            }
        }

        /// <summary>Cancels undrained creation and queues entity disable when configured.</summary>
        public static void NotifyDisabled(EcsEntityProvider<TWorld> provider)
        {
            if (provider == null || !Entries.TryGetValue(provider, out var entry))
                return;

            entry.PendingCreate = false;
            if (entry.Active && provider.onEnableAndDisable)
                entry.PendingDisable = true;
        }

        /// <summary>Unregisters a destroyed provider and applies its configured entity ownership.</summary>
        public static void NotifyDestroyed(
            EcsEntityProvider<TWorld> provider,
            OnDestroyType destroyType)
        {
            if (provider == null)
                return;

            Entries.Remove(provider, out var entry);
            if (destroyType == OnDestroyType.DestroyEntity)
            {
                if (entry != null)
                    Destroy(entry);
                else
                    provider.DestroyEntityNow();
                return;
            }

            provider.DetachEntityNow();
        }

        /// <summary>Begins one initialized world and restores enabled persistent requests.</summary>
        public static void BeginWorld()
        {
            _worldAcceptsAuthoring = true;
            foreach (var pair in Entries)
            {
                var entry = pair.Value;
                entry.Active = false;
                entry.RetryBlocked = false;
                entry.Diagnostic = string.Empty;
                entry.PendingEnable = false;
                entry.PendingDisable = false;
                entry.PendingResolve = false;
                if (entry.Provider != null)
                    entry.Provider.DetachEntityNow();
                entry.PendingCreate = entry.CreationRequested &&
                                      entry.Provider != null &&
                                      entry.Provider.isActiveAndEnabled;
            }
        }

        /// <summary>Applies one deterministic provider batch at a service boundary.</summary>
        public static int Drain()
        {
            if (!_worldAcceptsAuthoring || World<TWorld>.Status != WorldStatus.Initialized)
                return 0;

            ApplyStateIntents();
            Batch.Clear();
            foreach (var pair in Entries)
            {
                var entry = pair.Value;
                RefreshActiveState(entry);
                if (entry.PendingCreate && !entry.RetryBlocked &&
                    !entry.Active && entry.Provider != null)
                    Batch.Add(entry);
            }

            Batch.Sort(EntryComparer.Instance);
            Created.Clear();
            var progress = true;
            while (progress)
            {
                progress = false;
                for (var index = 0; index < Batch.Count; index++)
                {
                    var entry = Batch[index];
                    if (!entry.PendingCreate || entry.Active || entry.Provider == null)
                        continue;
                    if (!entry.Provider.CanCreate(out var reason))
                    {
                        entry.Diagnostic = reason;
                        continue;
                    }

                    try
                    {
                        if (!entry.Provider.CreateEntityNow())
                        {
                            entry.Diagnostic = "Provider did not create an entity.";
                            continue;
                        }

                        entry.Active = true;
                        entry.PendingCreate = false;
                        entry.Diagnostic = string.Empty;
                        Created.Add(entry);
                        progress = true;
                    }
                    catch (Exception exception)
                    {
                        FailEntry(
                            entry,
                            $"Provider creation failed: {exception.Message}");
                        RollbackCreated(
                            $"Creation batch rolled back: {exception.Message}",
                            entry);
                        return 0;
                    }
                }
            }

            try
            {
                for (var index = 0; index < Created.Count; index++)
                {
                    var entry = Created[index];
                    try
                    {
                        entry.Provider.ResolveLinksNow();
                    }
                    catch (Exception exception)
                    {
                        FailEntry(
                            entry,
                            $"Provider link resolution failed: {exception.Message}");
                        throw;
                    }
                }

                for (var index = 0; index < Created.Count; index++)
                {
                    var entry = Created[index];
                    try
                    {
                        entry.Provider.InvokeCreatedNow();
                    }
                    catch (Exception exception)
                    {
                        FailEntry(
                            entry,
                            $"Provider activation failed: {exception.Message}");
                        throw;
                    }
                }
            }
            catch (Exception exception)
            {
                RollbackCreated(
                    $"Activation batch rolled back: {exception.Message}",
                    FindFailedEntry());
                return 0;
            }

            ResolvePendingLinks();
            return Created.Count;
        }

        /// <summary>Returns the pending diagnostic for one registered provider.</summary>
        public static bool TryGetDiagnostic(
            EcsEntityProvider<TWorld> provider,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (provider == null || !Entries.TryGetValue(provider, out var entry))
                return false;

            RefreshActiveState(entry);
            diagnostic = entry.Diagnostic ?? string.Empty;
            return diagnostic.Length > 0;
        }

        /// <summary>Finds the first active provider for one entity kind in deterministic order.</summary>
        public static bool TryGetActiveProvider(
            byte entityType,
            out EcsEntityProvider<TWorld> provider)
        {
            provider = null;
            Batch.Clear();
            foreach (var pair in Entries)
            {
                var entry = pair.Value;
                RefreshActiveState(entry);
                if (entry.Active && entry.Provider != null &&
                    entry.Provider.EntityTypeId == entityType)
                    Batch.Add(entry);
            }
            if (Batch.Count == 0)
                return false;
            Batch.Sort(EntryComparer.Instance);
            provider = Batch[0].Provider;
            return true;
        }

        /// <summary>Stops authoring drains while systems still have access to provider entities.</summary>
        public static void StopWorld() => _worldAcceptsAuthoring = false;

        /// <summary>Destroys provider-owned entities before world teardown while retaining enabled requests.</summary>
        public static void EndWorld()
        {
            StopWorld();
            foreach (var pair in Entries)
            {
                var entry = pair.Value;
                if (entry.Active)
                    TryRollback(entry);
                else if (entry.Provider != null)
                    entry.Provider.DetachEntityNow();

                entry.Active = false;
                entry.PendingCreate = entry.CreationRequested &&
                                      entry.Provider != null &&
                                      entry.Provider.isActiveAndEnabled;
                entry.PendingEnable = false;
                entry.PendingDisable = false;
                entry.PendingResolve = false;
                entry.RetryBlocked = false;
            }
        }

        /// <summary>Clears all registry state for tests or domain-owned shutdown.</summary>
        public static void Clear()
        {
            EndWorld();
            Entries.Clear();
            Batch.Clear();
            Created.Clear();
        }

        private static Entry GetOrCreate(EcsEntityProvider<TWorld> provider)
        {
            if (Entries.TryGetValue(provider, out var entry))
                return entry;

            entry = new Entry(provider);
            Entries.Add(provider, entry);
            return entry;
        }

        private static void RefreshActiveState(Entry entry)
        {
            if (entry == null || !entry.Active)
                return;

            var provider = entry.Provider;
            if (provider != null && provider.EntityGid != default &&
                provider.EntityGid.TryUnpack<TWorld>(out _))
                return;

            entry.Active = false;
            entry.PendingEnable = false;
            entry.PendingDisable = false;
            entry.PendingResolve = false;
            provider?.DetachEntityNow();
            entry.PendingCreate = entry.CreationRequested &&
                                  provider != null &&
                                  provider.isActiveAndEnabled;
            entry.Diagnostic = entry.PendingCreate
                ? "Entity was removed outside authoring; recreation is pending."
                : string.Empty;
        }

        private static void ApplyStateIntents()
        {
            foreach (var pair in Entries)
            {
                var entry = pair.Value;
                if (!entry.Active || entry.Provider == null)
                    continue;

                if (entry.PendingDisable)
                {
                    entry.Provider.SetEntityEnabledNow(false);
                    entry.PendingDisable = false;
                }

                if (entry.PendingEnable)
                {
                    entry.Provider.SetEntityEnabledNow(true);
                    entry.PendingEnable = false;
                }
            }
        }

        private static void ResolvePendingLinks()
        {
            foreach (var pair in Entries)
            {
                var entry = pair.Value;
                if (!entry.Active || !entry.PendingResolve || entry.Provider == null)
                    continue;

                entry.Provider.ResolveLinksNow();
                entry.PendingResolve = false;
            }
        }

        private static bool Destroy(Entry entry)
        {
            entry.PendingCreate = false;
            entry.PendingEnable = false;
            entry.PendingDisable = false;
            entry.PendingResolve = false;
            entry.CreationRequested = false;
            if (entry.Provider == null)
                return false;

            var destroyed = entry.Provider.DestroyEntityNow();
            entry.Active = false;
            return destroyed;
        }

        private static void RollbackCreated(string diagnostic, Entry failedEntry = null)
        {
            for (var index = Created.Count - 1; index >= 0; index--)
            {
                var entry = Created[index];
                TryRollback(entry);
                if (ReferenceEquals(entry, failedEntry))
                {
                    entry.PendingCreate = false;
                    entry.RetryBlocked = true;
                }
                else
                {
                    entry.PendingCreate = entry.CreationRequested &&
                                          entry.Provider != null &&
                                          entry.Provider.isActiveAndEnabled;
                    entry.Diagnostic = diagnostic;
                }
            }

            Created.Clear();
        }

        private static void FailEntry(Entry entry, string diagnostic)
        {
            entry.PendingCreate = false;
            entry.RetryBlocked = true;
            entry.Diagnostic = diagnostic;
        }

        private static Entry FindFailedEntry()
        {
            for (var index = 0; index < Created.Count; index++)
                if (Created[index].RetryBlocked)
                    return Created[index];
            return null;
        }

        private static void TryRollback(Entry entry)
        {
            try
            {
                entry.Provider?.DestroyEntityNow();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                entry.Active = false;
            }
        }

        private sealed class Entry
        {
            internal Entry(EcsEntityProvider<TWorld> provider)
            {
                Provider = provider;
            }

            internal readonly EcsEntityProvider<TWorld> Provider;
            internal bool CreationRequested;
            internal bool PendingCreate;
            internal bool PendingEnable;
            internal bool PendingDisable;
            internal bool PendingResolve;
            internal bool Active;
            internal bool RetryBlocked;
            internal string Diagnostic;
        }

        private sealed class EntryComparer : IComparer<Entry>
        {
            internal static readonly EntryComparer Instance = new();

            public int Compare(Entry left, Entry right)
            {
                if (ReferenceEquals(left, right))
                    return 0;

                var scene = string.Compare(
                    ScenePath(left.Provider),
                    ScenePath(right.Provider),
                    StringComparison.Ordinal);
                if (scene != 0)
                    return scene;

                var hierarchy = string.Compare(
                    HierarchyPath(left.Provider),
                    HierarchyPath(right.Provider),
                    StringComparison.Ordinal);
                if (hierarchy != 0)
                    return hierarchy;

                return ComponentIndex(left.Provider).CompareTo(ComponentIndex(right.Provider));
            }

            private static string ScenePath(EcsEntityProvider<TWorld> provider)
            {
                if (provider == null)
                    return string.Empty;

                var scene = provider.gameObject.scene;
                return scene.IsValid() ? scene.path : string.Empty;
            }

            private static string HierarchyPath(EcsEntityProvider<TWorld> provider)
            {
                if (provider == null)
                    return string.Empty;

                var stack = new Stack<Transform>();
                var current = provider.transform;
                while (current != null)
                {
                    stack.Push(current);
                    current = current.parent;
                }

                var result = new StringBuilder();
                while (stack.Count > 0)
                {
                    var transform = stack.Pop();
                    result.Append('/');
                    result.Append(transform.GetSiblingIndex().ToString("D6"));
                    result.Append(':');
                    result.Append(transform.name);
                }

                return result.ToString();
            }

            private static int ComponentIndex(EcsEntityProvider<TWorld> provider)
            {
                if (provider == null)
                    return int.MaxValue;

                var components = provider.GetComponents<Component>();
                for (var index = 0; index < components.Length; index++)
                {
                    if (ReferenceEquals(components[index], provider))
                        return index;
                }

                return int.MaxValue;
            }
        }
    }
}
