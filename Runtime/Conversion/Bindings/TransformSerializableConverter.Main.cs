namespace UniGame.StaticEcs.Unity
{
    using System;
    using UnityEngine.Scripting.APIUpdating;

    /// <summary>Main-world inline transform converter.</summary>
    [Serializable]
    [MovedFrom(
        true,
        sourceNamespace: "UniGame.StaticEcs.Unity",
        sourceAssembly: "unigame.staticecs.unity",
        sourceClassName: "TransformBindingSerializableConverter"
    )]
    public sealed class TransformSerializableConverter : TransformSerializableConverter<Main> { }
}
