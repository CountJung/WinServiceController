using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Serilog;
using WinServiceController.Models;

namespace WinServiceController.Services
{
    public class PipeClientService : IPipeClientService, IDisposable
    {
        private const string PipeName = "ServiceMonitorPipe";
        private const int ConnectTimeoutMs = 3000;
        private const int BufferSize = 4096;

        private NamedPipeClientStream? _pipeStream;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public bool IsConnected => _pipeStream is { IsConnected: true };

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Disconnect();

                var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(ConnectTimeoutMs, cancellationToken);
                pipe.ReadMode = PipeTransmissionMode.Message;

                _pipeStream = pipe;
                Log.Debug("Connected to monitoring engine pipe");
                return true;
            }
            catch (TimeoutException)
            {
                Log.Debug("Monitoring engine pipe connection timed out");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to connect to monitoring engine pipe");
                return false;
            }
        }

        public void Disconnect()
        {
            var stream = _pipeStream;
            _pipeStream = null;
            stream?.Dispose();
        }

        public async Task<IpcResponse?> SendCommandAsync(IpcRequest request, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!IsConnected)
                {
                    var connected = await ConnectAsync(cancellationToken);
                    if (!connected)
                        return null;
                }

                var json = JsonSerializer.Serialize(request);
                var requestBytes = Encoding.UTF8.GetBytes(json);

                await _pipeStream!.WriteAsync(requestBytes, cancellationToken);
                await _pipeStream.FlushAsync(cancellationToken);

                var buffer = new byte[BufferSize];
                var bytesRead = await _pipeStream.ReadAsync(buffer, cancellationToken);

                if (bytesRead == 0)
                    return null;

                var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                return JsonSerializer.Deserialize<IpcResponse>(responseJson);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "IPC communication error");
                Disconnect();
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            Disconnect();
            _semaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
