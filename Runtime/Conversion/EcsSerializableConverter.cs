using System;
using FFS.Libraries.StaticEcs;
using UnityEngine;

namespace UniGame.StaticEcs.Unity
{
#if UNITY_EDITOR
    using UniModules.Editor;
#endif

#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    /// <summary>Base class for inline converters stored through <see cref="SerializeReference"/>.</summary>
    [Serializable]
    public abstract class EcsSerializableConverter<TWorld> : IEcsConverter<TWorld>
        where TWorld : struct, IWorldType
    {
#if ODIN_INSPECTOR
        [InlineButton(nameof(OpenScript),SdfIconType.Folder2Open)]
#endif
        [SerializeField]
        private bool _isEnabled = true;

        /// <inheritdoc />
        public virtual bool IsEnabled => _isEnabled;

        /// <inheritdoc />
        public abstract void Apply(World<TWorld>.Entity entity, GameObject host);
        /// <summary>Opens the converter implementation in the configured script editor.</summary>
        public void OpenScript()
        {
#if UNITY_EDITOR
            GetType().OpenScript();
#endif
        }

        private Color GetButtonColor()
        {
            return _isEnabled ?
                new Color(0.2f, 1f, 0.2f) :
                new Color(1, 0.6f, 0.4f);
        }
    }

}
