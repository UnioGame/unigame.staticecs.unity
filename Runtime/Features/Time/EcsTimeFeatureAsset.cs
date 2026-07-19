using UniGame.Core.Runtime;
using UnityEngine;

namespace UniGame.StaticEcs.Time
{
    using Unity;

    /// <summary>Creates an isolated Main-world ECS time feature.</summary>
    [CreateAssetMenu(menuName = "Static ECS/Features/Time", fileName = nameof(EcsTimeFeatureAsset))]
    public sealed class EcsTimeFeatureAsset : StaticEcsFeatureAsset
    {
        /// <summary>Order of the variable-step time system.</summary>
        public short updateOrder = EcsTimeFeature<Main>.DefaultUpdateOrder;
        /// <summary>Order of the fixed-step time system.</summary>
        public short fixedOrder = EcsTimeFeature<Main>.DefaultUpdateOrder;
        /// <summary>Whether the fixed-step time system is registered.</summary>
        public bool registerFixed = true;

        /// <inheritdoc />
        public override IStaticEcsFeature<Main> CreateFeature(IContext context)
        {
            return new EcsTimeFeature<Main>(updateOrder, fixedOrder, registerFixed);
        }
    }
}
