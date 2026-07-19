using FFS.Libraries.StaticEcs.Unity;
using FFS.Libraries.StaticEcs.Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace UniGame.StaticEcs.Editor.View.Tabs {
    using Unity;

    public sealed class BootstrapReportTab : IStaticEcsViewTab {
        private const string TabName = "Bootstrap Report";

        public string Name() => TabName;

        public void Init() { }

        public void OnWorldChanged(AbstractWorldData newWorldData) { }

        public void Destroy() { }

        public void Draw() {
            EditorGUILayout.LabelField("EcsStartupReport", EditorStyles.boldLabel);

            var report = EcsServiceRegistry.LastReport;
            if (report == null) {
                EditorGUILayout.HelpBox(
                    "No EcsService is currently registered. Reports become available once a StaticEcsServiceSource publishes a service into the GameContext during play mode.",
                    MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUILayout.Toggle("World Created", report.worldCreated);
                EditorGUILayout.Toggle("Types Registered", report.typesRegistered);
                EditorGUILayout.Toggle("World Initialized", report.worldInitialized);
                EditorGUILayout.Toggle("Systems Initialized", report.systemsInitialized);
                EditorGUILayout.IntField("Features Registered", report.featuresRegistered);
                EditorGUILayout.EnumPopup("Stage", report.stage);
                EditorGUILayout.EnumPopup("Failed Stage", report.failedStage);
                EditorGUILayout.TextField("Current Feature", report.currentFeature ?? string.Empty);
                EditorGUILayout.TextField("Failed Feature", report.failedFeature ?? string.Empty);
                EditorGUILayout.IntField("Update Count", report.updateCount);
                EditorGUILayout.LabelField("Message", report.message ?? string.Empty);
                EditorGUILayout.LabelField("Success", report.IsSuccess.ToString());
            }

            if (GUILayout.Button("Repaint")) {
                EditorWindow.focusedWindow?.Repaint();
            }
        }
    }
}
