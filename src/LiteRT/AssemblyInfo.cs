using System.Runtime.CompilerServices;

// Declared in source (not the csproj) so the grant survives the source sync into
// the Unity package, where the bindings compile as the LiteRT.Managed assembly
// and LiteRT.LM needs access to internals such as NativeRuntime.
[assembly: InternalsVisibleTo("LiteRT.LM")]
