module StarFederation.Datastar.FSharp.Benchmark.PatchSignalsBenchmarks

open System
open System.Threading
open System.Text.Json
open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open StarFederation.Datastar.FSharp
open StarFederation.Datastar.FSharp.Benchmark.TestData

[<MemoryDiagnoser>]
[<SimpleJob>]
type PatchSignalsBenchmarks() =

    // Use consistent JSON generation for all sizes
    let smallSignal = Signals.generate Sizes.Small
    let mediumSignal = Signals.generate Sizes.Medium  
    let largeSignal = Signals.generate Sizes.Large

    let defaultOptions = PatchSignalsOptions.Defaults
    let onlyIfMissingOptions = { defaultOptions with OnlyIfMissing = true }

    [<Benchmark(Baseline = true)>]
    member _.PatchSignals_Small_Default() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchSignals(httpContext.Response, smallSignal, defaultOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.PatchSignals_Small_OnlyIfMissing() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchSignals(httpContext.Response, smallSignal, onlyIfMissingOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.PatchSignals_Medium_Default() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchSignals(httpContext.Response, mediumSignal, defaultOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.PatchSignals_Medium_OnlyIfMissing() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchSignals(httpContext.Response, mediumSignal, onlyIfMissingOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.PatchSignals_Large_Default() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchSignals(httpContext.Response, largeSignal, defaultOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.PatchSignals_Large_OnlyIfMissing() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.PatchSignals(httpContext.Response, largeSignal, onlyIfMissingOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length
