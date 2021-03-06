// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.DependencyManager.UnitTests

open System
open System.IO
open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.Scripting
open FSharp.Compiler.SourceCodeServices

open NUnit.Framework

[<TestFixture>]
type DependencyManagerInteractiveTests() =

    let getValue ((value: Result<FsiValue option, exn>), (errors: FSharpErrorInfo[])) =
        if errors.Length > 0 then
            failwith <| sprintf "Evaluation returned %d errors:\r\n\t%s" errors.Length (String.Join("\r\n\t", errors))
        match value with
        | Ok(value) -> value
        | Error ex -> raise ex

    let ignoreValue = getValue >> ignore

    let scriptHost () = new FSharpScript(additionalArgs=[|"/langversion:preview"|])

    [<Test>]
    member __.``SmokeTest - #r nuget``() =
        let text = """
#r @"nuget:Newtonsoft.Json, Version=9.0.1"
0"""
        use script = scriptHost()
        let opt = script.Eval(text) |> getValue
        let value = opt.Value
        Assert.AreEqual(typeof<int>, value.ReflectionType)
        Assert.AreEqual(0, value.ReflectionValue :?> int)

    [<Test>]
    member __.``SmokeTest - #r nuget package not found``() =
        let text = """
#r @"nuget:System.Collections.Immutable.DoesNotExist, version=1.5.0"
0"""
        use script = scriptHost()
        let opt = script.Eval(text) |> getValue
        let value = opt.Value
        Assert.AreEqual(typeof<int>, value.ReflectionType)
        Assert.AreEqual(0, value.ReflectionValue :?> int)

    [<Test>]
    member __.``Dependency add events successful``() =
        let referenceText = "Newtonsoft.Json, Version=9.0.1"
        let text = referenceText |> sprintf """
#r @"nuget:%s"
0"""
        use script = scriptHost()
        let mutable dependencyAddingEventCount = 0
        let mutable dependencyAddedEventCount = 0
        let mutable foundDependencyAdding = false
        let mutable foundDependencyAdded = false
        let mutable packageRootsCount = 0
        let mutable generatedScriptsCount = 0
        Event.add (fun (dep: string * string) ->
            let key, dependency = dep
            dependencyAddingEventCount <- dependencyAddingEventCount + 1
            foundDependencyAdding <- foundDependencyAdding || (key = "nuget" && dependency = referenceText))
            script.DependencyAdding
        Event.add (fun (dep: string * string * string list * string list) ->
            let key, dependency, _generatedScripts, _packageRoots = dep
            generatedScriptsCount <- _generatedScripts.Length
            packageRootsCount <- _packageRoots.Length
            dependencyAddedEventCount <- dependencyAddedEventCount + 1
            foundDependencyAdded <- foundDependencyAdded || (key = "nuget" && dependency = referenceText))
            script.DependencyAdded
        script.Eval(text) |> ignoreValue
        Assert.AreEqual(1, dependencyAddingEventCount)
        Assert.AreEqual(1, dependencyAddedEventCount)
        Assert.AreEqual(1, dependencyAddingEventCount)
        Assert.AreEqual(1, dependencyAddedEventCount)
        Assert.AreEqual(1, generatedScriptsCount)
        Assert.AreEqual(1, dependencyAddedEventCount)
        Assert.AreEqual(true, foundDependencyAdding)
        Assert.AreEqual(true, foundDependencyAdded)

    [<Test>]
    member __.``Dependency add events failed``() =
        let referenceText = "System.Collections.Immutable.DoesNotExist, version=1.5.0"
        let text = referenceText |> sprintf """
#r @"nuget:%s"
0"""
        use script = scriptHost()
        let mutable dependencyAddingEventCount = 0
        let mutable dependencyFailedEventCount = 0
        let mutable foundDependencyAdding = false
        let mutable foundDependencyFailed = false
        Event.add (fun (dep: string * string) ->
            let key, dependency = dep
            dependencyAddingEventCount <- dependencyAddingEventCount + 1
            foundDependencyAdding <- foundDependencyAdding || (key = "nuget" && dependency = referenceText))
            script.DependencyAdding
        Event.add (fun (dep: string * string) ->
            let key, dependency = dep
            dependencyFailedEventCount <- dependencyFailedEventCount + 1
            foundDependencyFailed <- foundDependencyFailed || (key = "nuget" && dependency = referenceText))
            script.DependencyFailed
        script.Eval(text) |> ignoreValue
        Assert.AreEqual(1, dependencyAddingEventCount)
        Assert.AreEqual(1, dependencyFailedEventCount)
        Assert.AreEqual(true, foundDependencyAdding)
        Assert.AreEqual(true, foundDependencyFailed)

    [<Test>]
    member __.``Dependency add events aren't repeated``() =
        use script = scriptHost()
        let mutable dependencyAddingEventCount = 0
        Event.add (fun _ -> dependencyAddingEventCount <- dependencyAddingEventCount + 1) script.DependencyAdding
        script.Eval("#r \"nuget:NUnit.ConsoleRunner, Version=3.10.0\"") |> ignoreValue
        script.Eval("#r \"nuget:Newtonsoft.Json, Version=9.0.1\"\n0") |> ignoreValue
        Assert.AreEqual(2, dependencyAddingEventCount)
