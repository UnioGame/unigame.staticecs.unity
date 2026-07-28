using System.Collections.Generic;
using FFS.Libraries.StaticEcs.Unity;
using FFS.Libraries.StaticEcs.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace UniGame.StaticEcs.Editor.View.Tabs
{
    using Unity;

    /// <summary>Lists feature-first ECS service sources and their ordered entries.</summary>
    public sealed class GameFeaturesTab : IStaticEcsViewTab
    {
        private readonly List<ScriptableObject> _sources = new();
        private Vector2 _scroll;

        /// <inheritdoc />
        public string Name() => "Game Features";

        /// <inheritdoc />
        public void Init() => RefreshSources();

        /// <inheritdoc />
        public void OnWorldChanged(AbstractWorldData newWorldData) { }

        /// <inheritdoc />
        public void Destroy() => _sources.Clear();

        /// <inheritdoc />
        public void Draw()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Service sources: {_sources.Count}", EditorStyles.boldLabel);
                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                    RefreshSources();
            }

            if (_sources.Count == 0)
            {
                EditorGUILayout.HelpBox("No StaticEcsServiceSource assets found under Assets/.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var source in _sources)
            {
                DrawSource(source);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawSource(ScriptableObject source)
        {
            if (source == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(source.name, EditorStyles.boldLabel);
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                        Selection.activeObject = source;
                }

                var serialized = new SerializedObject(source);
                var features = serialized.FindProperty("features");
                EditorGUILayout.LabelField($"Features: {features.arraySize}");
                for (var i = 0; i < features.arraySize; i++)
                {
                    var entry = features.GetArrayElementAtIndex(i);
                    var enabled = entry.FindPropertyRelative("enabled").boolValue;
                    var asset = entry.FindPropertyRelative("asset").objectReferenceValue as StaticEcsFeatureAssetBase;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{(enabled ? "●" : "○")} {(asset == null ? "(missing feature)" : asset.FeatureName)}");
                        if (asset != null && GUILayout.Button("Ping", GUILayout.Width(60)))
                            EditorGUIUtility.PingObject(asset);
                    }
                }
            }
        }

        private void RefreshSources()
        {
            _sources.Clear();
            var guids = AssetDatabase.FindAssets("t:StaticEcsServiceSource", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var source = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ScriptableObject;
                if (source != null)
                    _sources.Add(source);
            }
        }
    }
}
