#pragma once

#include <string>
#include <cstdint>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <Pdh.h>
#include <Psapi.h>

namespace smc {

/// Collects CPU and memory metrics for a target service process.
struct ServiceMetrics {
    double cpuPercent = 0.0;
    double memoryMB = 0.0;
    uint64_t uptimeSeconds = 0;
    std::wstring status;
    std::wstring executablePath;
};

class ResourceCollector {
public:
    ResourceCollector();
    ~ResourceCollector();

    ResourceCollector(const ResourceCollector&) = delete;
    ResourceCollector& operator=(const ResourceCollector&) = delete;

    /// Collect metrics for a given process ID.
    ServiceMetrics Collect(DWORD processId);

    /// Get the process ID for a running service by name.
    static DWORD GetServiceProcessId(const std::wstring& serviceName);

    /// Get service status string.
    static std::wstring GetServiceStatus(const std::wstring& serviceName);

    /// Get executable path for a service.
    static std::wstring GetServiceExecutablePath(const std::wstring& serviceName);

private:
    double CalculateCpuUsage(HANDLE hProcess);

    ULARGE_INTEGER lastCpu_{};
    ULARGE_INTEGER lastSysCpu_{};
    ULARGE_INTEGER lastUserCpu_{};
    int numProcessors_ = 1;
};

} // namespace smc
