#include "MonitorService.h"
#include "JsonProtocol.h"
#include "Logger.h"

#include <sstream>

namespace smc {

namespace {

std::string WideToUtf8(const std::wstring& wide)
{
    if (wide.empty())
        return {};
    int size = ::WideCharToMultiByte(CP_UTF8, 0, wide.data(), static_cast<int>(wide.size()), nullptr, 0, nullptr, nullptr);
    std::string result(size, '\0');
    ::WideCharToMultiByte(CP_UTF8, 0, wide.data(), static_cast<int>(wide.size()), result.data(), size, nullptr, nullptr);
    return result;
}

std::wstring Utf8ToWide(const std::string& utf8)
{
    if (utf8.empty())
        return {};
    int size = ::MultiByteToWideChar(CP_UTF8, 0, utf8.data(), static_cast<int>(utf8.size()), nullptr, 0);
    std::wstring result(size, L'\0');
    ::MultiByteToWideChar(CP_UTF8, 0, utf8.data(), static_cast<int>(utf8.size()), result.data(), size);
    return result;
}

} // anonymous namespace

MonitorService::MonitorService()
    : ServiceBase(L"ServiceMonitorCore")
{
}

void MonitorService::OnStart(DWORD /*argc*/, LPWSTR* /*argv*/)
{
    Logger::Info(L"MonitorService starting");

    pipeServer_.SetMessageHandler([this](const std::string& req) {
        return HandleRequest(req);
    });
    pipeServer_.Start();

    Logger::Info(L"MonitorService started");
}

void MonitorService::OnStop()
{
    Logger::Info(L"MonitorService stopping");
    pipeServer_.Stop();
    Logger::Info(L"MonitorService stopped");
}

void MonitorService::RunConsole()
{
    Logger::Info(L"MonitorService starting (console mode)");

    pipeServer_.SetMessageHandler([this](const std::string& req) {
        return HandleRequest(req);
    });
    pipeServer_.Start();

    Logger::Info(L"MonitorService started (console mode)");
}

void MonitorService::StopConsole()
{
    Logger::Info(L"MonitorService stopping (console mode)");
    pipeServer_.Stop();
    Logger::Info(L"MonitorService stopped (console mode)");
}

std::string MonitorService::HandleRequest(const std::string& requestJson)
{
#ifndef USE_BUNDLED_JSON
    try {
        auto req = json::parse(requestJson);
        std::string command = req.value("command", "");
        std::string target = req.value("targetService", "");

        json resp;

        if (command == "GET_STATUS") {
            auto wTarget = Utf8ToWide(target);

            auto status = ResourceCollector::GetServiceStatus(wTarget);
            auto pid = ResourceCollector::GetServiceProcessId(wTarget);
            auto metrics = collector_.Collect(pid);

            resp["status"] = WideToUtf8(status);
            resp["cpu"] = metrics.cpuPercent;
            resp["memoryMB"] = metrics.memoryMB;
            resp["uptimeSeconds"] = static_cast<int64_t>(metrics.uptimeSeconds);
            resp["executablePath"] = WideToUtf8(ResourceCollector::GetServiceExecutablePath(wTarget));
        }
        else if (command == "SET_INTERVAL") {
            int interval = req.value("intervalMs", 1000);
            if (interval >= 500) {
                monitoringIntervalMs_ = interval;
                resp["status"] = "OK";
            }
            else {
                resp["error"] = "Interval must be >= 500ms";
            }
        }
        else if (command == "PING") {
            resp["status"] = "PONG";
        }
        else {
            resp["error"] = "Unknown command: " + command;
        }

        return resp.dump();
    }
    catch (const std::exception& ex) {
        json err;
        err["error"] = ex.what();
        return err.dump();
    }
#else
    try {
        auto req = json::parse(requestJson);
        std::string command = req.get_string("command");
        std::string target = req.get_string("targetService");

        json resp;

        if (command == "GET_STATUS") {
            auto wTarget = Utf8ToWide(target);

            auto status = ResourceCollector::GetServiceStatus(wTarget);
            auto pid = ResourceCollector::GetServiceProcessId(wTarget);
            auto metrics = collector_.Collect(pid);

            resp.set("status", WideToUtf8(status));
            resp.set("cpu", metrics.cpuPercent);
            resp.set("memoryMB", metrics.memoryMB);
            resp.set("uptimeSeconds", static_cast<int64_t>(metrics.uptimeSeconds));
        }
        else if (command == "SET_INTERVAL") {
            // Simplified: not supported in bundled mode
            resp.set("status", std::string("OK"));
        }
        else if (command == "PING") {
            resp.set("status", std::string("PONG"));
        }
        else {
            resp.set("error", "Unknown command: " + command);
        }

        return resp.dump();
    }
    catch (const std::exception& ex) {
        json err;
        err.set("error", std::string(ex.what()));
        return err.dump();
    }
#endif
}

} // namespace smc
