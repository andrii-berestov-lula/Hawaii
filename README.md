# Hawaii  [![Nuget](https://img.shields.io/nuget/v/Hawaii.svg?colorB=green)](https://www.nuget.org/packages/Hawaii)

![](logo.png)

A dotnet CLI tool to generate type-safe F# and Fable clients from OpenAPI/Swagger services.

### Features
 - Supports local and external schemas, comaptible with those schemas that can be read by [OpenAPI.NET](https://github.com/microsoft/OpenAPI.NET)
 - Generates clients for F# on dotnet or for Fable in the browser
 - Automatically handles JSON deserialization into schema types
 - Automatically handles mixed responses from end points that return binary _or_ typed JSON responses
 - Generates discriminated union types to describe the possible responses of each endpoint
 - Generates full F# projects, including their required dependencies and targeting `netstandard2.0`

### Install

```
dotnet tool install -g hawaii
```

### Configuration

Create a configuration file called `hawaii.json` with the following shape:
```
{
    "schema": <schema>,
    "project": <project>,
    "output": <output>,
    ["target"]: <"fsharp" | "fable">
    ["synchronous"]: <true | false>,
    ["asyncReturnType"]: <"async" | "task">,
    ["resolveReferences]": <true | false>,
    ["emptyDefinitions"]: <"ignore" | "free-form">,
    ["overrideSchema"]: <JSON schema subset>
}
```
Where
 - `<schema>` is a URL to the OpenApi/Swagger location, whether that is an external URL or a relative file path
 - `<project>` is the name of the project that will get generated
 - `<output>` is a relative path to the output directory where the project will be generated. (Note: this directory is deleted and re-generated when you run `hawaii`)
 - `<synchronous>` is an optional flag that determines whether hawaii should generate client methods that run http requests synchronously. This is useful when used inside console applications. (set to false by default)
 - `<target>` specifies whether hawaii should generate a client for F# on dotnet  (default) or a Fable client
 - `<asyncReturnType>` is an option to determine whether hawaii should generate client methods that return `Async<'T>` when set to "async" (default) or `Task<'T>` when set to "task" (this option is irrelevant when the `synchronous` option is set to `true`)
 - `<resolveReferences>` determines whether hawaii will attempt to resolve external references via schema pre-processing. This is set to `false` by default but sometimes an OpenApi schema is scattered into multiple schemas across a repository and this might help with the resolution.
 - `<emptyDefintions>` determines what hawaii should do when encountering a global type definition without schema information. When set to "ignore" (default) hawaii will generate the global type. However, sometimes these global types are still referenced from other types or definitions, in which case the setting this option to "free-form" will generate a type abbreviation for the empty schema equal to a free form object (`JToken` when targetting F# or `obj` when targetting Fable)
 - `<overrideSchema>` Allows you to override the resolved schema either to add more information (such as a missing operation ID) or _correct_ the types when you know better (see below)

### Example
Here is an example configuration for the pet store API:
```json
{
    "schema": "https://petstore3.swagger.io/api/v3/openapi.json",
    "output": "./output",
    "project": "PetStore",
    "synchronous": true
}
```
After you have the configuration ready, run `hawaii` in the directory where you have the `hawaii.json` file:
```
hawaii
```
You can also tell hawaii where to find the configuration file if it wasn't name `hawaii.json`, for example
```
hawaii --config ./petstore-hawaii.json
```
### Using the generated project
Once hawaii has finished running, you find a full generated F# project inside of the `<output>` directory. This project can be referenced from your application so you can start using it.

You can reference the project like this from your app like this:
```xml
<ItemGroup>
  <ProjectReference Include="..\path\to\output\PetStore.fsproj" />
</ItemGroup>
```
Then from your code:
```fs
open System
open System.Net.Http
open PetStore
open PetStore.Types

let petStoreUri = Uri "https://petstore3.swagger.io/api/v3"
let httpClient = new HttpClient(BaseAddress=petStoreUri)
let petStore = PetStoreClient(httpClient)

let availablePets() =
    let status = PetStatus.Available.Format()
    match petStore.findPetsByStatus(status) with
    | FindPetsByStatus.OK pets -> for pet in pets do printfn $"{pet.name}"
    | FindPetsByStatus.BadRequest -> printfn "Bad request"

availablePets()

// inventory : Map<string, int>
let (GetInventory.OK(inventory)) = petStore.getInventory()

for (status, quantity) in Map.toList inventory do
    printfn $"There are {quantity} pet(s) {status}"
```
Notice that you have to provide your own `HttpClient` to the `PetStoreClient` and setting the `BaseAddress` to the base path of the service.

### Sample Applications

- [Minimal PetStore](https://github.com/Zaid-Ajaj/hawaii-samples-petstore) - A simple console application showing how to use the PetStore API client generated by Hawaii
- [Feliz PetStore](https://github.com/Zaid-Ajaj/hawaii-samples-feliz-petstore) - A [Feliz](https://github.com/Zaid-Ajaj/Feliz) application that shows how to use the PetStore API client generated by Hawaii when targeting Fable

### Version
You can ask hawaii which version it is currently on:
```
hawaii --version
```

### No Logo
If you don't want the logo to show up in your CI or local machine, add `--no-logo` as the last parameter
```
hawaii --no-logo
hawaii --config ./hawaii.json --no-logo
```

### Advanced - Overriding The Schema
OpenAPI schemas can be very loose and not always typed. Sometimes they will be missing operation IDs on certain paths. Although Hawaii will attempt to derive valid operation IDs from the path, name collisions can sometimes happen.
Hawaii provides the `overrideSchema` option to allow you to "fix" the source schema or add more information when its missing.

Here is an example for how you can override operation IDs for certain paths
```json
{
  "overrideSchema": {
    "paths": {
      "/consumer/v1/services/{id}/allocations": {
        "get": {
          "operationId": "getAllocationsForCustomerByServiceId"
        }
      },
      "/consumer/v1/services/allocations/{id}": {
        "get": {
            "operationId": "getAllocationIdFromCustomerServices"
        }
      }
    }
  }
}
```
The `overrideSchema` property basically takes a subset of another schema and _merges_ it with the source schema.

You can go a step further by overriding the return types of certain responses. The following example shows how you can get the raw text output from the default response of a path instead of getting a typed response:
```json
{
  "overrideSchema": {
    "paths": {
      "/bin/querybuilder.json": {
        "get": {
          "responses": {
            "default": {
              "content": {
                "application/json": {
                  "schema": {
                    "type": "string"
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}
```

### Limitations
These are the very early days of Hawaii as a tool to generate F# clients and there are some known limitations and rough edges that I will be working on:
 - `anyOf`/`oneOf` not supported


> You can watch the live coding sessions as a playlist published on [YouTube here](https://www.youtube.com/watch?v=8dgjD6vG7yw&list=PLBzGkJMamtz0KCkK7OFnuXyXP7yUtnt9o)

### Running integration tests
```bash
cd ./build
# run hawaii against 10 schemas
dotnet run -- integration
# run hawaii agains the first {n} schemas out of ~2000 and see the progress
dotnet run -- rate {n}
```