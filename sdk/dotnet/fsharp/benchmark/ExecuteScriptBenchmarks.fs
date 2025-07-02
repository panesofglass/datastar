module StarFederation.Datastar.FSharp.Benchmark.ExecuteScriptBenchmarks

open System
open System.Threading
open BenchmarkDotNet.Attributes
open Microsoft.AspNetCore.Http
open StarFederation.Datastar.FSharp
open StarFederation.Datastar.FSharp.Benchmark.TestData

[<MemoryDiagnoser>]
[<SimpleJob>]
type ExecuteScriptBenchmarks() =

    // Use realistic JavaScript generation with consistent scaling  
    let smallScript = JavaScript.generate Sizes.Small
    let mediumScript = JavaScript.generate Sizes.Medium
    let largeScript = JavaScript.generate Sizes.Large
    
    // Script that already has <script> tags to test different code path
    let preWrappedScript = $"<script>{JavaScript.generate Sizes.Small}</script>"

    let defaultOptions = ExecuteScriptOptions.Defaults
    let customOptions = { defaultOptions with EventId = ValueSome "custom-event" }

    [<Benchmark(Baseline = true)>]
    member _.ExecuteScript_Small_Default() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.ExecuteScript(httpContext.Response, smallScript, defaultOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.ExecuteScript_Small_CustomOptions() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.ExecuteScript(httpContext.Response, smallScript, customOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.ExecuteScript_Medium_Default() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.ExecuteScript(httpContext.Response, mediumScript, defaultOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.ExecuteScript_Large_Default() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.ExecuteScript(httpContext.Response, largeScript, defaultOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length

    [<Benchmark>]
    member _.ExecuteScript_PreWrapped_Default() =
        let httpContext = DefaultHttpContext()
        ServerSentEventGenerator.ExecuteScript(httpContext.Response, preWrappedScript, defaultOptions, CancellationToken.None)
            .GetAwaiter().GetResult() |> ignore
        httpContext.Response.Body.Length
