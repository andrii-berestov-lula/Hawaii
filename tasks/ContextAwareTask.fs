namespace Hawaii.Tasks


open System;
open System.IO;
open System.Linq;
open System.Reflection;
open System.Runtime.Loader;
open Microsoft.Build.Framework;
open Microsoft.Build.Utilities
[<AbstractClass>]
type public ContextAwareTask () as cat =
    inherit Task()

    let ``type`` = cat.GetType()
    let typeInfo = ``type``.GetTypeInfo()

    abstract ManagedDllDirectory : string with get
    default _.ManagedDllDirectory
        with get() =
            let location = typeInfo.Assembly.Location
            
            let uri = Uri(location)
            Path.GetDirectoryName(uri.LocalPath)

    abstract UnmanagedDllDirectory : string with get
    default _.UnmanagedDllDirectory with get () = null

    abstract member ExecuteInner: unit -> bool

    override this.Execute() =
        let taskAssemblyPath = Uri(typeInfo.Assembly.Location).LocalPath
        let ctxt = CustomAssemblyLoader(this)
        let inContextAssembly = ctxt.LoadFromAssemblyPath(taskAssemblyPath)
        let innerTaskType = inContextAssembly.GetType(``type``.FullName)
        let innerTask = Activator.CreateInstance(innerTaskType)

        let outerProperties = ``type``.GetRuntimeProperties().ToDictionary(fun i -> i.Name);
        let innerProperties = innerTaskType.GetRuntimeProperties().ToDictionary(fun i -> i.Name);
        let propertiesDiscovery =
            outerProperties.Values
            |> Seq.filter (fun outerProperty -> outerProperty.SetMethod <> null && outerProperty.GetMethod <> null)
            |> Seq.map
                (fun outerProperty ->
                    let innerProperty = innerProperties.[outerProperty.Name]
                    (outerProperty, innerProperty))
        let propertiesMap = propertiesDiscovery |> Seq.toArray
        let outputPropertiesMap =
            propertiesDiscovery
            |> Seq.filter (fun (outerProperty, _) -> outerProperty.GetCustomAttribute<OutputAttribute>() <> null)

        let propertiesMap =
            propertiesMap
            |> Seq.map
                (fun pair ->
                    let (outerProperty, innerProperty) = pair
                    innerProperty.SetValue(innerTask, outerProperty.GetValue(this))
                    pair)

        let executeInnerMethod =
            innerTaskType.GetMethod(nameof(this.ExecuteInner), (BindingFlags.Instance ||| BindingFlags.NonPublic))
        let result = executeInnerMethod.Invoke(innerTask, Array.empty) :?> bool

        let outputPropertiesMap =
            outputPropertiesMap
            |> Seq.map
                (fun (outerProperty, innerProperty) ->
                    outerProperty.SetValue(this, innerProperty.GetValue(innerTask)))

        result
and private CustomAssemblyLoader(loaderTask: ContextAwareTask) =
    inherit AssemblyLoadContext()

    // let loaderTask = loaderTask
    member val public loaderTask = loaderTask
    override this.Load(assemblyName: AssemblyName) : Assembly =
        let assemblyPath = Path.Combine(this.loaderTask.ManagedDllDirectory, assemblyName.Name) + ".dll"
        if File.Exists(assemblyPath)
        then this.LoadFromAssemblyPath(assemblyPath)
        else this.LoadFromAssemblyName(assemblyName)

    override this.LoadUnmanagedDll(unmanagedDllName: string) : IntPtr =
        let unmanagedDllPath =
             Directory.EnumerateFiles(
                this.loaderTask.UnmanagedDllDirectory,
                $"{unmanagedDllName}.*").Concat(
                    Directory.EnumerateFiles(
                        this.loaderTask.UnmanagedDllDirectory,
                        $"lib{unmanagedDllName}.*"))
                .FirstOrDefault()

        if unmanagedDllPath <> null
        then this.LoadUnmanagedDllFromPath(unmanagedDllPath)
        else base.LoadUnmanagedDll(unmanagedDllName)


