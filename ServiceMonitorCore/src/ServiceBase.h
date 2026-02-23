#pragma once

#include <string>
#include <functional>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace smc {

/// Base class for implementing a Windows Service using RAII patterns.
class ServiceBase {
public:
    ServiceBase(const std::wstring& serviceName);
    virtual ~ServiceBase() = default;

    ServiceBase(const ServiceBase&) = delete;
    ServiceBase& operator=(const ServiceBase&) = delete;

    /// Registers and runs the service. Call from main().
    static bool Run(ServiceBase& service);

    /// Request the service to stop.
    void Stop();

    const std::wstring& GetName() const { return serviceName_; }

protected:
    virtual void OnStart(DWORD argc, LPWSTR* argv) = 0;
    virtual void OnStop() = 0;
    virtual void OnPause() {}
    virtual void OnContinue() {}

private:
    static void WINAPI ServiceMain(DWORD argc, LPWSTR* argv);
    static void WINAPI ServiceCtrlHandler(DWORD ctrl);

    void SetServiceStatus(DWORD currentState, DWORD exitCode = NO_ERROR, DWORD waitHint = 0);

    static ServiceBase* instance_;

    std::wstring serviceName_;
    SERVICE_STATUS serviceStatus_{};
    SERVICE_STATUS_HANDLE statusHandle_ = nullptr;
};

} // namespace smc
