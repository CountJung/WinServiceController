using WinServiceController.Models;

namespace WinServiceController.Services
{
    public interface IPipeClientService
    {
        bool IsConnected { get; }

        Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

        void Disconnect();

        Task<IpcResponse?> SendCommandAsync(IpcRequest request, CancellationToken cancellationToken = default);
    }
}
