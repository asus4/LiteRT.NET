using System;

namespace LiteRT.LM.Interop
{
    /// <summary>
    /// Local stand-in for Unity's <c>AOT.MonoPInvokeCallbackAttribute</c>. IL2CPP matches the
    /// attribute by its type <em>name</em> only, so a private definition satisfies it while
    /// keeping these bindings free of UnityEngine references (the Unity package syncs this
    /// source into an asmdef with <c>noEngineReferences: true</c>). Harmless outside Unity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type delegateType) => DelegateType = delegateType;

        public Type DelegateType { get; }
    }
}
