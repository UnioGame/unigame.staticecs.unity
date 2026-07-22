namespace UniGame.StaticEcs.Unity
{
    using FFS.Libraries.StaticEcs;
    using UnityEngine;

    /// <summary>Stores the Unity transform associated with an ECS entity.</summary>
    public struct TransformComponent : IComponent
    {
        /// <summary>The associated Unity transform.</summary>
        public Transform Transform;
    }
}
