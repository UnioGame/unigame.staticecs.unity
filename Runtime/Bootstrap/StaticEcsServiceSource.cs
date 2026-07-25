using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;
using UniGame.Context.Runtime;
using UniGame.Core.Runtime;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    /// <summary>Creates and runs a configured Static ECS service for one world type.</summary>
    public abstract class StaticEcsServiceSource<TWorld> : ServiceDataSourceAsset<IEcsService>
        where TWorld : struct, IWorldType
    {
        /// <summary>World capacity and initialization configuration.</summary>
        public StaticEcsWorldConfig world = StaticEcsWorldConfig.Default;

        /// <summary>System-group and player-loop configuration.</summary>
        public StaticEcsSystemsConfig systems = StaticEcsSystemsConfig.Default;

#if ODIN_INSPECTOR
        [ListDrawerSettings(ListElementLabelName = "@Name")]
#endif
        /// <summary>Ordered feature assets used to compose the world.</summary>
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
}
