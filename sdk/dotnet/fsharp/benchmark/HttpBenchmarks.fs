module StarFederation.Datastar.FSharp.Benchmark.HttpBenchmarks

open System
open System.Threading
open System.Threading.Tasks
open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open StarFederation.Datastar.FSharp
open StarFederation.Datastar.FSharp.Benchmark.TestData

[<MemoryDiagnoser>]
[<SimpleJob>]
type HttpBenchmarks() =

    // Test data using realistic content with consistent scaling
    let smallElement = Html.generate Sizes.Small
    let mediumElement = Html.generate Sizes.Medium
    let largeElement = Html.generate Sizes.Large
    
    let smallSignals = Signals.generate Sizes.Small
    let mediumSignals = Signals.generate Sizes.Medium
    let largeSignals = Signals.generate Sizes.Large
    
    let smallScript = JavaScript.generate Sizes.Small
    let mediumScript = JavaScript.generate Sizes.Medium
    let largeScript = JavaScript.generate Sizes.Large

    [<Benchmark(Baseline = true)>]
    member _.PatchElements_Small() =
        let httpContext = DefaultHttpContext()
        
        ServerSentEventGenerator.PatchElements(httpContext.Response, smallElement, PatchElementsOptions.Defaults, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.PatchElements_Medium() =
        let httpContext = DefaultHttpContext()
        
        ServerSentEventGenerator.PatchElements(httpContext.Response, mediumElement, PatchElementsOptions.Defaults, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.PatchElements_Large() =
        let httpContext = DefaultHttpContext()
        
        ServerSentEventGenerator.PatchElements(httpContext.Response, largeElement, PatchElementsOptions.Defaults, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.PatchSignals_Small() =
        let httpContext = DefaultHttpContext()
        
        ServerSentEventGenerator.PatchSignals(httpContext.Response, smallSignals, PatchSignalsOptions.Defaults, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.PatchSignals_Medium() =
        let httpContext = DefaultHttpContext()
        
        ServerSentEventGenerator.PatchSignals(httpContext.Response, mediumSignals, PatchSignalsOptions.Defaults, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.ExecuteScript_Small() =
        let httpContext = DefaultHttpContext()
        
        ServerSentEventGenerator.ExecuteScript(httpContext.Response, smallScript, ExecuteScriptOptions.Defaults, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        
        httpContext.Response.Body.Length
