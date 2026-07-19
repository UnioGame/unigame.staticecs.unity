using System;
using System.Collections.Generic;
using FFS.Libraries.StaticEcs;
using UnityEditor;
using UnityEngine;

namespace UniGame.StaticEcs.Editor.Validation
{
    using Unity;

    /// <summary>Inspector and synchronization UI for the Main-world ECS source.</summary>
    [CustomEditor(typeof(StaticEcsServiceSource))]
    public sealed class MainEcsServiceSourceInspector : EcsServiceSourceInspector<Main>
    {
    }

    /// <summary>Draws and validates a feature-first ECS service source.</summary>
    public abstract class EcsServiceSourceInspector<TWorld> : UnityEditor.Editor
        where TWorld : struct, IWorldType
    {
        private FeatureSyncResult _lastSync;
        private bool _hasSyncResult;

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Synchronize Features"))
            {
                Synchronize();
            }

            if (_hasSyncResult)
            {
                EditorGUILayout.HelpBox(
                    $"Added: {_lastSync.added}, removed: {_lastSync.removed}, " +
                    $"wrong world skipped: {_lastSync.wrongWorldSkipped}.",
                    MessageType.Info);
            }

            DrawValidation();
        }

        private void Synchronize()
        {
            var source = (StaticEcsServiceSource<TWorld>)target;
            _lastSync = FeatureConfigurationSynchronizer.SynchronizeProjectAssets(source);
            _hasSyncResult = true;
            serializedObject.Update();
        }

        private void DrawValidation()
        {
            var source = (StaticEcsServiceSource<TWorld>)target;
            var features = source.features;
            if (features == null || features.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No features assigned. The Static ECS world will be initialized without feature logic.",
                    MessageType.Warning);
                return;
            }

            var seen = new HashSet<StaticEcsFeatureAssetBase>();
            var hasDuplicates = false;
            var hasWrongWorld = false;
            var hasNull = false;
            var hasAnyEnabled = false;
            foreach (var entry in features)
            {
                if (entry == null || entry.asset == null)
                {
                    hasNull = true;
                    continue;
                }

                hasDuplicates |= !seen.Add(entry.asset);
                hasWrongWorld |= entry.asset.WorldType != typeof(TWorld);
                hasAnyEnabled |= entry.enabled;
            }

            if (hasNull || hasDuplicates)
            {
                EditorGUILayout.HelpBox(
                    "Null or duplicate feature entries detected. Use Synchronize Features to remove them.",
                    MessageType.Error);
            }

            if (hasWrongWorld)
            {
                EditorGUILayout.HelpBox(
                    $"At least one feature targets a world other than {typeof(TWorld).Name}.",
                    MessageType.Error);
            }

            if (!hasAnyEnabled)
            {
                EditorGUILayout.HelpBox("All configured features are disabled.", MessageType.Warning);
            }
        }
    }

    /// <summary>Summary of one deterministic feature configuration synchronization.</summary>
    public readonly struct FeatureSyncResult
    {
        /// <summary>Creates a synchronization summary.</summary>
        public FeatureSyncResult(int added, int removed, int wrongWorldSkipped)
        {
            this.added = added;
            this.removed = removed;
            this.wrongWorldSkipped = wrongWorldSkipped;
        }

        /// <summary>Number of appended compatible assets.</summary>
        public readonly int added;
        /// <summary>Number of null or duplicate entries removed.</summary>
        public readonly int removed;
        /// <summary>Number of discovered assets skipped due to a world mismatch.</summary>
        public readonly int wrongWorldSkipped;
    }

    /// <summary>Deterministically reconciles a feature list with discovered project assets.</summary>
    public static class FeatureConfigurationSynchronizer
    {
        /// <summary>Finds compatible assets under Assets/, records Undo, and synchronizes the source.</summary>
        public static FeatureSyncResult SynchronizeProjectAssets<TWorld>(StaticEcsServiceSource<TWorld> source)
            where TWorld : struct, IWorldType
        {
            var candidates = new List<StaticEcsFeatureAssetBase>();
            var paths = new List<string>();
            var guids = AssetDatabase.FindAssets("t:StaticEcsFeatureAssetBase", new[] { "Assets" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<StaticEcsFeatureAssetBase>(path);
                if (asset == null)
                {
                    continue;
                }

                candidates.Add(asset);
                paths.Add(path);
            }

            var indexes = new int[candidates.Count];
            for (var i = 0; i < indexes.Length; i++)
            {
                indexes[i] = i;
            }

            Array.Sort(indexes, (left, right) =>
                string.Compare(paths[left], paths[right], StringComparison.Ordinal));
            var ordered = new List<StaticEcsFeatureAssetBase>(candidates.Count);
            for (var i = 0; i < indexes.Length; i++)
            {
                ordered.Add(candidates[indexes[i]]);
            }

            source.features ??= new List<StaticEcsFeatureEntry>();
            Undo.RecordObject(source, "Synchronize Static ECS Features");
            var result = Synchronize<TWorld>(source.features, ordered);
            EditorUtility.SetDirty(source);
            return result;
        }

        /// <summary>Preserves existing order and flags, removes invalid entries, and appends missing assets.</summary>
        public static FeatureSyncResult Synchronize<TWorld>(
            List<StaticEcsFeatureEntry> entries,
            IReadOnlyList<StaticEcsFeatureAssetBase> orderedCandidates)
            where TWorld : struct, IWorldType
        {
            var removed = 0;
            var added = 0;
            var wrongWorld = 0;
            var seen = new HashSet<StaticEcsFeatureAssetBase>();

            for (var i = 0; i < entries.Count;)
            {
                var entry = entries[i];
                if (entry == null || entry.asset == null || !seen.Add(entry.asset))
                {
                    entries.RemoveAt(i);
                    removed++;
                    continue;
                }

                i++;
            }

            for (var i = 0; i < orderedCandidates.Count; i++)
            {
                var candidate = orderedCandidates[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.WorldType != typeof(TWorld))
                {
                    wrongWorld++;
                    continue;
                }

                if (!seen.Add(candidate))
                {
                    continue;
                }

                entries.Add(new StaticEcsFeatureEntry { enabled = true, asset = candidate });
                added++;
            }

            return new FeatureSyncResult(added, removed, wrongWorld);
        }
    }
}
