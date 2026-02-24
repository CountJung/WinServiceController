#pragma once

#include "ServiceBase.h"
#include "PipeServer.h"
#include "ResourceCollector.h"

#include <thread>
#include <atomic>
#include <mutex>
#include <string>
#include <chrono>
#include <vector>
#include <deque>
#include <unordered_map>

namespace smc {

/// A snapshot of one service at one point in time.
struct ServiceDataPoint {
    double cpuPercent = 0.0;
    double memoryMB = 0.0;
};

/// The main monitoring service. Inherits from ServiceBase for Windows Service lifecycle.
class MonitorService : public ServiceBase {
public:
    MonitorService();
    ~MonitorService() override = default;

    /// Run in console mode (no Windows Service registration).
    void RunConsole();

    /// Stop console mode or service.
    void StopConsole();

protected:
    void OnStart(DWORD argc, LPWSTR* argv) override;
    void OnStop() override;

private:
    /// Handles incoming IPC JSON requests.
    std::string HandleRequest(const std::string& requestJson);

    /// Background monitoring loop that populates the history ring buffer.
    void MonitorLoop();

    /// Enumerate all running Win32 services and collect metrics.
    void CollectAllMetrics();

    PipeServer pipeServer_;
    ResourceCollector collector_;
    std::atomic<int> monitoringIntervalMs_{ 1000 };
    std::atomic<bool> running_{ false };
    std::thread monitorThread_;

    // History ring buffer: service name -> deque of data points (max 7200 = 2hr @ 1s)
    static constexpr size_t MaxHistory = 7200;
    std::mutex historyMutex_;
    std::unordered_map<std::string, std::deque<ServiceDataPoint>> history_;
};

} // namespace smc
