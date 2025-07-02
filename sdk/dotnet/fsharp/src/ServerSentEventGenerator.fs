namespace StarFederation.Datastar.FSharp

open System
open System.Buffers
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers
open StarFederation.Datastar.FSharp.Utility

module JsonSerializerOptions =
    let SignalsDefault =
        let options = JsonSerializerOptions()
        options.PropertyNameCaseInsensitive <- true
        options

[<Sealed>]
type ServerSentEventGenerator(httpContextAccessor:IHttpContextAccessor) =
    let httpRequest = httpContextAccessor.HttpContext.Request
    let httpResponse = httpContextAccessor.HttpContext.Response
    let mutable _startResponseTask : Task = null
    let _startResponseLock = obj()

    static member StartServerEventStream (httpResponse:HttpResponse, ?additionalHeaders:seq<KeyValuePair<string, StringValues>>, ?cancellationToken:CancellationToken) =
        let additionalHeaders = defaultArg additionalHeaders (Seq.empty<KeyValuePair<string, StringValues>>)
        let cancellationToken = defaultArg cancellationToken httpResponse.HttpContext.RequestAborted

        let task = backgroundTask {
            httpResponse.Headers.ContentType <- "text/event-stream"
            if (httpResponse.HttpContext.Request.Protocol = HttpProtocol.Http11) then
                httpResponse.Headers.Connection <- "keep-alive"
            for KeyValue(name, content) in additionalHeaders do
                match httpResponse.Headers.TryGetValue(name) with
                | true, existing ->
                    // httpResponse.Headers[name] <- StringValues.Concat(existing, content)
                    ()
                | false, _ ->
                    httpResponse.Headers.Add(name, content)
            do! httpResponse.StartAsync(cancellationToken)
            return! httpResponse.BodyWriter.FlushAsync(cancellationToken)
        }
        task :> Task

    static member PatchElements(httpResponse:HttpResponse, elements, ?options:PatchElementsOptions, ?cancellationToken:CancellationToken) =
        let options = defaultArg options PatchElementsOptions.Defaults
        let cancellationToken = defaultArg cancellationToken httpResponse.HttpContext.RequestAborted

        let writer = httpResponse.BodyWriter
        writer |> ServerSentEvent.sendEventType PatchElements
        writer |> ServerSentEvent.sendId options.EventId
        writer |> ServerSentEvent.sendRetry options.Retry

        match options.Selector with
        | ValueSome selector ->
            writer |> ServerSentEvent.sendDataLine $"{Consts.DatastarDatalineSelector} {Selector.value selector}"
        | _ -> ()

        if options.PatchMode <> Consts.DefaultElementPatchMode then
            writer |> ServerSentEvent.sendDataLine $"{Consts.DatastarDatalineMode} {Consts.ElementPatchMode.toString options.PatchMode}"

        if options.UseViewTransition <> Consts.DefaultElementsUseViewTransitions then
            writer |> ServerSentEvent.sendDataLine $"{Consts.DatastarDatalineUseViewTransition} %A{options.UseViewTransition}"

        for segment in String.splitLinesToSegments elements do
            writer |> ServerSentEvent.sendDataLine (String.buildDataLine Consts.DatastarDatalineElements segment)

        writer |> ServerSentEvent.writeNewline

        writer.FlushAsync(cancellationToken)

    static member RemoveElement(httpResponse:HttpResponse, selector, ?options:RemoveElementOptions, ?cancellationToken:CancellationToken) =
        let options = defaultArg options RemoveElementOptions.Defaults
        let cancellationToken = defaultArg cancellationToken httpResponse.HttpContext.RequestAborted

        let writer = httpResponse.BodyWriter
        writer |> ServerSentEvent.sendEventType PatchElements
        writer |> ServerSentEvent.sendId options.EventId
        writer |> ServerSentEvent.sendRetry options.Retry

        writer |> ServerSentEvent.sendDataLine $"{Consts.DatastarDatalineSelector} {selector |> Selector.value}"
        writer |> ServerSentEvent.sendDataLine $"{Consts.DatastarDatalineMode} {ElementPatchMode.Remove |> Consts.ElementPatchMode.toString}"

        if options.UseViewTransition <> Consts.DefaultElementsUseViewTransitions then
            writer |> ServerSentEvent.sendDataLine $"{Consts.DatastarDatalineUseViewTransition} %A{options.UseViewTransition}"

        writer |> ServerSentEvent.writeNewline

        writer.FlushAsync(cancellationToken)

    static member PatchSignals(httpResponse:HttpResponse, signals, ?options:PatchSignalsOptions, ?cancellationToken:CancellationToken) =
        let options = defaultArg options PatchSignalsOptions.Defaults
        let cancellationToken = defaultArg cancellationToken httpResponse.HttpContext.RequestAborted

        let writer = httpResponse.BodyWriter
        writer |> ServerSentEvent.sendEventType PatchSignals
        writer |> ServerSentEvent.sendId options.EventId
        writer |> ServerSentEvent.sendRetry options.Retry

        if options.OnlyIfMissing <> Consts.DefaultPatchSignalsOnlyIfMissing then
            writer |> ServerSentEvent.sendDataLine $"{Consts.DatastarDatalineOnlyIfMissing} %A{options.OnlyIfMissing}"

        for segment in String.splitLinesToSegments (Signals.value signals) do
            writer |> ServerSentEvent.sendDataLine (String.buildDataLine Consts.DatastarDatalineSignals segment)

        writer |> ServerSentEvent.writeNewline

        writer.FlushAsync(cancellationToken)

    static member ExecuteScript(httpResponse:HttpResponse, script: string, ?options:ExecuteScriptOptions, ?cancellationToken:CancellationToken) =
        let options = defaultArg options ExecuteScriptOptions.Defaults
        let cancellationToken = defaultArg cancellationToken httpResponse.HttpContext.RequestAborted

        let writer = httpResponse.BodyWriter
        writer |> ServerSentEvent.sendEventType PatchElements
        writer |> ServerSentEvent.sendId options.EventId
        writer |> ServerSentEvent.sendRetry options.Retry

        let needsScriptTag = script.StartsWith("<script>", StringComparison.OrdinalIgnoreCase)

        if needsScriptTag then
            writer |> ServerSentEvent.sendDataLine $"{Consts.DatastarDatalineElements} <script>"

        for segment in String.splitLinesToSegments script do
            writer |> ServerSentEvent.sendDataLine (String.buildDataLine Consts.DatastarDatalineElements segment)

        if needsScriptTag then
            writer |> ServerSentEvent.sendDataLine $"{Consts.DatastarDatalineElements} </script>"

        writer |> ServerSentEvent.writeNewline

        writer.FlushAsync(cancellationToken)

    member this.StartServerEventStreamAsync(additionalHeaders, cancellationToken) =
        lock _startResponseLock (fun () -> if _startResponseTask = null then _startResponseTask <- ServerSentEventGenerator.StartServerEventStream(httpResponse, additionalHeaders, cancellationToken))
        _startResponseTask
    member this.StartServerEventStreamAsync(additionalHeaders) = this.StartServerEventStreamAsync(additionalHeaders, httpResponse.HttpContext.RequestAborted)

    member this.StartServerEventStreamAsync(cancellationToken) =
        lock _startResponseLock (fun () -> if _startResponseTask = null then _startResponseTask <- ServerSentEventGenerator.StartServerEventStream(httpResponse, cancellationToken=cancellationToken))
        _startResponseTask
    member this.StartServerEventStreamAsync() = this.StartServerEventStreamAsync(cancellationToken=httpResponse.HttpContext.RequestAborted)

    member this.PatchElementsAsync(elements, options, cancellationToken) =
        task {
            do!
                if _startResponseTask <> null
                then _startResponseTask
                else ServerSentEventGenerator.StartServerEventStream(httpResponse, cancellationToken=cancellationToken)
            return! ServerSentEventGenerator.PatchElements(httpResponse, elements, options, cancellationToken)
        }
    member this.PatchElementsAsync(elements, options) = this.PatchElementsAsync(elements, PatchElementsOptions.Defaults, httpResponse.HttpContext.RequestAborted)
    member this.PatchElementsAsync(elements, cancellationToken) = this.PatchElementsAsync(elements, PatchElementsOptions.Defaults, cancellationToken)
    member this.PatchElementsAsync(elements) = this.PatchElementsAsync(elements, PatchElementsOptions.Defaults, httpResponse.HttpContext.RequestAborted)

    member this.RemoveElementAsync(selector, options, cancellationToken) =
        task {
            do!
                if _startResponseTask <> null
                then _startResponseTask
                else ServerSentEventGenerator.StartServerEventStream(httpResponse, cancellationToken=cancellationToken)
            return! ServerSentEventGenerator.RemoveElement(httpResponse, selector, options, cancellationToken)
        }
    member this.RemoveElementAsync(selector, options) = this.RemoveElementAsync(selector, RemoveElementOptions.Defaults, httpResponse.HttpContext.RequestAborted)
    member this.RemoveElementAsync(selector, cancellationToken) = this.RemoveElementAsync(selector, RemoveElementOptions.Defaults, cancellationToken)
    member this.RemoveElementAsync(selector) = this.RemoveElementAsync(selector, RemoveElementOptions.Defaults, httpResponse.HttpContext.RequestAborted)

    member this.PatchSignalsAsync(signals, options, cancellationToken) =
        task {
            do!
                if _startResponseTask <> null
                then _startResponseTask
                else ServerSentEventGenerator.StartServerEventStream(httpResponse, cancellationToken=cancellationToken)
            return! ServerSentEventGenerator.PatchSignals(httpResponse, signals, options, cancellationToken)
        }
    member this.PatchSignalsAsync(signals, options) = this.PatchSignalsAsync(signals, PatchSignalsOptions.Defaults, httpResponse.HttpContext.RequestAborted)
    member this.PatchSignalsAsync(signals, cancellationToken) = this.PatchSignalsAsync(signals, PatchSignalsOptions.Defaults, cancellationToken)
    member this.PatchSignalsAsync(signals) = this.PatchSignalsAsync(signals, PatchSignalsOptions.Defaults, httpResponse.HttpContext.RequestAborted)

    member this.ExecuteScriptAsync(script, options, cancellationToken) =
        task {
            do!
                if _startResponseTask <> null
                then _startResponseTask
                else ServerSentEventGenerator.StartServerEventStream(httpResponse, cancellationToken=cancellationToken)
            return! ServerSentEventGenerator.ExecuteScript(httpResponse, script, options, cancellationToken)
        }
    member this.ExecuteScriptAsync(script, options) = this.ExecuteScriptAsync(script, ExecuteScriptOptions.Defaults, httpResponse.HttpContext.RequestAborted)
    member this.ExecuteScriptAsync(script, cancellationToken) = this.ExecuteScriptAsync(script, ExecuteScriptOptions.Defaults, cancellationToken)
    member this.ExecuteScriptAsync(script) = this.ExecuteScriptAsync(script, ExecuteScriptOptions.Defaults, httpResponse.HttpContext.RequestAborted)

    interface ISendServerEvent with
        member this.StartServerEventStreamAsync(additionalHeaders, cancellationToken) = this.StartServerEventStreamAsync(additionalHeaders, cancellationToken)
        member this.PatchElementsAsync(elements, options, cancellationToken) = this.PatchElementsAsync(elements, options, cancellationToken)
        member this.RemoveElementAsync(selector, options, cancellationToken) = this.RemoveElementAsync(selector, options, cancellationToken)
        member this.PatchSignalsAsync(signals, options, cancellationToken) = this.PatchSignalsAsync(signals, options, cancellationToken)
        member this.ExecuteScriptAsync(script, options, cancellationToken) = this.ExecuteScriptAsync(script, options, cancellationToken)

    static member GetSignalsStream(httpRequest:HttpRequest) =
        match httpRequest.Method with
        | System.Net.WebRequestMethods.Http.Get ->
            match httpRequest.Query.TryGetValue(Consts.DatastarKey) with
            | true, stringValues when stringValues.Count > 0 -> (new MemoryStream(Encoding.UTF8.GetBytes(stringValues[0])) :> Stream)
            | _ -> Stream.Null
        | _ -> httpRequest.Body

    static member ReadSignals(httpRequest:HttpRequest, ?cancellationToken:CancellationToken) =
        let cancellationToken = defaultArg cancellationToken httpRequest.HttpContext.RequestAborted

        task {
            match httpRequest.Method with
            | System.Net.WebRequestMethods.Http.Get ->
                match httpRequest.Query.TryGetValue(Consts.DatastarKey) with
                | true, stringValues when stringValues.Count > 0 -> return (stringValues[0] |> Signals.create)
                | _ -> return Signals.empty
            | _ ->
                try
                    use readResult = new StreamReader(httpRequest.Body)
                    let! signals = readResult.ReadToEndAsync(cancellationToken)
                    return (signals |> Signals.create)
                with _ -> return Signals.empty
        }

    static member ReadSignals<'T>(httpRequest:HttpRequest, ?jsonSerializerOptions:JsonSerializerOptions, ?cancellationToken:CancellationToken) =
        let cancellationToken = defaultArg cancellationToken httpRequest.HttpContext.RequestAborted
        let jsonSerializerOptions = defaultArg jsonSerializerOptions JsonSerializerOptions.SignalsDefault

        task {
            try
                match httpRequest.Method with
                | System.Net.WebRequestMethods.Http.Get ->
                    match httpRequest.Query.TryGetValue(Consts.DatastarKey) with
                    | true, stringValues when stringValues.Count > 0 ->
                        return ValueSome (JsonSerializer.Deserialize<'T>(stringValues[0], jsonSerializerOptions))
                    | _ ->
                        return ValueNone
                | _ ->
                    let! t = JsonSerializer.DeserializeAsync<'T>(httpRequest.Body, jsonSerializerOptions, cancellationToken)
                    return (ValueSome t)
            with _ -> return ValueNone
        }

    member this.GetSignalsStream() = ServerSentEventGenerator.GetSignalsStream(httpRequest)

    member this.ReadSignalsAsync(cancellationToken): Task<Signals> = ServerSentEventGenerator.ReadSignals(httpRequest, cancellationToken)
    member this.ReadSignalsAsync(): Task<Signals> = ServerSentEventGenerator.ReadSignals(httpRequest, httpRequest.HttpContext.RequestAborted)

    member this.ReadSignalsAsync<'T>(jsonSerializerOptions, cancellationToken) = ServerSentEventGenerator.ReadSignals<'T>(httpRequest, jsonSerializerOptions, cancellationToken)
    member this.ReadSignalsAsync<'T>(jsonSerializerOptions) = ServerSentEventGenerator.ReadSignals<'T>(httpRequest, jsonSerializerOptions, httpRequest.HttpContext.RequestAborted)
    member this.ReadSignalsAsync<'T>(cancellationToken) = ServerSentEventGenerator.ReadSignals<'T>(httpRequest, JsonSerializerOptions.SignalsDefault, cancellationToken)
    member this.ReadSignalsAsync<'T>() = ServerSentEventGenerator.ReadSignals<'T>(httpRequest, JsonSerializerOptions.SignalsDefault, httpRequest.HttpContext.RequestAborted)

    interface IReadSignals with
        member this.GetSignalsStream() =
            ServerSentEventGenerator.GetSignalsStream(httpRequest)

        member this.ReadSignalsAsync(cancellationToken:CancellationToken): Task<Signals> =
            ServerSentEventGenerator.ReadSignals(httpRequest, cancellationToken)

        member this.ReadSignalsAsync<'T>(jsonSerializerOptions, cancellationToken:CancellationToken) =
            ServerSentEventGenerator.ReadSignals<'T>(httpRequest, jsonSerializerOptions, cancellationToken)
