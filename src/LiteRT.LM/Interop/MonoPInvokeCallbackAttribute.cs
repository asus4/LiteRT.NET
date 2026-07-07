using System;

namespace LiteRT.LM.Interop
{
    // Stand-in for Unity's AOT.MonoPInvokeCallbackAttribute: IL2CPP matches the attribute by
    // type name only, so this avoids a UnityEngine reference. Harmless outside Unity.
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type delegateType) => DelegateType = delegateType;

        public Type DelegateType { get; }
    }
}
