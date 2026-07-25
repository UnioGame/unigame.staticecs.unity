using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity {
    using UniGame.Runtime.DataFlow;

    /// <summary>Runs one world's system groups on their configured Unity player-loop timings.</summary>
    public class EcsRunner<TWorld> : IDisposable
        where TWorld : struct, IWorldType {
        private readonly EcsService<TWorld> _service;
        private readonly StaticEcsSystemsConfig _config;
        private LifeTime _lifeTime = new();
        private bool _started;

        /// <summary>Creates a runner for the supplied initialized service and timing config.</summary>
        public EcsRunner(EcsService<TWorld> service, StaticEcsSystemsConfig config) {
            _service = service;
            _config = config;
        }

        /// <summary>Starts each enabled loop once; repeated calls are ignored.</summary>
        public void Start() {
            if (_started || !_lifeTime.IsAlive) {
                return;
            }

            _started = true;
            var token = _service.LifeTime.Token;
            if (_config.update) {
                RunAsync("Update", _service.Update, _config.updateTiming, token).Forget();
            }

            if (_config.fixedUpdate) {
                RunAsync("FixedUpdate", _service.FixedUpdate, _config.fixedUpdateTiming, token)
                    .Forget();
            }

            if (_config.lateUpdate) {
                RunAsync("LateUpdate", _service.LateUpdate, _config.lateUpdateTiming, token)
                    .Forget();
            }

            if (_config.cleanup) {
                RunAsync("Cleanup", _service.CleanupUpdate, _config.cleanupTiming, token)
                    .Forget();
            }

            RunAsync("WorldTick", _service.AdvanceTick, _config.tickTiming, token).Forget();
        }

        private async UniTaskVoid RunAsync(
            string group,
            Action tick,
            PlayerLoopTiming timing,
            CancellationToken token) {
            try {
                while (Application.isPlaying && _service.IsInitialized && _lifeTime.IsAlive) {
                    tick();
                    await UniTask.Yield(timing, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) {
                _lifeTime.Terminate();
                _service.RecordRuntimeFault(group, exception);
                Debug.LogException(exception);
            }
        }

        /// <summary>Stops all loops owned by this runner.</summary>
        public void Dispose()
        {
            _lifeTime.Terminate();
            _started = false;
        }
    }
}
