module StarFederation.Datastar.FSharp.Benchmark.PatchElementsBenchmarks

open System
open System.Threading
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Diagnosers
open Microsoft.AspNetCore.Http
open StarFederation.Datastar.FSharp
open StarFederation.Datastar.FSharp.Benchmark.TestData

[<MemoryDiagnoser>]
[<SimpleJob>]
type PatchElementsBenchmarks() =
    
    // Test data using realistic HTML with consistent scaling
    let smallElement = Html.generate Sizes.Small
    let mediumElement = Html.generate Sizes.Medium
    let largeElement = Html.generate Sizes.Large
    
    let defaultOptions = PatchElementsOptions.Defaults
    let selectorOptions = { defaultOptions with Selector = ValueSome (Selector.create "#target") }
    let fullOptions = { selectorOptions with PatchMode = ElementPatchMode.Inner; UseViewTransition = true }
    
    [<Benchmark(Baseline = true)>]
    member _.PatchElements_Small_Default() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchElements(httpContext.Response, smallElement, defaultOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length
    
    [<Benchmark>]
    member _.PatchElements_Small_WithSelector() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchElements(httpContext.Response, smallElement, selectorOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length
    
    [<Benchmark>]
    member _.PatchElements_Small_FullOptions() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchElements(httpContext.Response, smallElement, fullOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length
    
    [<Benchmark>]
    member _.PatchElements_Medium_Default() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchElements(httpContext.Response, mediumElement, defaultOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length
    
    [<Benchmark>]
    member _.PatchElements_Medium_WithSelector() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchElements(httpContext.Response, mediumElement, selectorOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length
    
    [<Benchmark>]
    member _.PatchElements_Large_Default() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchElements(httpContext.Response, largeElement, defaultOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length
    
    [<Benchmark>]
    member _.PatchElements_Large_WithSelector() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchElements(httpContext.Response, largeElement, selectorOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length
