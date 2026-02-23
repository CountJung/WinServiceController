#pragma once

// Minimal JSON serialization/deserialization helpers.
// When nlohmann-json is available via vcpkg, prefer using it directly.
// This header provides a lightweight fallback.

#ifndef USE_BUNDLED_JSON
#include <nlohmann/json.hpp>
namespace smc {
    using json = nlohmann::json;
}
#else

#include <string>
#include <map>
#include <sstream>
#include <stdexcept>

namespace smc {

/// A very minimal JSON object for simple key-value string/number pairs.
/// Only supports flat JSON objects. Not a full JSON implementation.
class json {
public:
    json() = default;

    void set(const std::string& key, const std::string& value) { strings_[key] = value; }
    void set(const std::string& key, double value) { numbers_[key] = value; }
    void set(const std::string& key, int64_t value) { integers_[key] = value; }

    std::string get_string(const std::string& key, const std::string& def = "") const {
        auto it = strings_.find(key);
        return it != strings_.end() ? it->second : def;
    }

    double get_number(const std::string& key, double def = 0.0) const {
        auto it = numbers_.find(key);
        return it != numbers_.end() ? it->second : def;
    }

    std::string dump() const {
        std::ostringstream ss;
        ss << "{";
        bool first = true;
        for (auto& [k, v] : strings_) {
            if (!first) ss << ",";
            ss << "\"" << k << "\":\"" << v << "\"";
            first = false;
        }
        for (auto& [k, v] : numbers_) {
            if (!first) ss << ",";
            ss << "\"" << k << "\":" << v;
            first = false;
        }
        for (auto& [k, v] : integers_) {
            if (!first) ss << ",";
            ss << "\"" << k << "\":" << v;
            first = false;
        }
        ss << "}";
        return ss.str();
    }

    static json parse(const std::string& input) {
        json j;
        // Very basic parser: only handles flat {"key":"value","key":number} patterns
        size_t pos = 0;
        while (pos < input.size()) {
            auto keyStart = input.find('"', pos);
            if (keyStart == std::string::npos) break;
            auto keyEnd = input.find('"', keyStart + 1);
            if (keyEnd == std::string::npos) break;

            std::string key = input.substr(keyStart + 1, keyEnd - keyStart - 1);

            auto colon = input.find(':', keyEnd + 1);
            if (colon == std::string::npos) break;

            auto valStart = input.find_first_not_of(" \t\n\r", colon + 1);
            if (valStart == std::string::npos) break;

            if (input[valStart] == '"') {
                auto valEnd = input.find('"', valStart + 1);
                if (valEnd == std::string::npos) break;
                j.strings_[key] = input.substr(valStart + 1, valEnd - valStart - 1);
                pos = valEnd + 1;
            }
            else {
                auto valEnd = input.find_first_of(",}", valStart);
                if (valEnd == std::string::npos) valEnd = input.size();
                std::string numStr = input.substr(valStart, valEnd - valStart);
                if (numStr.find('.') != std::string::npos)
                    j.numbers_[key] = std::stod(numStr);
                else
                    j.integers_[key] = std::stoll(numStr);
                pos = valEnd;
            }
        }
        return j;
    }

private:
    std::map<std::string, std::string> strings_;
    std::map<std::string, double> numbers_;
    std::map<std::string, int64_t> integers_;
};

} // namespace smc

#endif // USE_BUNDLED_JSON
