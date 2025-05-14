namespace StarFederation.Datastar

open System
open System.Buffers
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions
open System.Threading.Tasks
open StarFederation.Datastar.Utility

/// <summary>
/// Allocation-free buffer management for text operations
/// </summary>
[<Struct>]
type SpanBuffer =
    val mutable private Buffer: char[]
    val mutable private Position: int
    val mutable private IsPooled: bool

    new(initialSize) =
        let useStackAlloc = initialSize <= 256

        { Buffer =
            if useStackAlloc then
                Array.zeroCreate initialSize
            else
                ArrayPool<char>.Shared.Rent(initialSize)
          Position = 0
          IsPooled = not useStackAlloc }

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Reset() = this.Position <- 0

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Append(value: ReadOnlySpan<char>) =
        if this.Position + value.Length > this.Buffer.Length then
            let newSize = Math.Max(this.Buffer.Length * 2, this.Position + value.Length)
            let newBuffer = ArrayPool<char>.Shared.Rent(newSize)
            Array.Copy(this.Buffer, newBuffer, this.Position)

            if this.IsPooled then
                ArrayPool<char>.Shared.Return(this.Buffer)

            this.Buffer <- newBuffer
            this.IsPooled <- true

        value.CopyTo(this.Buffer.AsSpan(this.Position))
        this.Position <- this.Position + value.Length

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.AppendLine() = this.Append("\n".AsSpan())

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.AsMemory() =
        ReadOnlyMemory<char>(this.Buffer, 0, this.Position)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.AsSpan() =
        ReadOnlySpan<char>(this.Buffer, 0, this.Position)

    interface IDisposable with
        member this.Dispose() =
            if this.IsPooled then
                ArrayPool<char>.Shared.Return(this.Buffer)

type ServerSentEvent =
    { EventType: EventType
      Id: string voption
      Retry: TimeSpan
      DataLines: ReadOnlyMemory<char>[] }

/// <summary>
/// Signals read to and from Datastar on the front end
/// </summary>
type Signals = string

/// <summary>
/// A dotted path into Signals to access a key/value pair
/// </summary>
type SignalPath = string

/// <summary>
/// An HTML selector name
/// </summary>
type Selector = string

type MergeFragmentsOptions =
    { Selector: Selector voption
      MergeMode: FragmentMergeMode
      UseViewTransition: bool
      EventId: string voption
      Retry: TimeSpan }

type MergeSignalsOptions =
    { OnlyIfMissing: bool
      EventId: string voption
      Retry: TimeSpan }

type RemoveFragmentsOptions =
    { UseViewTransition: bool
      EventId: string voption
      Retry: TimeSpan }

type ExecuteScriptOptions =
    { AutoRemove: bool
      Attributes: string[]
      EventId: string voption
      Retry: TimeSpan }

type EventOptions =
    { EventId: string voption
      Retry: TimeSpan }

/// <summary>
/// Read the signals from the request
/// </summary>
type IReadSignals =
    abstract ReadSignals: unit -> Task<Signals voption>
    abstract ReadSignals<'T> : unit -> Task<'T voption>
    abstract ReadSignals<'T> : JsonSerializerOptions -> Task<'T voption>

/// <summary>
/// Can send SSEs to the client
/// </summary>
type ISendServerEvent =
    abstract SendServerEvent: ServerSentEvent -> Task

module ServerSentEvent =
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let serializeToSpan (sse: ServerSentEvent) (buffer: SpanBuffer) =
        // Event type line
        buffer.Append("event: ".AsSpan())
        buffer.Append((sse.EventType |> Consts.EventType.toString).AsSpan())
        buffer.AppendLine()

        // ID line if present
        if sse.Id |> ValueOption.isSome then
            buffer.Append("id: ".AsSpan())
            buffer.Append((sse.Id |> ValueOption.get).AsSpan())
            buffer.AppendLine()

        // Retry line if not default
        if sse.Retry <> Consts.DefaultSseRetryDuration then
            buffer.Append("retry: ".AsSpan())
            buffer.Append(sse.Retry.TotalMilliseconds.ToString().AsSpan())
            buffer.AppendLine()

        // Data lines
        for dataLine in sse.DataLines do
            buffer.Append("data: ".AsSpan())
            buffer.Append(dataLine.Span)
            buffer.AppendLine()

        // Final empty lines
        buffer.AppendLine()
        buffer.AppendLine()

    let serialize sse =
        // Use a pooled buffer starting with 4KB
        use buffer = new SpanBuffer(4096)
        serializeToSpan sse buffer

        // Convert to string (single allocation at the end)
        buffer.AsSpan().ToString()

module Signals =
    let value (signals: Signals) : string = signals.ToString()
    let create (signalsString: string) = Signals signalsString
    let createFromSpan (span: ReadOnlySpan<char>) = Signals(span.ToString())

    let tryCreate (signalsString: string) =
        try
            let _ = JsonObject.Parse(signalsString)
            ValueSome(Signals signalsString)
        with _ ->
            ValueNone

    let tryCreateFromSpan (span: ReadOnlySpan<char>) = tryCreate (span.ToString())
    let empty = Signals "{ }"

module SignalPath =
    let value (signalPath: SignalPath) = signalPath.ToString()
    let kebabValue signals = signals |> value |> String.toKebab

    let isValidKey (signalPathKey: string) =
        signalPathKey |> String.IsPopulated
        && signalPathKey.ToCharArray()
           |> Seq.forall (fun chr -> Char.IsLetter chr || Char.IsNumber chr || chr = '_')

    let isValidKeySpan (signalPathKey: ReadOnlySpan<char>) =
        signalPathKey.Length > 0
        && signalPathKey.ToArray()
           |> Seq.forall (fun chr -> Char.IsLetter chr || Char.IsNumber chr || chr = '_')

    let isValid (signalPathString: string) =
        signalPathString.Split('.') |> Array.forall isValidKey

    let isValidSpan (signalPathSpan: ReadOnlySpan<char>) =
        // This creates an array but is still more efficient than the string version
        let parts = signalPathSpan.ToString().Split('.')
        parts |> Array.forall isValidKey

    let tryCreate (signalPathString: string) =
        if isValid signalPathString then
            ValueSome(SignalPath signalPathString)
        else
            ValueNone

    let tryCreateFromSpan (signalPathSpan: ReadOnlySpan<char>) =
        if isValidSpan signalPathSpan then
            ValueSome(SignalPath(signalPathSpan.ToString()))
        else
            ValueNone

    let sp (signalPathString: string) =
        if isValid signalPathString then
            SignalPath signalPathString
        else
            failwith $"{signalPathString} is not a valid signal path"

    let create = sp

    let keys signalPath =
        signalPath |> value |> String.split [ "." ]

    let createJsonNodePathToValue<'T> signalPath (signalValue: 'T) =
        signalPath
        |> keys
        |> Seq.rev
        |> Seq.fold
            (fun json key -> JsonObject([ KeyValuePair<string, JsonNode>(key, json) ]) :> JsonNode)
            (JsonValue.Create(signalValue) :> JsonNode)


module Selector =
    let regex =
        Regex(
            @"[#.][-_]?[_a-zA-Z]+(?:\w|\\.)*|(?<=\s+|^)(?:\w+|\*)|\[[^\s""'=<>`]+?(?<![~|^$*])([~|^$*]?=(?:['""].*['""]|[^\s""'=<>`]+))?\]|:[\w-]+(?:\(.*\))?",
            RegexOptions.Compiled
        )

    let value (selector: Selector) = selector.ToString()
    let isValid (selectorString: string) = regex.IsMatch selectorString

    let isValidSpan (selectorSpan: ReadOnlySpan<char>) =
        // Unfortunately need to allocate here for regex
        regex.IsMatch(selectorSpan.ToString())

    let tryCreate (selectorString: string) =
        if isValid selectorString then
            ValueSome(Selector selectorString)
        else
            ValueNone

    let tryCreateFromSpan (selectorSpan: ReadOnlySpan<char>) =
        if isValidSpan selectorSpan then
            ValueSome(Selector(selectorSpan.ToString()))
        else
            ValueNone

    let sel (selectorString: string) =
        if isValid selectorString then
            Selector selectorString
        else
            failwith $"{selectorString} is not a valid selector"

    let create = sel

module MergeFragmentsOptions =
    let defaults =
        { Selector = ValueNone
          MergeMode = Consts.DefaultFragmentMergeMode
          UseViewTransition = Consts.DefaultFragmentsUseViewTransitions
          EventId = ValueNone
          Retry = Consts.DefaultSseRetryDuration }

module MergeSignalsOptions =
    let defaults =
        { OnlyIfMissing = Consts.DefaultMergeSignalsOnlyIfMissing
          EventId = ValueNone
          Retry = Consts.DefaultSseRetryDuration }

module RemoveFragmentsOptions =
    let defaults =
        { UseViewTransition = Consts.DefaultFragmentsUseViewTransitions
          EventId = ValueNone
          Retry = Consts.DefaultSseRetryDuration }

module ExecuteScriptOptions =
    let defaults =
        { AutoRemove = Consts.DefaultExecuteScriptAutoRemove
          Attributes = [| Consts.DefaultExecuteScriptAttributes |]
          EventId = ValueNone
          Retry = Consts.DefaultSseRetryDuration }

module EventOptions =
    let defaults =
        { EventId = ValueNone
          Retry = Consts.DefaultSseRetryDuration }
