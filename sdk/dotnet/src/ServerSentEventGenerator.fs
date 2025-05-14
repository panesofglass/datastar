namespace StarFederation.Datastar

open System
open System.Runtime.CompilerServices

/// <summary>
/// Converts requests into SSEs, serializes, and sends to sseHandler handlers
/// </summary>
[<AbstractClass; Sealed>]
type ServerSentEventGenerator =
    static member inline private send (sseHandler: ISendServerEvent) sse = sseHandler.SendServerEvent sse

    static member Send(sse, sseHandler: ISendServerEvent) =
        ServerSentEventGenerator.send sseHandler sse

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member private CreateDataLine(buffer: SpanBuffer, prefix: ReadOnlySpan<char>, content: ReadOnlySpan<char>) =
        buffer.Reset()
        buffer.Append(prefix) |> ignore
        buffer.Append(content) |> ignore
        buffer.AsMemory()

    static member MergeFragments(sseHandler, fragments: string, ?options: MergeFragmentsOptions) =
        ServerSentEventGenerator.MergeFragments(
            sseHandler,
            fragments.AsSpan(),
            defaultArg options MergeFragmentsOptions.defaults
        )

    static member MergeFragments(sseHandler, fragments: ReadOnlySpan<char>, ?options: MergeFragmentsOptions) =
        let options = defaultArg options MergeFragmentsOptions.defaults
        let dataLines = ResizeArray<ReadOnlyMemory<char>>()

        use buffer = new SpanBuffer(4096) // Single reusable buffer

        // Add selector if present
        if options.Selector |> ValueOption.isSome then
            dataLines.Add(
                ServerSentEventGenerator.CreateDataLine(
                    buffer,
                    $"{Consts.DatastarDatalineSelector} ".AsSpan(),
                    (options.Selector |> ValueOption.get |> Selector.value).AsSpan()
                )
            )

        // Add merge mode if not default
        if options.MergeMode <> Consts.DefaultFragmentMergeMode then
            dataLines.Add(
                ServerSentEventGenerator.CreateDataLine(
                    buffer,
                    $"{Consts.DatastarDatalineMergeMode} ".AsSpan(),
                    (options.MergeMode |> Consts.FragmentMergeMode.toString).AsSpan()
                )
            )

        // Add view transition if not default
        if options.UseViewTransition <> Consts.DefaultFragmentsUseViewTransitions then
            dataLines.Add(
                ServerSentEventGenerator.CreateDataLine(
                    buffer,
                    $"{Consts.DatastarDatalineUseViewTransition} ".AsSpan(),
                    options.UseViewTransition.ToString().AsSpan()
                )
            )

        // Add fragments
        let lines = fragments.ToString().Split('\n')

        for line in lines do
            dataLines.Add(
                ServerSentEventGenerator.CreateDataLine(
                    buffer,
                    $"{Consts.DatastarDatalineFragments} ".AsSpan(),
                    line.AsSpan()
                )
            )

        { EventType = MergeFragments
          Id = options.EventId
          Retry = options.Retry
          DataLines = dataLines.ToArray() }
        |> ServerSentEventGenerator.send sseHandler

    static member RemoveFragments(sseHandler, selector: Selector, ?options: RemoveFragmentsOptions) =
        let options = defaultArg options RemoveFragmentsOptions.defaults
        let dataLines = ResizeArray<ReadOnlyMemory<char>>()

        use buffer = new SpanBuffer(4096) // Single reusable buffer

        // Add selector
        dataLines.Add(
            ServerSentEventGenerator.CreateDataLine(
                buffer,
                $"{Consts.DatastarDatalineSelector} ".AsSpan(),
                (selector |> Selector.value).AsSpan()
            )
        )

        // Add view transition if not default
        if options.UseViewTransition <> Consts.DefaultFragmentsUseViewTransitions then
            dataLines.Add(
                ServerSentEventGenerator.CreateDataLine(
                    buffer,
                    $"{Consts.DatastarDatalineUseViewTransition} ".AsSpan(),
                    options.UseViewTransition.ToString().AsSpan()
                )
            )

        { EventType = RemoveFragments
          Id = options.EventId
          Retry = options.Retry
          DataLines = dataLines.ToArray() }
        |> ServerSentEventGenerator.send sseHandler

    static member MergeSignals(sseHandler, mergeSignals: Signals, ?options: MergeSignalsOptions) =
        ServerSentEventGenerator.MergeSignals(
            sseHandler,
            (Signals.value mergeSignals).AsSpan(),
            defaultArg options MergeSignalsOptions.defaults
        )

    static member MergeSignals(sseHandler, signals: ReadOnlySpan<char>, ?options: MergeSignalsOptions) =
        let options = defaultArg options MergeSignalsOptions.defaults
        let dataLines = ResizeArray<ReadOnlyMemory<char>>()

        use buffer = new SpanBuffer(4096) // Single reusable buffer

        // Add only if missing flag if not default
        if options.OnlyIfMissing <> Consts.DefaultMergeSignalsOnlyIfMissing then
            dataLines.Add(
                ServerSentEventGenerator.CreateDataLine(
                    buffer,
                    $"{Consts.DatastarDatalineOnlyIfMissing} ".AsSpan(),
                    options.OnlyIfMissing.ToString().AsSpan()
                )
            )

        // Add signals
        let lines = signals.ToString().Split('\n')

        for line in lines do
            dataLines.Add(
                ServerSentEventGenerator.CreateDataLine(
                    buffer,
                    $"{Consts.DatastarDatalineSignals} ".AsSpan(),
                    line.AsSpan()
                )
            )

        { EventType = MergeSignals
          Id = options.EventId
          Retry = options.Retry
          DataLines = dataLines.ToArray() }
        |> ServerSentEventGenerator.send sseHandler

    static member RemoveSignals(sseHandler, paths: seq<SignalPath>, ?options: EventOptions) =
        let options = defaultArg options EventOptions.defaults
        let paths' = paths |> Seq.map SignalPath.value |> String.concat " "

        use buffer = new SpanBuffer(4096) // Single reusable buffer

        { EventType = RemoveSignals
          Id = options.EventId
          Retry = options.Retry
          DataLines =
            [| ServerSentEventGenerator.CreateDataLine(
                   buffer,
                   $"{Consts.DatastarDatalinePaths} ".AsSpan(),
                   paths'.AsSpan()
               ) |] }
        |> ServerSentEventGenerator.send sseHandler

    static member ExecuteScript(sseHandler, script: string, ?options: ExecuteScriptOptions) =
        ServerSentEventGenerator.ExecuteScript(
            sseHandler,
            script.AsSpan(),
            defaultArg options ExecuteScriptOptions.defaults
        )

    static member ExecuteScript(sseHandler, script: ReadOnlySpan<char>, ?options: ExecuteScriptOptions) =
        let options = defaultArg options ExecuteScriptOptions.defaults
        let dataLines = ResizeArray<ReadOnlyMemory<char>>()

        use buffer = new SpanBuffer(4096) // Single reusable buffer

        // Add auto remove if not default
        if options.AutoRemove <> Consts.DefaultExecuteScriptAutoRemove then
            dataLines.Add(
                ServerSentEventGenerator.CreateDataLine(
                    buffer,
                    $"{Consts.DatastarDatalineAutoRemove} ".AsSpan(),
                    options.AutoRemove.ToString().AsSpan()
                )
            )

        // Add attributes if not default
        if
            not
            <| Seq.forall2 (=) options.Attributes [| Consts.DefaultExecuteScriptAttributes |]
        then
            for attr in options.Attributes do
                dataLines.Add(
                    ServerSentEventGenerator.CreateDataLine(
                        buffer,
                        $"{Consts.DefaultExecuteScriptAttributes} ".AsSpan(),
                        attr.AsSpan()
                    )
                )

        // Add script lines
        let lines = script.ToString().Split('\n')

        for line in lines do
            dataLines.Add(
                ServerSentEventGenerator.CreateDataLine(
                    buffer,
                    $"{Consts.DatastarDatalineScript} ".AsSpan(),
                    line.AsSpan()
                )
            )

        { EventType = ExecuteScript
          Id = options.EventId
          Retry = options.Retry
          DataLines = dataLines.ToArray() }
        |> ServerSentEventGenerator.send sseHandler
