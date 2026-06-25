namespace Loupedeck.LoupedeckHelperPlugin.State
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.IO.Pipes;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.LoupedeckHelperPlugin.Helpers;
    using Loupedeck.SharedState;

    internal sealed class SharedStateIpcServer : IDisposable
    {
        private readonly MultiWheelFnState _state;
        private readonly SharedStateEndpoint _endpoint;
        private readonly ConcurrentDictionary<Guid, ClientSession> _subscribers = new();
        private CancellationTokenSource _cancellation;
        private Socket _unixSocket;

        public SharedStateIpcServer(MultiWheelFnState state)
        {
            this._state = state;
            this._endpoint = SharedStateEndpoint.CreateDefault();
        }

        public void Start()
        {
            this._cancellation = new CancellationTokenSource();
            this._state.Changed += this.OnStateChanged;

            if (this._endpoint.IsUnix)
            {
                this.StartUnixSocket();
            }
            else if (this._endpoint.IsNamedPipe)
            {
                _ = Task.Run(() => this.RunNamedPipeAcceptLoopAsync(this._cancellation.Token));
            }
            else
            {
                throw new NotSupportedException($"Unsupported endpoint {this._endpoint.RawValue}");
            }

            SharedStateDiscovery.Write(this._endpoint);
            PluginLog.Info($"[LoupedeckSharedState] Started IPC endpoint {this._endpoint.RawValue}");
        }

        public void Dispose()
        {
            this._state.Changed -= this.OnStateChanged;
            this._cancellation?.Cancel();
            this._unixSocket?.Dispose();

            foreach (var subscriber in this._subscribers.Values)
            {
                subscriber.Dispose();
            }

            this._subscribers.Clear();

            if (this._endpoint.IsUnix)
            {
                TryDeleteFile(this._endpoint.Address);
            }

            this._cancellation?.Dispose();
        }

        private void StartUnixSocket()
        {
            TryDeleteFile(this._endpoint.Address);
            this._unixSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            this._unixSocket.Bind(new UnixDomainSocketEndPoint(this._endpoint.Address));
            this._unixSocket.Listen(20);
            _ = Task.Run(() => this.RunUnixAcceptLoopAsync(this._cancellation.Token));
        }

        private async Task RunUnixAcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var socket = await this._unixSocket.AcceptAsync(cancellationToken).ConfigureAwait(false);
                    _ = Task.Run(() => this.HandleClientAsync(new NetworkStream(socket, ownsSocket: true), cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    PluginLog.Warning(ex, "[LoupedeckSharedState] Unix socket accept failed");
                }
            }
        }

        private async Task RunNamedPipeAcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = new NamedPipeServerStream(
                        this._endpoint.Address,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    var connectedPipe = pipe;
                    pipe = null;
                    _ = Task.Run(() => this.HandleClientAsync(connectedPipe, cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    pipe?.Dispose();
                    break;
                }
                catch (Exception ex)
                {
                    pipe?.Dispose();
                    PluginLog.Warning(ex, "[LoupedeckSharedState] Named pipe accept failed");
                }
            }
        }

        private async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var session = new ClientSession(stream);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await session.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line == null)
                    {
                        break;
                    }

                    var request = JsonSerializer.Deserialize<StateRequest>(line, JsonOptions());
                    if (request == null)
                    {
                        await session.WriteResponseAsync(StateResponse.Fail(null, "invalid-json"), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (!String.Equals(request.Key, SharedStateConstants.MultiWheelKeepActiveKey, StringComparison.Ordinal))
                    {
                        await session.WriteResponseAsync(StateResponse.Fail(request.Id, "unknown-key"), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var keepOpen = await this.HandleRequestAsync(session, request, cancellationToken).ConfigureAwait(false);
                    if (!keepOpen)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "[LoupedeckSharedState] Client handling failed");
            }
            finally
            {
                this._subscribers.TryRemove(session.Id, out _);
            }
        }

        private async Task<Boolean> HandleRequestAsync(ClientSession session, StateRequest request, CancellationToken cancellationToken)
        {
            switch (request.Command)
            {
                case "get":
                    await session.WriteResponseAsync(StateResponse.Success(request.Id, this._state.IsEnabled), cancellationToken).ConfigureAwait(false);
                    return false;
                case "set":
                    if (!request.Value.HasValue)
                    {
                        await session.WriteResponseAsync(StateResponse.Fail(request.Id, "missing-value"), cancellationToken).ConfigureAwait(false);
                        return false;
                    }

                    await session.WriteResponseAsync(StateResponse.Success(request.Id, this._state.Set(request.Value.Value)), cancellationToken).ConfigureAwait(false);
                    return false;
                case "toggle":
                    await session.WriteResponseAsync(StateResponse.Success(request.Id, this._state.Toggle()), cancellationToken).ConfigureAwait(false);
                    return false;
                case "disable":
                    await session.WriteResponseAsync(StateResponse.Success(request.Id, this._state.Disable()), cancellationToken).ConfigureAwait(false);
                    return false;
                case "subscribe":
                    this._subscribers[session.Id] = session;
                    await session.WriteResponseAsync(StateResponse.Success(request.Id, this._state.IsEnabled), cancellationToken).ConfigureAwait(false);
                    await session.WriteEventAsync(this._state.IsEnabled, cancellationToken).ConfigureAwait(false);
                    return true;
                default:
                    await session.WriteResponseAsync(StateResponse.Fail(request.Id, "unknown-command"), cancellationToken).ConfigureAwait(false);
                    return false;
            }
        }

        private void OnStateChanged(Boolean value)
        {
            PluginLog.Info($"[LoupedeckSharedState] MultiWheel keep-active changed to {value.ToString().ToLowerInvariant()}");

            foreach (var subscriber in this._subscribers.Values)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await subscriber.WriteEventAsync(value, this._cancellation.Token).ConfigureAwait(false);
                    }
                    catch
                    {
                        this._subscribers.TryRemove(subscriber.Id, out _);
                    }
                });
            }
        }

        private static void TryDeleteFile(String path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static JsonSerializerOptions JsonOptions() => new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private sealed class StateRequest
        {
            public String Id { get; set; }

            [JsonPropertyName("cmd")]
            public String Command { get; set; }

            public String Key { get; set; }
            public Boolean? Value { get; set; }
        }

        private sealed class StateResponse
        {
            public String Id { get; set; }
            public Boolean Ok { get; set; }
            public Boolean? Value { get; set; }
            public String Error { get; set; }

            public static StateResponse Success(String id, Boolean value) => new() { Id = id, Ok = true, Value = value };

            public static StateResponse Fail(String id, String error) => new() { Id = id, Ok = false, Error = error };
        }

        private sealed class StateEvent
        {
            public String Event { get; set; } = "changed";
            public String Key { get; set; } = SharedStateConstants.MultiWheelKeepActiveKey;
            public Boolean Value { get; set; }
        }

        private sealed class ClientSession : IDisposable
        {
            private readonly Stream _stream;
            private readonly StreamReader _reader;
            private readonly StreamWriter _writer;
            private readonly SemaphoreSlim _writeLock = new(1, 1);

            public ClientSession(Stream stream)
            {
                this.Id = Guid.NewGuid();
                this._stream = stream;
                this._reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
                this._writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            }

            public Guid Id { get; }

            public Task<String> ReadLineAsync(CancellationToken cancellationToken) =>
                this._reader.ReadLineAsync(cancellationToken).AsTask();

            public Task WriteResponseAsync(StateResponse response, CancellationToken cancellationToken) =>
                this.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions()), cancellationToken);

            public Task WriteEventAsync(Boolean value, CancellationToken cancellationToken) =>
                this.WriteLineAsync(JsonSerializer.Serialize(new StateEvent { Value = value }, JsonOptions()), cancellationToken);

            public void Dispose()
            {
                this._writer.Dispose();
                this._reader.Dispose();
                this._stream.Dispose();
                this._writeLock.Dispose();
            }

            private async Task WriteLineAsync(String line, CancellationToken cancellationToken)
            {
                await this._writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await this._writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    this._writeLock.Release();
                }
            }
        }
    }
}
