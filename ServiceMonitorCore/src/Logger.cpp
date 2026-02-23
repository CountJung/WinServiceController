#include "Logger.h"

namespace smc {

void Logger::Init(const std::filesystem::path& logDir, size_t maxFileSizeBytes)
{
    std::lock_guard lock(mutex_);
    maxSize_ = maxFileSizeBytes;

    if (!std::filesystem::exists(logDir))
        std::filesystem::create_directories(logDir);

    logPath_ = logDir / L"ServiceMonitorCore.log";
    file_.open(logPath_, std::ios::app);
    initialized_ = true;

    WriteUnlocked(L"INFO", L"Logger initialized");
}

void Logger::Info(const std::wstring& message)
{
    Write(L"INFO", message);
}

void Logger::Error(const std::wstring& message)
{
    Write(L"ERROR", message);
}

void Logger::Shutdown()
{
    std::lock_guard lock(mutex_);
    if (file_.is_open()) {
        file_.flush();
        file_.close();
    }
    initialized_ = false;
}

void Logger::Write(const std::wstring& level, const std::wstring& message)
{
    std::lock_guard lock(mutex_);
    WriteUnlocked(level, message);
}

void Logger::WriteUnlocked(const std::wstring& level, const std::wstring& message)
{
    if (!initialized_ || !file_.is_open())
        return;

    RotateIfNeeded();

    auto now = std::chrono::system_clock::now();
    auto time = std::chrono::system_clock::to_time_t(now);
    std::tm tm{};
    localtime_s(&tm, &time);

    wchar_t timeBuf[32];
    wcsftime(timeBuf, sizeof(timeBuf) / sizeof(wchar_t), L"%Y-%m-%d %H:%M:%S", &tm);

    file_ << L"[" << timeBuf << L"] [" << level << L"] " << message << std::endl;
}

void Logger::RotateIfNeeded()
{
    if (!std::filesystem::exists(logPath_))
        return;

    auto size = std::filesystem::file_size(logPath_);
    if (size >= maxSize_) {
        file_.close();

        auto rotatedPath = logPath_;
        rotatedPath.replace_extension(L".old.log");

        std::filesystem::remove(rotatedPath);
        std::filesystem::rename(logPath_, rotatedPath);

        file_.open(logPath_, std::ios::app);
    }
}

} // namespace smc
