#pragma once

namespace yarg::audio {

class PlatformDynamicLibrary {
public:
    PlatformDynamicLibrary() = default;
    ~PlatformDynamicLibrary();
    PlatformDynamicLibrary(const PlatformDynamicLibrary&) = delete;
    PlatformDynamicLibrary& operator=(const PlatformDynamicLibrary&) = delete;
    PlatformDynamicLibrary(PlatformDynamicLibrary&& other) noexcept;
    PlatformDynamicLibrary& operator=(PlatformDynamicLibrary&& other) noexcept;

    static PlatformDynamicLibrary findLoaded(const char* name) noexcept;
    static PlatformDynamicLibrary load(const char* name) noexcept;

    explicit operator bool() const noexcept { return handle_ != nullptr; }
    void* symbol(const char* name) const noexcept;

private:
    PlatformDynamicLibrary(void* handle, bool owned) noexcept
        : handle_(handle), owned_(owned) {}

    void reset() noexcept;

    void* handle_ = nullptr;
    bool owned_ = false;
};

} // namespace yarg::audio
