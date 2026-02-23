#pragma once

#include <string>
#include <fstream>
#include <mutex>
#include <filesystem>
#include <chrono>
#include <format>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace smc {

/// Rolling file logger with size limit.
/// Logs only Errors, state changes, and IPC failures.
class Logger {
public:
    static void Init(const std::filesystem::path& logDir, size_t maxFileSizeBytes = 5 * 1024 * 1024);
    static void Info(const std::wstring& message);
    static void Error(const std::wstring& message);
    static void Shutdown();

private:
    static void Write(const std::wstring& level, const std::wstring& message);
    static void WriteUnlocked(const std::wstring& level, const std::wstring& message);
    static void RotateIfNeeded();

    static inline std::mutex mutex_;
    static inline std::wofstream file_;
    static inline std::filesystem::path logPath_;
    static inline size_t maxSize_ = 5 * 1024 * 1024;
    static inline bool initialized_ = false;
};

} // namespace smc
