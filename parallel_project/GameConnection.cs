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

        public GameConnection(string pipeName, bool isServer)
        {
            _pipeName = pipeName;
            _isServer = isServer;
        }

        public static GameConnection CreateServer(string? pipeName = null) => new GameConnection(pipeName ?? DefaultPipeName, isServer: true);
        public static GameConnection CreateClient(string? pipeName = null) => new GameConnection(pipeName ?? DefaultPipeName, isServer: false);

        public async Task StartAsync(CancellationToken token)
        {
            if (_isServer)
                await StartServerAsync(token);
            else
                await StartClientAsync(token);
        }

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

        private void SetupStreams(Stream stream)
        {
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream) { AutoFlush = true };
            IsConnected = true;
            _readLoop = Task.Run(() => ReadLoopAsync(_shutdown.Token));
        }

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
