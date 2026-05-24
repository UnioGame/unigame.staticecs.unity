using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;
using UniGame.Context.Runtime;
using UniGame.Core.Runtime;
using UnityEngine;

namespace unigame.staticecs.unity {
    public abstract class StaticEcsServiceSource<TWorld> : ServiceDataSourceAsset<IEcsService>
        where TWorld : struct, IWorldType {
        public StaticEcsWorldConfig world = StaticEcsWorldConfig.Default;
        public StaticEcsSystemsConfig systems = StaticEcsSystemsConfig.Default;
        public List<StaticEcsModuleConfig> modules = new();

        protected override UniTask<IEcsService> CreateServiceInternalAsync(IContext context) {
            var service = new EcsService<TWorld>(world, systems);
            service.Initialize(modules);

            context.Publish(service.Report);

            new EcsRunner<TWorld>(service, systems).Start();

            return UniTask.FromResult<IEcsService>(service);
        }
    }

    [CreateAssetMenu(menuName = "Static ECS/Service Source", fileName = nameof(StaticEcsServiceSource))]
    public sealed class StaticEcsServiceSource : StaticEcsServiceSource<Main> { }
}
