using UniGame.GameFlow.Runtime;

namespace unigame.staticecs.unity {
    public interface IEcsService : IGameService {
        EcsStartupReport Report { get; }

        bool IsInitialized { get; }

        void Update();

        void FixedUpdate();

        void LateUpdate();

        void CleanupUpdate();
    }
}
