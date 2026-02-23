cmake_minimum_required(VERSION 3.20)
project(ServiceMonitorCore LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# vcpkg integration (optional, for nlohmann-json)
if(DEFINED CMAKE_TOOLCHAIN_FILE)
    message(STATUS "Using vcpkg toolchain: ${CMAKE_TOOLCHAIN_FILE}")
endif()

find_package(nlohmann_json CONFIG QUIET)

add_executable(${PROJECT_NAME} WIN32
    src/main.cpp
    src/ServiceBase.cpp
    src/MonitorService.cpp
    src/PipeServer.cpp
    src/ResourceCollector.cpp
    src/Logger.cpp
)

target_include_directories(${PROJECT_NAME} PRIVATE src)

if(nlohmann_json_FOUND)
    target_link_libraries(${PROJECT_NAME} PRIVATE nlohmann_json::nlohmann_json)
else()
    message(STATUS "nlohmann_json not found via vcpkg, using bundled header")
    target_compile_definitions(${PROJECT_NAME} PRIVATE USE_BUNDLED_JSON)
endif()

target_link_libraries(${PROJECT_NAME} PRIVATE
    advapi32
    pdh
    psapi
)

# Install
install(TARGETS ${PROJECT_NAME} RUNTIME DESTINATION bin)
