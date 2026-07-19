using System.Collections.Generic;
using FFS.Libraries.StaticEcs;
using UnityEditor;

namespace UniGame.StaticEcs.Editor.Validation {
    using Unity;

    public abstract class EcsServiceSourceInspector<TWorld> : UnityEditor.Editor
        where TWorld : struct, IWorldType {
        public override void OnInspectorGUI() {

            base.OnInspectorGUI();

            DrawValidation();
        }

        private void DrawValidation() {

            var source = (StaticEcsServiceSource<TWorld>)target;


            var modules = source.modules;

            if (modules == null || modules.Count == 0) {
                EditorGUILayout.HelpBox(
                    "No modules assigned. Static ECS world will be initialized empty.",
                    MessageType.Warning);
                return;
            }

            var seen = new HashSet<StaticEcsModuleConfig>();
            var hasDuplicates = false;
            var hasWrongWorld = false;
            var hasAnyEnabled = false;

            foreach (var module in modules) {
                if (module == null) {
                    continue;
                }

                if (!seen.Add(module)) {
                    hasDuplicates = true;
                }

                if (module is not StaticEcsModuleConfig<TWorld>) {
                    hasWrongWorld = true;
                }

                if (module.enabled) {
                    hasAnyEnabled = true;
                }
            }

            if (hasDuplicates) {
                EditorGUILayout.HelpBox(
                    "Duplicate module entries detected. Each module should appear only once.",
                    MessageType.Error);
            }

            if (hasWrongWorld) {
                EditorGUILayout.HelpBox(
                    $"At least one module is not a StaticEcsModuleConfig<{typeof(TWorld).Name}>. It will be ignored at runtime.",
                    MessageType.Error);
            }

            if (!hasAnyEnabled) {
                EditorGUILayout.HelpBox(
                    "All modules are disabled. The Static ECS world will be initialized without registered types.",
                    MessageType.Warning);
            }
        }
    }
}
