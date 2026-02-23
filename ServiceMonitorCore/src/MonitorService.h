#pragma once

#include "ServiceBase.h"
#include "PipeServer.h"
#include "ResourceCollector.h"

#include <thread>
#include <atomic>
#include <mutex>
#include <string>
#include <chrono>

namespace smc {

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

    PipeServer pipeServer_;
    ResourceCollector collector_;
    std::atomic<int> monitoringIntervalMs_{ 1000 };
};

} // namespace smc
