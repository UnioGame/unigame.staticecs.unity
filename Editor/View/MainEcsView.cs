using unigame.staticecs.unity;
using UnityEditor;

namespace UniGame.StaticEcs.Editor.View {
    public sealed class MainEcsView : EcsView<Main, StaticEcsEntityProvider, StaticEcsEventProvider> {
        [MenuItem("UniGame/Static ECS/Open View %#e", priority = 100)]
        public static void Open() {
            var window = GetWindow<MainEcsView>();
            window.Init();
            window.Show();
            window.Focus();
        }
    }
}
