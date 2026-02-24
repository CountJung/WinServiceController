# Windows Service Monitor

A modern Windows Service monitoring application combining a **C++20 backend service** with a **WPF Fluent UI** frontend. Monitor, control, and visualize Windows service resource usage in real time.

## Features

- **Real-time Service Monitoring** — CPU, Memory, Uptime per service via a lightweight C++ engine
- **Service Lifecycle Control** — Start, Stop, Restart, Install, Uninstall services directly from the UI
- **Live Charts** — CPU and Memory trend graphs powered by LiveCharts2 (2-hour sliding window)
- **Fluent Modern UI** — WPF UI 4.x with Light/Dark/System theme support
- **Named Pipe IPC** — Async duplex JSON messaging between UI and monitoring engine
- **System Tray Integration** — Minimize to tray on close with configurable notifications
- **Auto-Start** — Register with Windows startup via Settings
- **Administrator Privilege** — Auto-requests elevation for service control operations
- **Persistent Settings** — Theme, tray behavior, engine path saved to JSON

## Architecture

```
[ WPF UI (.NET 10) ]
        │
        │  Named Pipe (Async JSON IPC)
        ▼
[ C++ Windows Service (C++20) ]
        │
        ├── Service Monitor Engine
        ├── Resource Collector (CPU / Memory / Uptime)
        ├── Pipe Server (Overlapped I/O)
        └── Rolling File Logger
```

## Tech Stack

| Layer    | Technology |
|----------|-----------|
| UI       | .NET 10 WPF, WPF UI 4.x, CommunityToolkit.Mvvm |
| Charts   | LiveCharts2 (SkiaSharp) |
| Logging  | Serilog (UI), Custom rolling logger (C++) |
| IPC      | Named Pipe — `\\.\pipe\ServiceMonitorPipe` |
| Backend  | Modern C++20, Win32 Service API, PDH |
| Build    | CMake + Visual Studio 2026 (C++), .NET CLI (WPF) |

## Project Structure

```
WinServiceController/
├── WinServiceController.slnx        Solution file
├── README.md                         This file
├── ProjectInstructions.md            Development guide
│
├── WinServiceController/             .NET 10 WPF project
│   ├── Views/Pages/
│   │   ├── DashboardPage.xaml        Service summary + engine control
│   │   ├── ServiceListPage.xaml      Service list with search & controls
│   │   ├── ChartPage.xaml            CPU/Memory trend charts
│   │   └── SettingsPage.xaml         Theme, tray, startup, engine path
│   ├── ViewModels/Pages/             MVVM ViewModels
│   ├── Models/                       ServiceInfo, IpcMessage, UserSettings
│   ├── Services/                     PipeClient, UserSettings, AppHost
│   └── Helpers/                      Converters
│
└── ServiceMonitorCore/               C++20 CMake project
    └── src/
        ├── main.cpp                  Entry point (--console flag)
        ├── MonitorService.h/.cpp     IPC command handler
        ├── PipeServer.h/.cpp         Async Named Pipe server
        ├── ResourceCollector.h/.cpp  CPU/Memory/Uptime collector
        └── Logger.h/.cpp             Rolling file logger
```

## Quick Start

### 1. Build C++ Monitoring Engine

```bash
cd ServiceMonitorCore
cmake --preset default
cmake --build build --preset debug
```

### 2. Run Engine in Console Mode (Development)

```bash
./build/Debug/ServiceMonitorCore.exe --console
```

### 3. Run WPF Application

```bash
cd WinServiceController
dotnet run
```

> **Note**: The WPF app requires Administrator privileges to control services.

### 4. Or Install as Windows Service

```bash
sc create ServiceMonitorCore binPath= "C:\full\path\to\ServiceMonitorCore.exe"
sc start ServiceMonitorCore
```

You can also Install/Start/Stop/Uninstall the engine directly from the Dashboard.

## IPC Protocol

JSON over Named Pipe (`\\.\pipe\ServiceMonitorPipe`), message-mode.

**Request:**
```json
{ "command": "GET_STATUS", "targetService": "Spooler" }
```

**Response:**
```json
{ "status": "Running", "cpu": 1.25, "memoryMB": 52, "uptimeSeconds": 10452 }
```

Commands: `PING`, `GET_STATUS`, `SET_INTERVAL`

## Settings

All settings are persisted in `settings.json` next to the executable:

| Setting | Description |
|---------|------------|
| Theme | System / Light / Dark |
| Minimize to Tray | Hide to tray on window close |
| Suppress Tray Notification | Don't show "minimized to tray" balloon |
| Start with Windows | Auto-launch on login |
| Engine Path | Path to ServiceMonitorCore.exe |

## Requirements

- Windows 10/11
- .NET 10 SDK
- Visual Studio 2022/2026 with C++ workload (for C++ engine)
- Administrator privileges

## License

Private project — all rights reserved.

---

# Windows Service Monitor (한국어)

C++20 백엔드 서비스와 WPF Fluent UI 프론트엔드를 결합한 윈도우 서비스 모니터링 애플리케이션입니다.

## 주요 기능

- **실시간 서비스 모니터링** — 경량 C++ 엔진을 통한 서비스별 CPU, 메모리, 가동 시간 감시
- **서비스 생명주기 제어** — UI에서 직접 시작, 중지, 재시작, 설치, 제거
- **실시간 차트** — LiveCharts2 기반 CPU/메모리 트렌드 그래프 (기본 2시간 슬라이딩 윈도우)
- **Fluent 모던 UI** — WPF UI 4.x, 라이트/다크/시스템 테마 지원
- **Named Pipe IPC** — UI와 모니터링 엔진 간 비동기 양방향 JSON 메시징
- **시스템 트레이 통합** — 닫기 시 트레이로 최소화, 알림 설정 가능
- **자동 시작** — 설정에서 윈도우 시작 시 자동 실행 등록
- **관리자 권한** — 서비스 제어를 위한 자동 권한 상승
- **설정 영속화** — 테마, 트레이 동작, 엔진 경로를 JSON 파일로 저장

## 아키텍처

```
[ WPF UI (.NET 10) ]
        │
        │  Named Pipe (비동기 JSON IPC)
        ▼
[ C++ Windows Service (C++20) ]
        │
        ├── 서비스 모니터 엔진
        ├── 리소스 수집기 (CPU / 메모리 / 가동시간)
        ├── 파이프 서버 (Overlapped I/O)
        └── 롤링 파일 로거
```

## 기술 스택

| 레이어 | 기술 |
|--------|------|
| UI | .NET 10 WPF, WPF UI 4.x, CommunityToolkit.Mvvm |
| 차트 | LiveCharts2 (SkiaSharp) |
| 로깅 | Serilog (UI), 커스텀 롤링 로거 (C++) |
| IPC | Named Pipe — `\\.\pipe\ServiceMonitorPipe` |
| 백엔드 | Modern C++20, Win32 Service API, PDH |
| 빌드 | CMake + Visual Studio 2026 (C++), .NET CLI (WPF) |

## 빠른 시작

### 1. C++ 모니터링 엔진 빌드

```bash
cd ServiceMonitorCore
cmake --preset default
cmake --build build --preset debug
```

### 2. 콘솔 모드 실행 (개발용)

```bash
./build/Debug/ServiceMonitorCore.exe --console
```

### 3. WPF 애플리케이션 실행

```bash
cd WinServiceController
dotnet run
```

> **참고**: WPF 앱은 서비스 제어를 위해 관리자 권한이 필요합니다.

### 4. 또는 Windows 서비스로 설치

```bash
sc create ServiceMonitorCore binPath= "C:\전체\경로\ServiceMonitorCore.exe"
sc start ServiceMonitorCore
```

대시보드에서 직접 엔진 설치/시작/중지/제거가 가능합니다.

## IPC 프로토콜

Named Pipe (`\\.\pipe\ServiceMonitorPipe`)를 통한 JSON 메시지 모드.

**요청:**
```json
{ "command": "GET_STATUS", "targetService": "Spooler" }
```

**응답:**
```json
{ "status": "Running", "cpu": 1.25, "memoryMB": 52, "uptimeSeconds": 10452 }
```

명령어: `PING`, `GET_STATUS`, `SET_INTERVAL`

## 설정

모든 설정은 실행 파일 옆의 `settings.json`에 영속화됩니다:

| 설정 | 설명 |
|------|------|
| Theme | 시스템 / 라이트 / 다크 |
| Minimize to Tray | 창 닫기 시 트레이로 숨기기 |
| Suppress Tray Notification | "트레이로 최소화" 알림 표시 안 함 |
| Start with Windows | 로그인 시 자동 실행 |
| Engine Path | ServiceMonitorCore.exe 경로 |

## 요구사항

- Windows 10/11
- .NET 10 SDK
- Visual Studio 2022/2026 + C++ 워크로드 (C++ 엔진 빌드 시)
- 관리자 권한

## 라이선스

비공개 프로젝트 — 모든 권리 보유.