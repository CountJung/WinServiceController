#include "ResourceCollector.h"
#include "Logger.h"

#include <memory>

#pragma comment(lib, "pdh.lib")
#pragma comment(lib, "psapi.lib")
#pragma comment(lib, "advapi32.lib")

namespace smc {

ResourceCollector::ResourceCollector()
{
    SYSTEM_INFO sysInfo{};
    ::GetSystemInfo(&sysInfo);
    numProcessors_ = static_cast<int>(sysInfo.dwNumberOfProcessors);
    if (numProcessors_ < 1) numProcessors_ = 1;
}

ResourceCollector::~ResourceCollector() = default;

ServiceMetrics ResourceCollector::Collect(DWORD processId)
{
    ServiceMetrics metrics{};

    if (processId == 0)
        return metrics;

    HANDLE hProcess = ::OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processId);
    if (!hProcess)
        return metrics;

    // RAII handle wrapper
    auto closer = std::unique_ptr<void, decltype(&::CloseHandle)>(hProcess, ::CloseHandle);

    // CPU usage (per-process state tracking)
    metrics.cpuPercent = CalculateCpuUsage(hProcess, processId);

    // Memory usage
    PROCESS_MEMORY_COUNTERS_EX pmc{};
    pmc.cb = sizeof(pmc);
    if (::GetProcessMemoryInfo(hProcess, reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&pmc), sizeof(pmc))) {
        metrics.memoryMB = static_cast<double>(pmc.WorkingSetSize) / (1024.0 * 1024.0);
    }

    // Uptime
    FILETIME createTime{}, exitTime{}, kernelTime{}, userTime{};
    if (::GetProcessTimes(hProcess, &createTime, &exitTime, &kernelTime, &userTime)) {
        FILETIME nowFt{};
        ::GetSystemTimeAsFileTime(&nowFt);

        ULARGE_INTEGER start{}, now{};
        start.LowPart = createTime.dwLowDateTime;
        start.HighPart = createTime.dwHighDateTime;
        now.LowPart = nowFt.dwLowDateTime;
        now.HighPart = nowFt.dwHighDateTime;

        metrics.uptimeSeconds = (now.QuadPart - start.QuadPart) / 10'000'000ULL;
    }

    return metrics;
}

double ResourceCollector::CalculateCpuUsage(HANDLE hProcess, DWORD processId)
{
    FILETIME nowFt{}, creationFt{}, exitFt{}, kernelFt{}, userFt{};
    ::GetSystemTimeAsFileTime(&nowFt);

    if (!::GetProcessTimes(hProcess, &creationFt, &exitFt, &kernelFt, &userFt))
        return 0.0;

    ULARGE_INTEGER now{}, kernel{}, user{};
    now.LowPart = nowFt.dwLowDateTime;
    now.HighPart = nowFt.dwHighDateTime;
    kernel.LowPart = kernelFt.dwLowDateTime;
    kernel.HighPart = kernelFt.dwHighDateTime;
    user.LowPart = userFt.dwLowDateTime;
    user.HighPart = userFt.dwHighDateTime;

    auto& state = cpuStates_[processId];

    if (state.lastTime.QuadPart == 0) {
        state.lastTime = now;
        state.lastKernel = kernel;
        state.lastUser = user;
        return 0.0;
    }

    auto timeDelta = now.QuadPart - state.lastTime.QuadPart;
    if (timeDelta == 0)
        return 0.0;

    auto cpuDelta = (kernel.QuadPart - state.lastKernel.QuadPart) +
                    (user.QuadPart - state.lastUser.QuadPart);

    double percent = (static_cast<double>(cpuDelta) / static_cast<double>(timeDelta)) * 100.0 / numProcessors_;

    // Clamp to [0, 100] — shared svchost processes can occasionally overshoot due to timing
    if (percent < 0.0) percent = 0.0;
    if (percent > 100.0) percent = 100.0;

    state.lastTime = now;
    state.lastKernel = kernel;
    state.lastUser = user;

    return percent;
}

DWORD ResourceCollector::GetServiceProcessId(const std::wstring& serviceName)
{
    SC_HANDLE scManager = ::OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT);
    if (!scManager)
        return 0;

    auto scmCloser = std::unique_ptr<SC_HANDLE__, decltype(&::CloseServiceHandle)>(scManager, ::CloseServiceHandle);

    SC_HANDLE service = ::OpenServiceW(scManager, serviceName.c_str(), SERVICE_QUERY_STATUS);
    if (!service)
        return 0;

    auto svcCloser = std::unique_ptr<SC_HANDLE__, decltype(&::CloseServiceHandle)>(service, ::CloseServiceHandle);

    SERVICE_STATUS_PROCESS ssp{};
    DWORD bytesNeeded = 0;
    if (!::QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO,
        reinterpret_cast<LPBYTE>(&ssp), sizeof(ssp), &bytesNeeded))
        return 0;

    return ssp.dwProcessId;
}

std::wstring ResourceCollector::GetServiceStatus(const std::wstring& serviceName)
{
    SC_HANDLE scManager = ::OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT);
    if (!scManager)
        return L"Unknown";

    auto scmCloser = std::unique_ptr<SC_HANDLE__, decltype(&::CloseServiceHandle)>(scManager, ::CloseServiceHandle);

    SC_HANDLE service = ::OpenServiceW(scManager, serviceName.c_str(), SERVICE_QUERY_STATUS);
    if (!service)
        return L"Unknown";

    auto svcCloser = std::unique_ptr<SC_HANDLE__, decltype(&::CloseServiceHandle)>(service, ::CloseServiceHandle);

    SERVICE_STATUS_PROCESS ssp{};
    DWORD bytesNeeded = 0;
    if (!::QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO,
        reinterpret_cast<LPBYTE>(&ssp), sizeof(ssp), &bytesNeeded))
        return L"Unknown";

    switch (ssp.dwCurrentState) {
    case SERVICE_RUNNING:       return L"Running";
    case SERVICE_STOPPED:       return L"Stopped";
    case SERVICE_PAUSED:        return L"Paused";
    case SERVICE_START_PENDING: return L"StartPending";
    case SERVICE_STOP_PENDING:  return L"StopPending";
    default:                    return L"Unknown";
    }
}

std::wstring ResourceCollector::GetServiceExecutablePath(const std::wstring& serviceName)
{
    SC_HANDLE scManager = ::OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT);
    if (!scManager)
        return {};

    auto scmCloser = std::unique_ptr<SC_HANDLE__, decltype(&::CloseServiceHandle)>(scManager, ::CloseServiceHandle);

    SC_HANDLE service = ::OpenServiceW(scManager, serviceName.c_str(), SERVICE_QUERY_CONFIG);
    if (!service)
        return {};

    auto svcCloser = std::unique_ptr<SC_HANDLE__, decltype(&::CloseServiceHandle)>(service, ::CloseServiceHandle);

    DWORD bytesNeeded = 0;
    ::QueryServiceConfigW(service, nullptr, 0, &bytesNeeded);

    auto buffer = std::make_unique<BYTE[]>(bytesNeeded);
    auto config = reinterpret_cast<QUERY_SERVICE_CONFIGW*>(buffer.get());

    if (!::QueryServiceConfigW(service, config, bytesNeeded, &bytesNeeded))
        return {};

    return config->lpBinaryPathName ? config->lpBinaryPathName : L"";
}

} // namespace smc
