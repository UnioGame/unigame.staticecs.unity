using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;
using UniGame.Context.Runtime;
using UniGame.Core.Runtime;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
    public abstract class StaticEcsServiceSource<TWorld> : ServiceDataSourceAsset<IEcsService>
        where TWorld : struct, IWorldType
    {
        public StaticEcsWorldConfig world = StaticEcsWorldConfig.Default;
        public StaticEcsSystemsConfig systems = StaticEcsSystemsConfig.Default;

        public List<StaticEcsModuleConfig> modules = new();

        protected override UniTask<IEcsService> CreateServiceInternalAsync(IContext context)
        {
            var lifeTime = context.LifeTime;
            var service = new EcsService<TWorld>(world, systems);

            service.Initialize(modules);
            context.Publish(service.Report);

            var runner = new EcsRunner<TWorld>(service, systems).AddTo(lifeTime);
            runner.Start();

            context.Publish(runner);
            return UniTask.FromResult<IEcsService>(service);
        }
    }

    [CreateAssetMenu(menuName = "Static ECS/Service Source", fileName = nameof(StaticEcsServiceSource))]
    public sealed class StaticEcsServiceSource : StaticEcsServiceSource<Main> { }
}
