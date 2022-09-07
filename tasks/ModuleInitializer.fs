#if !NETCOREAPP2_0
namespace Hawaii.Tasks

type ModuleInitializer () =
    [<CompiledName("Initialize")>]
    static member public initialize() = AssemblyResolver.enable()
#endif
    