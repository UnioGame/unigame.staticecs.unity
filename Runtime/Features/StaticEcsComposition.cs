namespace UniGame.StaticEcs.Unity
{
    using System;
    using System.Collections.Generic;
    using FFS.Libraries.StaticEcs;
    using UnityEngine;

    /// <summary>Ordered code and asset feature declarations used to compose one Static ECS world.</summary>
    /// <remarks>
    /// Declarations are factories: <see cref="EcsService{TWorld}"/> creates fresh code
    /// features and fresh asset clones for every initialization. Declaration order is the
    /// order in which feature pipelines start; system order is defined by system groups.
    /// A programmatic feature type may be declared once per world; a duplicate fails in
    /// <c>Add</c>.
    /// </remarks>
    public class StaticEcsComposition<TWorld>
        where TWorld : struct, IWorldType
    {
        private readonly List<StaticEcsFeatureDeclaration<TWorld>> _declarations = new();
        private readonly List<StaticEcsResourceDeclaration> _resources = new();

        /// <summary>Gets the number of declared features.</summary>
        public int Count => _declarations.Count;

        /// <summary>Gets a read-only view of the feature declared at <paramref name="index"/>.</summary>
        public StaticEcsCompositionEntry this[int index]
        {
            get
            {
                var declaration = _declarations[index];
                return new StaticEcsCompositionEntry(declaration.FeatureType, declaration.Asset);
            }
        }

        internal IReadOnlyList<StaticEcsFeatureDeclaration<TWorld>> Declarations =>
            _declarations;

        internal IReadOnlyList<StaticEcsResourceDeclaration> Resources => _resources;

        /// <summary>Declares a ScriptableObject resource published before features initialize.</summary>
        /// <remarks>
        /// The service publishes a runtime copy, so the asset never changes in Play Mode, and
        /// destroys the copy with the world. A resource type may be declared once per world.
        /// </remarks>
        public void AddResource<TResource>(TResource asset)
            where TResource : ScriptableObject, IResource
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            for (var i = 0; i < _resources.Count; i++)
            {
                if (_resources[i].ResourceType == typeof(TResource))
                    throw new InvalidOperationException(
                        $"Resource `{typeof(TResource).Name}` is already declared for `{typeof(TWorld).Name}`.");
            }

            var declaration = new StaticEcsResourceDeclaration(
                typeof(TResource),
                asset,
                static source =>
                {
                    var copy = UnityEngine.Object.Instantiate((TResource)source);
                    copy.hideFlags = HideFlags.DontSave;
                    return copy;
                },
                static copy =>
                {
                    var resource = (TResource)copy;
                    World<TWorld>.SetResource(resource);
                });

            _resources.Add(declaration);
        }

        /// <summary>Declares a code feature created fresh for every world initialization.</summary>
        public void Add<TFeature>()
            where TFeature : class, IStaticEcsFeature<TWorld>, new()
        {
            var featureType = typeof(TFeature);
            RejectDuplicate(featureType);

            var declaration = new StaticEcsFeatureDeclaration<TWorld>(
                null,
                static () => new TFeature(),
                featureType,
                featureType.Name);

            _declarations.Add(declaration);
        }

        /// <summary>Declares a feature asset cloned for every world initialization.</summary>
        public void Add(StaticEcsFeatureAssetBase asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            if (asset.WorldType != typeof(TWorld))
                throw new InvalidOperationException(
                    $"Feature asset `{asset.name}` targets `{asset.WorldType.Name}`, expected `{typeof(TWorld).Name}`.");

            for (var i = 0; i < _declarations.Count; i++)
            {
                if (_declarations[i].Asset == asset)
                    throw new InvalidOperationException($"Feature asset `{asset.name}` is listed more than once.");
            }

            var featureType = asset.ProgrammaticFeatureType;
            if (featureType != null)
                RejectDuplicate(featureType);

            var declaration = new StaticEcsFeatureDeclaration<TWorld>(
                asset,
                null,
                featureType,
                asset.FeatureName);

            _declarations.Add(declaration);
        }

        /// <summary>Declares the enabled assets of serialized entries; null and disabled entries are skipped.</summary>
        public void AddEntries(IReadOnlyList<StaticEcsFeatureEntry> entries)
        {
            if (entries == null)
                return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.IsEnabled || entry.asset == null)
                    continue;

                Add(entry.asset);
            }
        }

        private void RejectDuplicate(Type featureType)
        {
            for (var i = 0; i < _declarations.Count; i++)
            {
                if (_declarations[i].FeatureType == featureType)
                    throw new InvalidOperationException(
                        $"Feature `{featureType.Name}` is already declared for `{typeof(TWorld).Name}`.");
            }
        }
    }
}
