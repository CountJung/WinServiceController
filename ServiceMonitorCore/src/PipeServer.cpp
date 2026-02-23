#include "PipeServer.h"
#include "Logger.h"

#include <memory>
#include <vector>

namespace smc {

PipeServer::PipeServer(const std::wstring& pipeName)
    : pipeName_(pipeName)
{
    stopEvent_ = ::CreateEventW(nullptr, TRUE, FALSE, nullptr);
}

PipeServer::~PipeServer()
{
    Stop();
    if (stopEvent_)
        ::CloseHandle(stopEvent_);
}

void PipeServer::SetMessageHandler(MessageHandler handler)
{
    messageHandler_ = std::move(handler);
}

void PipeServer::Start()
{
    if (running_.load())
        return;

    running_ = true;
    ::ResetEvent(stopEvent_);
    listenThread_ = std::thread(&PipeServer::ListenLoop, this);

    Logger::Info(L"Pipe server started: " + pipeName_);
}

void PipeServer::Stop()
{
    if (!running_.load())
        return;

    running_ = false;
    ::SetEvent(stopEvent_);

    if (listenThread_.joinable())
        listenThread_.join();

    Logger::Info(L"Pipe server stopped");
}

void PipeServer::ListenLoop()
{
    // Security descriptor allowing local connections only
    SECURITY_ATTRIBUTES sa{};
    sa.nLength = sizeof(sa);
    sa.bInheritHandle = FALSE;

    while (running_.load()) {
        HANDLE pipe = ::CreateNamedPipeW(
            pipeName_.c_str(),
            PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED,
            PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
            PIPE_UNLIMITED_INSTANCES,
            4096,   // output buffer
            4096,   // input buffer
            0,      // default timeout
            &sa
        );

        if (pipe == INVALID_HANDLE_VALUE) {
            Logger::Error(L"CreateNamedPipe failed: " + std::to_wstring(::GetLastError()));
            ::Sleep(1000);
            continue;
        }

        // Overlapped connect so we can check for stop signal
        OVERLAPPED ol{};
        ol.hEvent = ::CreateEventW(nullptr, TRUE, FALSE, nullptr);

        BOOL connected = ::ConnectNamedPipe(pipe, &ol);
        if (!connected) {
            DWORD err = ::GetLastError();
            if (err == ERROR_IO_PENDING) {
                HANDLE waitHandles[] = { ol.hEvent, stopEvent_ };
                DWORD waitResult = ::WaitForMultipleObjects(2, waitHandles, FALSE, INFINITE);

                if (waitResult == WAIT_OBJECT_0 + 1) {
                    // Stop requested
                    ::CancelIo(pipe);
                    ::CloseHandle(ol.hEvent);
                    ::CloseHandle(pipe);
                    break;
                }
            }
            else if (err != ERROR_PIPE_CONNECTED) {
                ::CloseHandle(ol.hEvent);
                ::CloseHandle(pipe);
                continue;
            }
        }

        ::CloseHandle(ol.hEvent);

        // Handle client in the same thread (single-client model for simplicity)
        HandleClient(pipe);

        ::DisconnectNamedPipe(pipe);
        ::CloseHandle(pipe);
    }
}

void PipeServer::HandleClient(HANDLE pipe)
{
    constexpr DWORD bufSize = 4096;
    std::vector<char> buffer(bufSize);

    while (running_.load()) {
        DWORD bytesRead = 0;
        BOOL success = ::ReadFile(pipe, buffer.data(), bufSize, &bytesRead, nullptr);

        if (!success || bytesRead == 0) {
            if (::GetLastError() == ERROR_BROKEN_PIPE) {
                Logger::Info(L"Client disconnected");
            }
            break;
        }

        std::string request(buffer.data(), bytesRead);

        std::string response;
        if (messageHandler_) {
            try {
                response = messageHandler_(request);
            }
            catch (const std::exception& ex) {
                response = R"({"error":")" + std::string(ex.what()) + R"("})";
                Logger::Error(L"Handler exception: " + std::wstring(ex.what(), ex.what() + strlen(ex.what())));
            }
        }
        else {
            response = R"({"error":"no handler"})";
        }

        DWORD bytesWritten = 0;
        ::WriteFile(pipe, response.c_str(), static_cast<DWORD>(response.size()), &bytesWritten, nullptr);
        ::FlushFileBuffers(pipe);
    }
}

} // namespace smc
