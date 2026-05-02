using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace parallel_project
{
    public sealed class GameConnection : IAsyncDisposable
    {
        public const string DefaultPipeName = "parallel_project_game";

        private readonly string _pipeName;
        private readonly bool _isServer;

        private NamedPipeServerStream? _server;
        private NamedPipeClientStream? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private Task? _readLoop;

        public bool IsConnected { get; private set; }
        public bool IsServer => _isServer;
        public string PipeName => _pipeName;

        public event Action<string>? MessageReceived;
        public event Action<string>? Disconnected;

        /// <summary>
        /// Creates a new connection wrapper for a named pipe.
        /// </summary>
        /// <param name="pipeName">Pipe name to host/connect to.</param>
        /// <param name="isServer">True to create the server side; false to create the client side.</param>
        public GameConnection(string pipeName, bool isServer)
        {
            _pipeName = pipeName;
            _isServer = isServer;
        }

        /// <summary>
        /// Factory for a server-side connection (host).
        /// </summary>
        /// <param name="pipeName">Optional custom pipe name; defaults to <see cref="DefaultPipeName"/>.</param>
        public static GameConnection CreateServer(string? pipeName = null) => new GameConnection(pipeName ?? DefaultPipeName, isServer: true);

        /// <summary>
        /// Factory for a client-side connection (joiner).
        /// </summary>
        /// <param name="pipeName">Optional custom pipe name; defaults to <see cref="DefaultPipeName"/>.</param>
        public static GameConnection CreateClient(string? pipeName = null) => new GameConnection(pipeName ?? DefaultPipeName, isServer: false);

        /// <summary>
        /// Starts the underlying pipe and begins the background read loop.
        /// </summary>
        /// <param name="token">Cancellation token used while waiting for connection.</param>
        /// <remarks>
        /// Logic: Branches between server wait-for-connection and client connect. Once connected, it creates
        /// a StreamReader/StreamWriter and starts a dedicated read loop that raises <see cref="MessageReceived"/>.
        /// </remarks>
        public async Task StartAsync(CancellationToken token)
        {
            if (_isServer)
                await StartServerAsync(token);
            else
                await StartClientAsync(token);
        }

        /// <summary>
        /// Creates the server pipe and waits for a single client connection.
        /// </summary>
        /// <param name="token">Cancellation token for the wait.</param>
        private async Task StartServerAsync(CancellationToken token)
        {
            _server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await _server.WaitForConnectionAsync(token);
            SetupStreams(_server);
        }

        /// <summary>
        /// Connects to an existing server pipe.
        /// </summary>
        /// <param name="token">Cancellation token for the connect.</param>
        private async Task StartClientAsync(CancellationToken token)
        {
            _client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await _client.ConnectAsync(token);
            SetupStreams(_client);
        }

        /// <summary>
        /// Initializes reader/writer wrappers and kicks off the async read loop.
        /// </summary>
        /// <param name="stream">Connected pipe stream.</param>
        private void SetupStreams(Stream stream)
        {
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream) { AutoFlush = true };
            IsConnected = true;
            _readLoop = Task.Run(() => ReadLoopAsync(_shutdown.Token));
        }

        /// <summary>
        /// Continuously reads line-delimited messages and raises <see cref="MessageReceived"/>.
        /// </summary>
        /// <param name="token">Cancellation token used to stop the loop during disposal.</param>
        /// <remarks>
        /// Logic: A null line indicates the remote end closed. Any read exception triggers a disconnect callback
        /// unless we are already shutting down.
        /// </remarks>
        private async Task ReadLoopAsync(CancellationToken token)
        {
            string reason = "Disconnected";
            try
            {
                while (!token.IsCancellationRequested)
                {
                    string? line = await _reader!.ReadLineAsync();
                    if (line == null)
                        break;
                    MessageReceived?.Invoke(line);
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    reason = "Read error: " + ex.Message;
            }
            finally
            {
                if (IsConnected)
                    Disconnected?.Invoke(reason);
                IsConnected = false;
            }
        }

        /// <summary>
        /// Sends a single line-delimited message to the remote endpoint.
        /// </summary>
        /// <param name="message">Message payload (a single line; newline not expected).</param>
        /// <param name="token">Cancellation token for the send lock/writer.</param>
        /// <remarks>
        /// Logic: Uses a semaphore so concurrent sends don't interleave and corrupt the stream.
        /// </remarks>
        public async Task SendAsync(string message, CancellationToken token = default)
        {
            if (!IsConnected || _writer == null)
                return;

            await _sendLock.WaitAsync(token);
            try
            {
                await _writer.WriteLineAsync(message);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// Stops the read loop and releases all underlying resources.
        /// </summary>
        /// <remarks>
        /// Logic: Cancels the internal shutdown token, awaits the read loop, then disposes streams and locks.
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            try { _shutdown.Cancel(); } catch { }

            if (_readLoop != null)
            {
                try { await _readLoop; } catch { }
            }

            try { _reader?.Dispose(); } catch { }
            try { _writer?.Dispose(); } catch { }
            try { _server?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }

            try { _sendLock.Dispose(); } catch { }
            try { _shutdown.Dispose(); } catch { }
        }
    }
}
