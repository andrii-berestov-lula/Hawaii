namespace Hawaii.Tasks

open System
open System.IO
open System.Text.Json
open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open Program

type public GenerateOpenAPIClient() = 
    inherit Task()

    [<Required>]
    member val public Platform : string = String.Empty with get, set
    
    [<Required>]
    member val public Configuration: string = String.Empty with get,set 

    member val public EmitMetadata = false with get, set

    member val public OutputPath : string = String.Empty with get, set

    member val public Project = "OpenAPIClient" with get, set

    member val public Schema = "" with get, set

    member val public AsyncType = AsyncReturnType.Async with get, set

    member val public Target = "fable" with get, set

    [<Output>]
    member val public GeneratedFiles = Array.empty<string> with get, set

    static member private TryParseTarget =
        function
        | "fable" -> Some Target.Fable
        | "fsharp" -> Some Target.FSharp
        | _ -> None
        
    override this.Execute() =
        if String.IsNullOrEmpty this.Schema then raise (Exception "Schema must not be null or empty")
        if String.IsNullOrEmpty this.Target then raise (Exception "Target must be fable or fsharp")
        if String.IsNullOrEmpty this.Project then raise (Exception "Project must not be null or empty")
        let configName = "config.json"

        let preparedConfigFile = JsonSerializer.Serialize(
                {
                schema =  this.Schema
                asyncReturnType = this.AsyncType
                output = if String.IsNullOrEmpty(this.OutputPath) then
                             Path.Combine("obj", this.Configuration, this.Platform, "Hawaii")
                         else this.OutputPath
                target = (GenerateOpenAPIClient.TryParseTarget (this.Target.ToLower())) |> Option.defaultValue Target.Fable
                project = this.Project
                synchronous = true
                resolveReferences = true
                overrideSchema = None
                emptyDefinitions = EmptyDefinitionResolution.GenerateFreeForm
                filterTags = []
                odataSchema = true })
        File.WriteAllText(configName, preparedConfigFile)
        
        use buffer = new MemoryStream()
        let writer = new StreamWriter(buffer)
        Console.SetOut(writer)
        
        let executionCode = runConfig configName
        
        executionCode = 0