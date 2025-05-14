module Program

open BenchmarkDotNet.Running
open StarFederation.Datastar.Tests

[<EntryPoint>]
let main argv =
    let summary = BenchmarkRunner.Run<ServerSentEventGeneratorBenchmarks>()
    0

