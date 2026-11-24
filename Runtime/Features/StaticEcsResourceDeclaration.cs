namespace UniGame.StaticEcs.Unity
{
    using System;
    using UnityEngine;

    internal sealed class StaticEcsResourceDeclaration
    {
        public StaticEcsResourceDeclaration(
            Type resourceType,
            ScriptableObject asset,
            Func<ScriptableObject, ScriptableObject> createCopy,
            Action<ScriptableObject> publish)
        {
            ResourceType = resourceType;
            Asset = asset;
            CreateCopy = createCopy;
            Publish = publish;
        }

        public Type ResourceType { get; }

        public ScriptableObject Asset { get; }

        public Func<ScriptableObject, ScriptableObject> CreateCopy { get; }

        public Action<ScriptableObject> Publish { get; }
    }
}
