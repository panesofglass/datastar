namespace StarFederation.Datastar.Tests

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open StarFederation.Datastar
open System

[<MemoryDiagnoser>]
type ServerSentEventGeneratorBenchmarks() =
    
    let createDataLine (text: string) =
        use buffer = new SpanBuffer(text.Length)
        buffer.Append(text.AsSpan())
        buffer.AsMemory()
    
    [<Benchmark>]
    member _.SerializeSmallEvent() =
        let event = 
            { EventType = EventType.MergeFragments
              Id = ValueSome "test-1"
              Retry = TimeSpan.FromMilliseconds(1000.0)
              DataLines = [|
                  createDataLine $"{Consts.DatastarDatalineSelector} #test-div"
                  createDataLine $"{Consts.DatastarDatalineMergeMode} {Consts.FragmentMergeMode.toString FragmentMergeMode.Morph}"
                  createDataLine $"{Consts.DatastarDatalineFragments} <div>Test content</div>"
              |] }
        ServerSentEvent.serialize event
        
    [<Benchmark>]
    member _.SerializeMediumEvent() =
        let event = 
            { EventType = EventType.MergeFragments
              Id = ValueSome "test-2"
              Retry = TimeSpan.FromMilliseconds(1000.0)
              DataLines = [|
                  yield createDataLine $"{Consts.DatastarDatalineSelector} #test-div"
                  yield createDataLine $"{Consts.DatastarDatalineMergeMode} {Consts.FragmentMergeMode.toString FragmentMergeMode.Morph}"
                  yield! [1..10] |> List.map (fun i -> 
                      createDataLine $"{Consts.DatastarDatalineFragments} <div>Test content {i}</div>")
              |] }
        ServerSentEvent.serialize event
        
    [<Benchmark>]
    member _.SerializeLargeEvent() =
        let event = 
            { EventType = EventType.MergeFragments
              Id = ValueSome "test-3"
              Retry = TimeSpan.FromMilliseconds(1000.0)
              DataLines = [|
                  yield createDataLine $"{Consts.DatastarDatalineSelector} #test-div"
                  yield createDataLine $"{Consts.DatastarDatalineMergeMode} {Consts.FragmentMergeMode.toString FragmentMergeMode.Morph}"
                  yield! [1..100] |> List.map (fun i -> 
                      createDataLine $"{Consts.DatastarDatalineFragments} <div id='item-{i}'>Test content for item {i} with some additional text to increase size</div>")
              |] }
        ServerSentEvent.serialize event

