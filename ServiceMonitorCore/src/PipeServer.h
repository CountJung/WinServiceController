#pragma once

#include <string>
#include <functional>
#include <thread>
#include <atomic>
#include <mutex>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace smc {

/// Asynchronous duplex Named Pipe server for IPC with the WPF UI.
/// Protocol: JSON over UTF-8, newline-delimited messages.
class PipeServer {
public:
    using MessageHandler = std::function<std::string(const std::string& requestJson)>;

    explicit PipeServer(const std::wstring& pipeName = L"\\\\.\\pipe\\ServiceMonitorPipe");
    ~PipeServer();

    PipeServer(const PipeServer&) = delete;
    PipeServer& operator=(const PipeServer&) = delete;

    /// Set the handler that processes incoming JSON requests and returns JSON responses.
    void SetMessageHandler(MessageHandler handler);

    /// Start listening for client connections (non-blocking, runs in background thread).
    void Start();

    /// Stop the pipe server.
    void Stop();

    bool IsRunning() const { return running_.load(); }

private:
    void ListenLoop();
    void HandleClient(HANDLE pipe);

    std::wstring pipeName_;
    MessageHandler messageHandler_;
    std::thread listenThread_;
    std::atomic<bool> running_{ false };
    HANDLE stopEvent_ = nullptr;
};

} // namespace smc
