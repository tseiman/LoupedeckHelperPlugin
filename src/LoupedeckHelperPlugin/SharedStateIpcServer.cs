namespace Loupedeck.LoupedeckHelperPlugin
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

    internal sealed class SharedStateIpcServer : IDisposable
    {
        private readonly MultiWheelFnState _state;
        private readonly Action<String> _log;
        private readonly Action<Exception, String> _logError;
        private readonly ConcurrentDictionary<Guid, StreamWriter> _watchers = new();
        private readonly CancellationTokenSource _cancellation = new();
        private Socket _unixSocket;

        public SharedStateIpcServer(MultiWheelFnState state, Action<String> log, Action<Exception, String> logError)
        {
            this._state = state;
            this._log = log;
            this._logError = logError;
            this._state.Changed += this.OnStateChanged;
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
            this._state.Changed -= this.OnStateChanged;
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
            await using (stream)
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true })
            {
                try
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    var keepOpen = await this.HandleRequestAsync(line, writer, cancellationToken).ConfigureAwait(false);
                    if (keepOpen)
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    }
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

        private async Task<Boolean> HandleRequestAsync(String line, StreamWriter writer, CancellationToken cancellationToken)
        {
            try
            {
                var request = JsonSerializer.Deserialize<Request>(line ?? "", JsonOptions());
                if (request == null || request.Key != SharedStateDiscovery.Key)
                {
                    await writer.WriteLineAsync(Serialize(Response.Fail("unknown-key")).AsMemory(), cancellationToken).ConfigureAwait(false);
                    return false;
                }

                switch (request.Command)
                {
                    case "get":
                        await writer.WriteLineAsync(Serialize(Response.State(this._state.IsEnabled)).AsMemory(), cancellationToken).ConfigureAwait(false);
                        return false;
                    case "watch":
                        this._watchers[Guid.NewGuid()] = writer;
                        return true;
                    default:
                        await writer.WriteLineAsync(Serialize(Response.Fail("unknown-command")).AsMemory(), cancellationToken).ConfigureAwait(false);
                        return false;
                }
            }
            catch (Exception ex)
            {
                this._logError(ex, "[LoupedeckSharedState] Invalid request");
                await writer.WriteLineAsync(Serialize(Response.Fail("invalid-request")).AsMemory(), cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        private void OnStateChanged()
        {
            var message = Serialize(Response.Changed(this._state.IsEnabled));
            foreach (var watcher in this._watchers)
            {
                try
                {
                    watcher.Value.WriteLine(message);
                }
                catch
                {
                    this._watchers.TryRemove(watcher.Key, out _);
                }
            }
        }

        private static String Serialize(Response response) => JsonSerializer.Serialize(response, JsonOptions());

        private static JsonSerializerOptions JsonOptions() => new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private sealed class Request
        {
            [JsonPropertyName("cmd")]
            public String Command { get; set; }

            public String Key { get; set; }
        }

        private sealed class Response
        {
            public String Event { get; set; }

            public Boolean Ok { get; set; }

            public String Key { get; set; }

            public Boolean? Value { get; set; }

            public String Error { get; set; }

            public static Response State(Boolean value) => new() { Ok = true, Key = SharedStateDiscovery.Key, Value = value };

            public static Response Changed(Boolean value) => new() { Event = "changed", Key = SharedStateDiscovery.Key, Value = value };

            public static Response Fail(String error) => new() { Ok = false, Error = error };
        }
    }
}
