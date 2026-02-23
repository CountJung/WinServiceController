#include "ServiceBase.h"
#include "Logger.h"
#include <cassert>

namespace smc {

ServiceBase* ServiceBase::instance_ = nullptr;

ServiceBase::ServiceBase(const std::wstring& serviceName)
    : serviceName_(serviceName)
{
    serviceStatus_.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
    serviceStatus_.dwCurrentState = SERVICE_START_PENDING;
    serviceStatus_.dwControlsAccepted = 0;
    serviceStatus_.dwWin32ExitCode = NO_ERROR;
    serviceStatus_.dwServiceSpecificExitCode = 0;
    serviceStatus_.dwCheckPoint = 0;
    serviceStatus_.dwWaitHint = 0;
}

bool ServiceBase::Run(ServiceBase& service)
{
    instance_ = &service;

    std::wstring name = service.serviceName_;
    SERVICE_TABLE_ENTRYW serviceTable[] = {
        { name.data(), ServiceMain },
        { nullptr, nullptr }
    };

    if (!::StartServiceCtrlDispatcherW(serviceTable)) {
        Logger::Error(L"StartServiceCtrlDispatcher failed: " + std::to_wstring(::GetLastError()));
        return false;
    }

    return true;
}

void WINAPI ServiceBase::ServiceMain(DWORD argc, LPWSTR* argv)
{
    assert(instance_ != nullptr);

    instance_->statusHandle_ = ::RegisterServiceCtrlHandlerW(
        instance_->serviceName_.c_str(),
        ServiceCtrlHandler
    );

    if (!instance_->statusHandle_) {
        Logger::Error(L"RegisterServiceCtrlHandler failed");
        return;
    }

    instance_->SetServiceStatus(SERVICE_START_PENDING);

    try {
        instance_->OnStart(argc, argv);
        instance_->SetServiceStatus(SERVICE_RUNNING);
    }
    catch (const std::exception& ex) {
        std::string what = ex.what();
        std::wstring wWhat(what.begin(), what.end());
        Logger::Error(L"OnStart exception: " + wWhat);
        instance_->SetServiceStatus(SERVICE_STOPPED, ERROR_SERVICE_SPECIFIC_ERROR);
    }
}

void WINAPI ServiceBase::ServiceCtrlHandler(DWORD ctrl)
{
    switch (ctrl) {
    case SERVICE_CONTROL_STOP:
        instance_->SetServiceStatus(SERVICE_STOP_PENDING);
        instance_->OnStop();
        instance_->SetServiceStatus(SERVICE_STOPPED);
        break;
    case SERVICE_CONTROL_PAUSE:
        instance_->SetServiceStatus(SERVICE_PAUSE_PENDING);
        instance_->OnPause();
        instance_->SetServiceStatus(SERVICE_PAUSED);
        break;
    case SERVICE_CONTROL_CONTINUE:
        instance_->SetServiceStatus(SERVICE_CONTINUE_PENDING);
        instance_->OnContinue();
        instance_->SetServiceStatus(SERVICE_RUNNING);
        break;
    case SERVICE_CONTROL_INTERROGATE:
        break;
    default:
        break;
    }
}

void ServiceBase::SetServiceStatus(DWORD currentState, DWORD exitCode, DWORD waitHint)
{
    static DWORD checkPoint = 1;

    serviceStatus_.dwCurrentState = currentState;
    serviceStatus_.dwWin32ExitCode = exitCode;
    serviceStatus_.dwWaitHint = waitHint;

    if (currentState == SERVICE_RUNNING || currentState == SERVICE_STOPPED) {
        serviceStatus_.dwControlsAccepted = SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_PAUSE_CONTINUE;
        serviceStatus_.dwCheckPoint = 0;
    }
    else {
        serviceStatus_.dwControlsAccepted = 0;
        serviceStatus_.dwCheckPoint = checkPoint++;
    }

    ::SetServiceStatus(statusHandle_, &serviceStatus_);
}

void ServiceBase::Stop()
{
    SetServiceStatus(SERVICE_STOP_PENDING);
    OnStop();
    SetServiceStatus(SERVICE_STOPPED);
}

} // namespace smc
