using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity {
    using UniGame.Runtime.DataFlow;

    public sealed class EcsRunner<TWorld> : IDisposable
        where TWorld : struct, IWorldType {
        private readonly EcsService<TWorld> _service;
        private readonly StaticEcsSystemsConfig _config;
        private LifeTime _lifeTime = new();

        public EcsRunner(EcsService<TWorld> service, StaticEcsSystemsConfig config) {
            _service = service;
            _config = config;
        }

        public void Start() {
            var token = _service.LifeTime.Token;
            if (_config.update)      RunAsync(_service.Update,        _config.updateTiming,      token).Forget();
            if (_config.fixedUpdate) RunAsync(_service.FixedUpdate,   _config.fixedUpdateTiming, token).Forget();
            if (_config.lateUpdate)  RunAsync(_service.LateUpdate,    _config.lateUpdateTiming,  token).Forget();
            if (_config.cleanup)     RunAsync(_service.CleanupUpdate, _config.cleanupTiming,     token).Forget();
        }

        private async UniTaskVoid RunAsync(Action tick, PlayerLoopTiming timing, CancellationToken token) {
            try {
                while (Application.isPlaying && _service.IsInitialized && _lifeTime.IsAlive) {
                    tick();
                    await UniTask.Yield(timing, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void Dispose()
        {
            _lifeTime.Terminate();
        }
    }
}
