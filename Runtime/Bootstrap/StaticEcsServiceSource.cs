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

        public List<StaticEcsFeatureEntry> features = new();

        protected override async UniTask<IEcsService> CreateServiceInternalAsync(IContext context)
        {
            var lifeTime = context.LifeTime;
            var service = new EcsService<TWorld>(world, systems);
            try
            {
                await service.InitializeAsync(features, context, lifeTime.Token);
                context.Publish(service.Report);

                var runner = new EcsRunner<TWorld>(service, systems).AddTo(lifeTime);
                runner.Start();

                context.Publish(runner);
                return service;
            }
            catch
            {
                service.Dispose();
                throw;
            }
        }
    }

    [CreateAssetMenu(menuName = "Static ECS/Service Source", fileName = nameof(StaticEcsServiceSource))]
    public sealed class StaticEcsServiceSource : StaticEcsServiceSource<Main> { }
}
