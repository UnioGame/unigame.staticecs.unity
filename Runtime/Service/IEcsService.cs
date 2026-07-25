using UniGame.GameFlow.Runtime;

namespace UniGame.StaticEcs.Unity {
    public interface IEcsService : IGameService {
        EcsStartupReport Report { get; }

        bool IsInitialized { get; }

        void Update();

        void FixedUpdate();

        void LateUpdate();

        void CleanupUpdate();

        void AdvanceTick();
    }
}
