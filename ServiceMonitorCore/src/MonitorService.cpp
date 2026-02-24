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

/// Enumerate all running Win32 services and return their names.
std::vector<std::wstring> GetRunningServiceNames()
{
    std::vector<std::wstring> names;
    SC_HANDLE hScm = ::OpenSCManagerW(nullptr, nullptr, SC_MANAGER_ENUMERATE_SERVICE);
    if (!hScm) return names;

    DWORD bytesNeeded = 0, serviceCount = 0, resumeHandle = 0;
    ::EnumServicesStatusExW(hScm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_ACTIVE,
        nullptr, 0, &bytesNeeded, &serviceCount, &resumeHandle, nullptr);

    std::vector<BYTE> buffer(bytesNeeded);
    if (::EnumServicesStatusExW(hScm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_ACTIVE,
        buffer.data(), static_cast<DWORD>(buffer.size()), &bytesNeeded, &serviceCount, &resumeHandle, nullptr))
    {
        auto* entries = reinterpret_cast<ENUM_SERVICE_STATUS_PROCESSW*>(buffer.data());
        for (DWORD i = 0; i < serviceCount; ++i) {
            names.emplace_back(entries[i].lpServiceName);
        }
    }
    ::CloseServiceHandle(hScm);
    return names;
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

    running_ = true;
    monitorThread_ = std::thread([this]() { MonitorLoop(); });

    Logger::Info(L"MonitorService started");
}

void MonitorService::OnStop()
{
    Logger::Info(L"MonitorService stopping");
    running_ = false;
    if (monitorThread_.joinable())
        monitorThread_.join();
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

    running_ = true;
    monitorThread_ = std::thread([this]() { MonitorLoop(); });

    Logger::Info(L"MonitorService started (console mode)");
}

void MonitorService::StopConsole()
{
    Logger::Info(L"MonitorService stopping (console mode)");
    running_ = false;
    if (monitorThread_.joinable())
        monitorThread_.join();
    pipeServer_.Stop();
    Logger::Info(L"MonitorService stopped (console mode)");
}

void MonitorService::MonitorLoop()
{
    while (running_) {
        CollectAllMetrics();
        // Sleep in small increments to allow quick shutdown
        for (int i = 0; i < monitoringIntervalMs_.load() / 100 && running_; ++i) {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
    }
}

void MonitorService::CollectAllMetrics()
{
    auto names = GetRunningServiceNames();

    // Deduplicate PIDs — multiple services may share the same svchost.exe process.
    // Collect metrics once per PID, then distribute to all services sharing that PID.
    std::unordered_map<DWORD, ServiceDataPoint> pidMetrics;
    std::vector<std::pair<std::string, DWORD>> servicesPids;

    for (const auto& wName : names) {
        auto pid = ResourceCollector::GetServiceProcessId(wName);
        if (pid == 0) continue;
        servicesPids.emplace_back(WideToUtf8(wName), pid);

        if (pidMetrics.find(pid) == pidMetrics.end()) {
            auto metrics = collector_.Collect(pid);
            pidMetrics[pid] = { metrics.cpuPercent, metrics.memoryMB };
        }
    }

    std::lock_guard lock(historyMutex_);
    for (const auto& [key, pid] : servicesPids) {
        const auto& dp = pidMetrics[pid];
        auto& dq = history_[key];
        dq.push_back(dp);
        while (dq.size() > MaxHistory) {
            dq.pop_front();
        }
    }
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
        else if (command == "GET_ALL_STATUS") {
            std::lock_guard lock(historyMutex_);
            json services = json::array();
            for (const auto& [name, dq] : history_) {
                if (dq.empty()) continue;
                json svc;
                svc["name"] = name;
                svc["cpu"] = dq.back().cpuPercent;
                svc["memoryMB"] = dq.back().memoryMB;
                services.push_back(svc);
            }
            resp["status"] = "OK";
            resp["services"] = services;
        }
        else if (command == "GET_HISTORY") {
            std::lock_guard lock(historyMutex_);
            json services = json::array();
            for (const auto& [name, dq] : history_) {
                json svc;
                svc["name"] = name;
                json cpuArr = json::array();
                json memArr = json::array();
                for (const auto& dp : dq) {
                    cpuArr.push_back(dp.cpuPercent);
                    memArr.push_back(dp.memoryMB);
                }
                svc["cpu"] = cpuArr;
                svc["memoryMB"] = memArr;
                services.push_back(svc);
            }
            resp["status"] = "OK";
            resp["services"] = services;
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
        else if (command == "GET_ALL_STATUS") {
            // Bundled JSON doesn't support arrays easily; return simple OK
            resp.set("status", std::string("OK"));
        }
        else if (command == "GET_HISTORY") {
            resp.set("status", std::string("OK"));
        }
        else if (command == "SET_INTERVAL") {
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
