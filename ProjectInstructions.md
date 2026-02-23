# Windows Service Monitor (WPF + Modern C++ Core) — Copilot Instruction Guide

## 0. Project Overview

This project is a Windows Service monitoring application with:

* Modern C++ backend (Windows Service)
* WPF UI (WPF UI + MVVM Toolkit)
* IPC via Named Pipe
* Focus: lightweight, always-running monitoring
* Target: modern UI + high stability + low overhead

Primary Goal:
Monitor running Windows services (CPU, Memory, Runtime, File Access Path) and control the service lifecycle via a modern UI.

---

# 1. Tech Stack (Strict)

## 1.1 UI Layer (Frontend)

* .NET 10 WPF
* WPF UI 4.x (Fluent modern UI)
* CommunityToolkit.Mvvm (MVVM Toolkit)
* Serilog (lightweight file logging)
* System.ServiceProcess.ServiceController (service enumeration)
* LiveCharts2 (for optional charts)

## 1.2 Core Monitoring Engine

* Language: Modern C++ (C++20)
* Type: Windows Service (Win32 Service)
* IPC: Named Pipe (asynchronous)
* Build: CMake or MSBuild (preferred: CMake + vcpkg)

## 1.3 Architecture Pattern

* Clean Architecture (Simplified)
* MVVM (UI)
* Service-Oriented Core
* IPC Bridge Layer

---

# 2. High-Level Architecture

```
[ WPF UI (Admin) ]
        │
        │ Named Pipe (Async IPC)
        ▼
[ C++ Windows Service ]
        │
        ├── Service Monitor Engine
        ├── Resource Collector (CPU/MEM)
        ├── Runtime Tracker
        └── File Access Tracker (Optional)
```

Design Principles:

* UI must NEVER directly monitor system resources
* All monitoring logic lives inside the C++ service
* UI acts as Controller + Visualizer only
* IPC must be non-blocking and resilient

---

# 3. Core Functional Requirements

## 3.1 Service Monitoring Features

The C++ service must monitor:

* CPU usage per target service
* Memory usage (Working Set / Private Bytes)
* Service running time (uptime)
* Executable path & file access base path (lightweight level)
* Service status (Running / Stopped / Paused)

Performance Constraint:
Monitoring loop must be lightweight (< 1% CPU overhead)

---

## 3.2 Service Lifecycle Control (From UI)

The WPF UI must support:

* Install Service
* Uninstall Service
* Start Service
* Stop Service
* Restart Service
* Auto-restart on crash (configurable)

Use:

* `sc.exe` OR Win32 Service API (CreateService, StartService, etc.)

---

# 4. IPC Specification (Named Pipe — Mandatory)

## 4.1 Pipe Design

* Pipe Name: `\\\\.\\pipe\\ServiceMonitorPipe`
* Mode: Duplex (Two-way communication)
* Async I/O required
* JSON message protocol (UTF-8)

Example Message Format:

```json
{
  "command": "GET_STATUS",
  "targetService": "Spooler"
}
```

Response:

```json
{
  "status": "Running",
  "cpu": 1.25,
  "memoryMB": 52,
  "uptimeSeconds": 10452
}
```

---

# 5. C++ Service Implementation Rules

## 5.1 Mandatory Requirements

* Use C++20
* Use RAII for all handles
* No global mutable state
* Thread-safe monitoring loop
* Structured logging (file-based, lightweight)

## 5.2 Windows APIs to Prefer

* OpenSCManager / EnumServicesStatusEx
* QueryServiceStatusEx
* GetProcessTimes
* GetProcessMemoryInfo
* PDH (for accurate CPU metrics)

Avoid:

* Heavy external dependencies
* Polling with extremely short intervals (< 500ms)

Recommended Monitoring Interval:

* Default: 1 second
* Configurable via IPC

---

# 6. WPF UI Implementation Rules

## 6.1 MVVM Structure (Strict)

```
/View
/ViewModels
/Models
/Services (IPC Client)
/Core (Shared Contracts)
```

Use:

* ObservableObject (MVVM Toolkit)
* RelayCommand for all UI actions
* Dependency Injection (Microsoft.Extensions.Hosting)

---

## 6.2 UI/UX Requirements (Modern)

UI must:

* Use WPF UI Fluent design system
* Support Light/Dark Theme toggle
* Provide:

  * Dashboard View
  * Service List View
  * Detail Panel (CPU/MEM/Uptime)
  * Control Buttons (Start/Stop/Restart)

Do NOT use:

* WinForms components
* Legacy WPF styling
* Blocking UI thread calls

---

# 7. Chart System (Optional but Prepared)

Preferred:

* LiveCharts2 (free, modern, MVVM friendly)

Chart Scope:

* CPU usage over time
* Memory usage trend

Chart must:

* Support theme sync with WPF UI
* Use ObservableCollection binding
* Limit data points (max 300) for performance

---

# 8. Administrator Privilege Strategy (Critical)

Because this app controls Windows Services:

Requirements:

* UI must auto-request Administrator privileges
* Use application manifest:

```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

Service Installation must:

* Detect elevation
* Prompt UAC if not elevated
* Fail gracefully if permission denied

---

# 9. Logging Strategy (Lightweight)

## 9.1 C++ Service

* Rolling log file
* Max 5MB per file
* Log only:

  * Errors
  * Service state changes
  * IPC failures

## 9.2 WPF UI

* Serilog (File sink)
* Debug + Error logs only

Avoid verbose logging to reduce overhead.

---

# 10. Performance Constraints (Very Important)

* Service RAM usage target: < 50MB
* Idle CPU usage: < 0.5%
* IPC response time: < 50ms
* Must not block system services or cause deadlocks

---

# 11. Security Considerations

* Named Pipe must use proper security descriptor
* Only allow local machine connections
* Validate all IPC JSON input (no trust)

---

# 12. Development Workflow (Copilot Guidance)

When generating code:

* Always separate UI and Core logic
* Never mix monitoring logic inside WPF
* Generate async IPC client/server
* Prefer modern C++ patterns over legacy Win32 style
* Use smart pointers (unique_ptr / shared_ptr)
* Ensure exception safety in service loop

---

# 13. Out of Scope (Do NOT Implement)

* Cross-platform support
* Heavy telemetry systems
* Cloud sync
* Kernel-level monitoring
* Complex database storage (use in-memory + optional JSON cache only)

---

# 14. Final Development Priority Order

1. C++ Windows Service (Core Engine) ✅ Skeleton implemented
2. Named Pipe IPC Layer ✅ Server (C++) and Client (C#) implemented
3. WPF UI Dashboard ✅ Summary cards + engine status
4. Service Control Panel ✅ ServiceListPage with Start/Stop/Restart
5. Optional Charts (LiveCharts2)
6. Optimization & Hardening

END OF INSTRUCTION

---

# 15. Implementation Status & Maintenance Notes

## 15.1 Completed (Phase 1)

### WPF UI
* **Removed**: DataPage, DataViewModel, DataColor (template demo artifacts)
* **Removed**: Empty Translations.cs
* **Theme**: Added System Theme mode (`ApplicationTheme.Unknown` = follow OS)
  - `SystemThemeWatcher.Watch/UnWatch` toggled in SettingsViewModel
  - Three options: System / Light / Dark
* **Dashboard**: Summary cards (Total/Running/Stopped services) + Engine status
* **ServiceListPage**: Service list with search, Start/Stop/Restart controls
  - Uses `System.ServiceProcess.ServiceController` for enumeration
  - CPU/Memory columns prepared (populated via IPC when C++ service runs)
* **Admin Manifest**: `requireAdministrator` enabled
* **Serilog**: File sink in `{AppDir}/logs/`, rolling daily, 7-day retention, 5MB limit
* **DI**: IPipeClientService registered as singleton

### C++ Service (ServiceMonitorCore)
* **CMake project** with vcpkg support and CMakePresets
* **ServiceBase**: RAII-based Win32 Service framework
* **MonitorService**: Handles IPC commands (`GET_STATUS`, `SET_INTERVAL`, `PING`)
* **PipeServer**: Async duplex Named Pipe with overlapped I/O, `PIPE_REJECT_REMOTE_CLIENTS`
* **ResourceCollector**: CPU (via GetProcessTimes), Memory (WorkingSetSize), Uptime, Service status/PID/exe path
* **Logger**: Rolling file logger with 5MB size limit
* **JsonProtocol**: nlohmann-json integration with bundled fallback parser

## 15.2 Completed (Phase 2 — Verification & Integration)

### C++ Build & Testing
* **CMakePresets**: Changed generator from Ninja to `Visual Studio 18 2026` with x64 architecture
* **Logger deadlock fix**: `Init()` called `Write()` which re-acquired the same non-recursive mutex → extracted `WriteUnlocked()` for internal use
* **wchar_t→char conversion fix**: Replaced unsafe iterator-based `std::string(wstr.begin(), wstr.end())` with proper `WideCharToMultiByte`/`MultiByteToWideChar` helpers
* **Console mode**: `main.cpp` supports `--console` flag for development/testing without service registration. Falls back to console mode automatically if `StartServiceCtrlDispatcher` fails.
* **MonitorService**: Added `RunConsole()`/`StopConsole()` public methods to reuse the same handler logic in console mode
* **Build verified**: `cmake --preset default && cmake --build build --preset debug` — 0 errors, 0 code warnings

### IPC End-to-End Verification
* **PING → PONG**: ✅ Confirmed via `NamedPipeClientStream` from PowerShell
* **GET_STATUS → Response**: ✅ Returns correct service status (`Running`/`Stopped`). CPU/Memory/Uptime return 0 when run without admin privileges (expected — `OpenProcess` fails for SYSTEM processes)
* **Message-mode pipe**: Both C++ server (`PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE`) and C# client (`pipe.ReadMode = PipeTransmissionMode.Message`) aligned

### WPF IPC Client Integration
* **PipeClientService**: Added `PipeTransmissionMode.Message` after connect. Replaced `lock` with `SemaphoreSlim` for async-safe concurrency
* **DashboardViewModel**: Added `DispatcherTimer` (3s interval) for auto-refresh. `RefreshAsyncCommand` pings C++ engine to update connection status
* **ServiceListViewModel**: `RefreshServicesAsyncCommand` queries C++ engine for CPU/Memory per running service via `GET_STATUS` IPC. All service lifecycle commands now properly `await RefreshServicesAsync()`

### How to Run (Development)
```bash
# 1. Build C++ engine
cd ServiceMonitorCore
cmake --preset default
cmake --build build --preset debug

# 2. Start engine in console mode (keeps pipe server alive)
./build/Debug/ServiceMonitorCore.exe --console

# 3. Run WPF app (separate terminal, admin required)
cd ../WinServiceController
dotnet run

# 4. Or register as Windows Service (admin required)
sc create ServiceMonitorCore binPath= "C:\full\path\to\ServiceMonitorCore.exe"
sc start ServiceMonitorCore
```

## 15.2 Architecture

```
WinServiceController/               (Solution root)
├── WinServiceController.slnx
├── ProjectInstructions.md
├── WinServiceController/            (.NET 10 WPF project)
│   ├── Views/
│   │   ├── Windows/MainWindow.xaml  (FluentWindow + NavigationView)
│   │   └── Pages/
│   │       ├── DashboardPage.xaml   (Service overview cards)
│   │       ├── ServiceListPage.xaml (Service list + controls)
│   │       └── SettingsPage.xaml    (Theme: System/Light/Dark)
│   ├── ViewModels/
│   │   ├── Windows/MainWindowViewModel.cs
│   │   └── Pages/
│   │       ├── DashboardViewModel.cs
│   │       ├── ServiceListViewModel.cs
│   │       └── SettingsViewModel.cs
│   ├── Models/
│   │   ├── ServiceInfo.cs           (ObservableObject)
│   │   ├── IpcMessage.cs            (IpcRequest/IpcResponse DTOs)
│   │   └── AppConfig.cs             (PipeName, MonitoringIntervalMs)
│   ├── Services/
│   │   ├── IPipeClientService.cs    (IPC client interface)
│   │   ├── PipeClientService.cs     (NamedPipeClientStream implementation)
│   │   └── ApplicationHostService.cs
│   └── Helpers/
│       └── EnumToBooleanConverter.cs
└── ServiceMonitorCore/              (C++20 CMake project)
    ├── CMakeLists.txt
    ├── CMakePresets.json
    ├── vcpkg.json
    └── src/
        ├── main.cpp                 (wWinMain entry point)
        ├── ServiceBase.h/.cpp       (Win32 Service RAII framework)
        ├── MonitorService.h/.cpp    (IPC command handler)
        ├── PipeServer.h/.cpp        (Async duplex Named Pipe server)
        ├── ResourceCollector.h/.cpp (CPU/Memory/Uptime collection)
        ├── JsonProtocol.h           (nlohmann-json or bundled fallback)
        └── Logger.h/.cpp            (Rolling file logger)
```

## 15.3 Key Design Decisions

1. **System Theme**: Uses `ApplicationTheme.Unknown` enum value to represent "follow OS" mode.
   The `EnumToBooleanConverter` maps `ConverterParameter=Unknown` to the System radio button.
2. **IPC Protocol**: JSON over Named Pipe (`\\.\pipe\ServiceMonitorPipe`), message-mode.
   Commands: `GET_STATUS`, `SET_INTERVAL`, `PING`.
3. **Service Control**: WPF uses `System.ServiceProcess.ServiceController` directly for lifecycle ops.
   Resource monitoring (CPU/Memory) is delegated to C++ service via IPC.
4. **C++ JSON**: Prefers nlohmann-json via vcpkg; falls back to a minimal bundled parser if unavailable.
5. **Pipe Security**: `PIPE_REJECT_REMOTE_CLIENTS` flag blocks remote connections.

## 15.4 Next Steps

- [x] Build & test C++ service — Visual Studio 2026 generator, 0 errors
- [x] Console mode for dev testing (`--console` flag + auto-fallback)
- [x] IPC end-to-end verified (PING, GET_STATUS)
- [x] Connect WPF IPC client to live service (message-mode pipe)
- [x] Add real-time polling in Dashboard (DispatcherTimer, 3s)
- [x] ServiceListPage queries C++ engine for per-service CPU/Memory
- [ ] LiveCharts2 integration for CPU/Memory trend graphs
- [ ] Service install/uninstall commands from UI
- [ ] Auto-restart on crash configuration
- [ ] Error/notification snackbar in UI
- [ ] Performance testing & hardening
