#include "MonitorService.h"
#include "Logger.h"

#include <filesystem>
#include <iostream>
#include <csignal>
#include <atomic>

static std::atomic<bool> g_running{ true };

static void SignalHandler(int /*sig*/)
{
    g_running = false;
}

/// Run the monitoring engine in console mode for development/testing.
static int RunConsoleMode(const std::filesystem::path& logDir)
{
    if (::AllocConsole()) {
        FILE* fp = nullptr;
        freopen_s(&fp, "CONOUT$", "w", stdout);
        freopen_s(&fp, "CONOUT$", "w", stderr);
        freopen_s(&fp, "CONIN$", "r", stdin);
    }

    std::signal(SIGINT, SignalHandler);

    smc::Logger::Init(logDir);
    std::wcout << L"[Console Mode] ServiceMonitorCore started. Press Ctrl+C to stop.\n";

    smc::MonitorService service;
    service.RunConsole();

    std::wcout << L"[Console Mode] Pipe server listening on \\\\.\\pipe\\ServiceMonitorPipe\n";

    while (g_running) {
        ::Sleep(500);
    }

    std::wcout << L"\n[Console Mode] Shutting down...\n";
    service.StopConsole();
    smc::Logger::Shutdown();
    return 0;
}

int WINAPI wWinMain(
    _In_ HINSTANCE /*hInstance*/,
    _In_opt_ HINSTANCE /*hPrevInstance*/,
    _In_ LPWSTR lpCmdLine,
    _In_ int /*nCmdShow*/)
{
    wchar_t exePath[MAX_PATH]{};
    ::GetModuleFileNameW(nullptr, exePath, MAX_PATH);
    auto logDir = std::filesystem::path(exePath).parent_path() / L"logs";

    std::wstring cmdLine = lpCmdLine ? lpCmdLine : L"";
    if (cmdLine.find(L"--console") != std::wstring::npos) {
        return RunConsoleMode(logDir);
    }

    // Normal Windows Service mode
    smc::Logger::Init(logDir);

    smc::MonitorService service;
    bool result = smc::ServiceBase::Run(service);

    // Fallback to console mode if not launched as a service
    if (!result && ::GetLastError() == ERROR_FAILED_SERVICE_CONTROLLER_CONNECT) {
        smc::Logger::Shutdown();
        return RunConsoleMode(logDir);
    }

    smc::Logger::Shutdown();
    return result ? 0 : 1;
}
