namespace Loupedeck.LoupedeckHelperPlugin
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class SharedStateIpcServer : IDisposable
    {
        private readonly MultiWheelFnState _state;
        private readonly Action<String> _log;
        private readonly Action<Exception, String> _logError;
        private readonly CancellationTokenSource _cancellation = new();
        private Socket _unixSocket;

        public SharedStateIpcServer(MultiWheelFnState state, Action<String> log, Action<Exception, String> logError)
        {
            this._state = state;
            this._log = log;
            this._logError = logError;
        }

        public void Start()
        {
            Directory.CreateDirectory(SharedStateDiscovery.GetDirectory());

            if (OperatingSystem.IsWindows())
            {
                _ = Task.Run(() => this.RunPipeLoopAsync(this._cancellation.Token));
            }
            else
            {
                this.StartUnixSocket();
            }

            SharedStateDiscovery.Write();
            this._log($"[LoupedeckSharedState] Started IPC endpoint {SharedStateDiscovery.Endpoint}");
        }

        public void Dispose()
        {
            this._cancellation.Cancel();
            this._unixSocket?.Dispose();
            SharedStateDiscovery.Delete();
            this._cancellation.Dispose();
        }

        private void StartUnixSocket()
        {
            File.Delete(SharedStateDiscovery.UnixSocketPath);
            this._unixSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            this._unixSocket.Bind(new UnixDomainSocketEndPoint(SharedStateDiscovery.UnixSocketPath));
            this._unixSocket.Listen(8);
            _ = Task.Run(() => this.RunUnixLoopAsync(this._cancellation.Token));
        }

        private async Task RunUnixLoopAsync(CancellationToken cancellationToken)
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
                    this._logError(ex, "[LoupedeckSharedState] Unix socket accept failed");
                }
            }
        }

        private async Task RunPipeLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = new NamedPipeServerStream(
                        SharedStateDiscovery.Pipe,
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
                    this._logError(ex, "[LoupedeckSharedState] Named pipe accept failed");
                }
            }
        }

        private async Task HandleClientAsync(Stream stream, CancellationToken cancellationToken)
        {
            await using (stream.ConfigureAwait(false))
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true }.ConfigureAwait(false))
            {
                try
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    var response = this.HandleRequest(line);
                    await writer.WriteLineAsync(response.AsMemory(), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    this._logError(ex, "[LoupedeckSharedState] Client request failed");
                }
            }
        }

        private String HandleRequest(String line)
        {
            try
            {
                var request = JsonSerializer.Deserialize<Request>(line ?? "", JsonOptions());
                if (request == null || request.Key != SharedStateDiscovery.Key)
                {
                    return Serialize(Response.Fail(request?.Id, "unknown-key"));
                }

                switch (request.Command)
                {
                    case "get":
                        return Serialize(Response.Success(request.Id, this._state.IsEnabled));
                    case "set":
                        if (!request.Value.HasValue)
                        {
                            return Serialize(Response.Fail(request.Id, "missing-value"));
                        }

                        this._state.Set(request.Value.Value);
                        return Serialize(Response.Success(request.Id, this._state.IsEnabled));
                    case "toggle":
                        this._state.Toggle();
                        return Serialize(Response.Success(request.Id, this._state.IsEnabled));
                    case "disable":
                        this._state.Disable();
                        return Serialize(Response.Success(request.Id, this._state.IsEnabled));
                    default:
                        return Serialize(Response.Fail(request.Id, "unknown-command"));
                }
            }
            catch (Exception ex)
            {
                this._logError(ex, "[LoupedeckSharedState] Invalid request");
                return Serialize(Response.Fail(null, "invalid-request"));
            }
        }

        private static String Serialize(Response response) => JsonSerializer.Serialize(response, JsonOptions());

        private static JsonSerializerOptions JsonOptions() => new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private sealed class Request
        {
            public String Id { get; set; }

            [JsonPropertyName("cmd")]
            public String Command { get; set; }

            public String Key { get; set; }

            public Boolean? Value { get; set; }
        }

        private sealed class Response
        {
            public String Id { get; set; }

            public Boolean Ok { get; set; }

            public Boolean? Value { get; set; }

            public String Error { get; set; }

            public static Response Success(String id, Boolean value) => new() { Id = id, Ok = true, Value = value };

            public static Response Fail(String id, String error) => new() { Id = id, Ok = false, Error = error };
        }
    }
}
