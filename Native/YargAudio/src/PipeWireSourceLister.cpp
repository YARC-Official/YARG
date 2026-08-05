#include "PipeWireSourceLister.h"

#include <algorithm>
#include <cstdio>
#include <cctype>
#include <chrono>
#include <cstring>
#include <limits>
#include <memory>
#include <mutex>
#include <thread>

namespace yarg::audio {
namespace {

// ---------------------------------------------------------------------------
// PipeWire ABI surface (hand-declared; layouts verified stable across
// pipewire 0.3.65 (2022) -> 1.6.8 (current)). Only the fields we read are
// declared. See plans/pipewire-mic-listing.md for the spike that pinned these.
// ---------------------------------------------------------------------------

struct SpaDict; // opaque; read through pw_properties_new_dict

struct SpaList {
    SpaList* next;
    SpaList* prev;
};

struct SpaCallbacks {
    const void* funcs;
    void* data;
};

struct SpaHook {
    SpaList link;
    SpaCallbacks cb;
    void (*removed)(SpaHook* hook);
    void* priv;
};

static_assert(sizeof(SpaHook) == 6 * sizeof(void*),
    "SpaHook layout changed; update the hand-declared ABI");

struct PwRegistryEvents {
    uint32_t version;
    void (*global)(void* data, uint32_t id, uint32_t permissions,
        const char* type, uint32_t version, const struct SpaDict* props);
    void (*global_remove)(void* data, uint32_t id);
};

struct PwNodeInfo {
    uint32_t id;
    uint32_t max_input_ports;
    uint32_t max_output_ports;
    uint64_t change_mask;
    uint32_t n_input_ports;
    uint32_t n_output_ports;
    int32_t state;
    const char* error;
    struct SpaDict* props;
    void* params;
    uint32_t n_params;
};

struct PwNodeEvents {
    uint32_t version;
    void (*info)(void* data, const PwNodeInfo* info);
    void (*param)(void* data, int seq, uint32_t id, uint32_t index,
        uint32_t next, const void* param);
};

} // namespace

struct PipeWireSourceLister::Functions {
    void (*init)(int* argc, char*** argv) = nullptr;
    void* (*threadLoopNew)(const char* name, const void* properties,
        std::size_t userDataSize) = nullptr;
    void* (*threadLoopGetLoop)(void* loop) = nullptr;
    int (*threadLoopStart)(void* loop) = nullptr;
    void (*threadLoopStop)(void* loop) = nullptr;
    void (*threadLoopDestroy)(void* loop) = nullptr;
    void (*threadLoopLock)(void* loop) = nullptr;
    void (*threadLoopUnlock)(void* loop) = nullptr;
    void* (*contextNew)(void* loop, const void* properties,
        std::size_t userDataSize) = nullptr;
    void* (*contextConnect)(void* context, const void* properties,
        std::size_t userDataSize) = nullptr;
    void (*contextDestroy)(void* context) = nullptr;
    void* (*coreGetRegistry)(void* core, uint32_t version,
        std::size_t userDataSize) = nullptr;
    int (*registryAddListener)(void* registry, SpaHook* listener,
        const PwRegistryEvents* events, void* data) = nullptr;
    void* (*registryBind)(void* registry, uint32_t id, const char* type,
        uint32_t version, std::size_t userDataSize) = nullptr;
    int (*nodeAddListener)(void* node, SpaHook* listener,
        const PwNodeEvents* events, void* data) = nullptr;
    void* (*propertiesNewDict)(const struct SpaDict* dict) = nullptr;
    const char* (*propertiesGet)(void* properties, const char* key) = nullptr;
    void (*propertiesFree)(void* properties) = nullptr;
};

namespace {

// ---------------------------------------------------------------------------
// Callback state. Written on the PipeWire thread loop, read on the caller
// thread only after the loop has been stopped (which joins the loop thread).
// ---------------------------------------------------------------------------

constexpr std::chrono::milliseconds SnapshotTimeout = std::chrono::milliseconds(2000);
constexpr int QuietPollCount = 5; // ~50ms without registry activity settles a batch

struct SnapshotState {
    std::vector<InputSourceInfo> sources;
    std::vector<SpaHook*> hooks;
    PwNodeEvents nodeEvents{};
    std::atomic<int> pending{0};
    std::atomic<int> done{0};
    std::atomic<bool> changed{false};
    const PipeWireSourceLister::Functions* functions = nullptr;
    void* registry = nullptr;
};

std::string getProp(const PipeWireSourceLister::Functions& fn,
    const struct SpaDict* dict, const char* key) {
    void* properties = fn.propertiesNewDict(dict);
    if (!properties) return {};
    // pw_properties_get returns a pointer into the properties object's own
    // storage (pw_properties_new_dict deep-copies), so copy before freeing.
    const char* value = fn.propertiesGet(properties, key);
    std::string result = value ? value : "";
    fn.propertiesFree(properties);
    return result;
}

int parseIntProp(const PipeWireSourceLister::Functions& fn,
    const struct SpaDict* dict, const char* key, int fallback) {
    const std::string value = getProp(fn, dict, key);
    if (value.empty()) return fallback;
    char* end = nullptr;
    const long parsed = std::strtol(value.c_str(), &end, 10);
    if (end == value.c_str() || parsed < std::numeric_limits<int>::min() ||
        parsed > std::numeric_limits<int>::max()) {
        return fallback;
    }
    return static_cast<int>(parsed);
}

// "AUX0" or "0" -> 0, "AUX12" -> 12. Named SPA positions ("[FL]") map by
// channel order. -1 when the channel cannot be determined.
int parseSplitChannel(const std::string& position) {
    const std::size_t digit = position.find_first_of("0123456789");
    if (digit != std::string::npos) {
        char* end = nullptr;
        const long channel = std::strtol(position.c_str() + digit, &end, 10);
        if (end != position.c_str() + digit &&
            channel >= 0 && channel <= std::numeric_limits<int>::max()) {
            return static_cast<int>(channel);
        }
    }

    static constexpr const char* kChannelNames[] = {
        "FL", "FR", "FC", "LFE", "BL", "BR", "FLC", "FRC",
        "BC", "SL", "SR", "TC", "TFL", "TFC", "TFR", "TBL", "TBC", "TBR",
    };
    const std::size_t open = position.find('[');
    const std::size_t close = position.find(']');
    if (open == std::string::npos || close == std::string::npos ||
        close <= open) {
        return -1;
    }
    std::string token = position.substr(open + 1, close - open - 1);
    std::transform(token.begin(), token.end(), token.begin(),
        [](unsigned char c) { return static_cast<char>(std::toupper(c)); });
    for (std::size_t i = 0; i < sizeof(kChannelNames) / sizeof(kChannelNames[0]); ++i) {
        if (token == kChannelNames[i]) return static_cast<int>(i);
    }
    return -1;
}

// "[AUX0,AUX1]" -> 2; empty -> 0.
int countHwChannels(const std::string& hwPosition) {
    if (hwPosition.empty()) return 0;
    return 1 + static_cast<int>(std::count(hwPosition.begin(), hwPosition.end(), ','));
}

void onGlobal(void* data, uint32_t id, uint32_t permissions, const char* type,
    uint32_t version, const struct SpaDict* props) {
    (void) permissions;
    auto* state = static_cast<SnapshotState*>(data);
    if (!type || std::strcmp(type, "PipeWire:Interface:Node") != 0) return;

    const std::string mediaClass = getProp(*state->functions, props, "media.class");
    if (mediaClass != "Audio/Source") return;

    // Registry globals only carry minimal props (media.class, node.name,
    // node.description). The ALSA details (alsa.card, api.alsa.split.*,
    // api.alsa.path) arrive only after binding the node and reading its info
    // event, so bind every Audio/Source node and collect info.
    void* node = state->functions->registryBind(state->registry, id, type, version, 0);
    if (!node) return;

    // Each listener hook must be individually allocated: sharing one hook
    // across binds clobbers earlier listeners (spike segfault).
    auto* hook = new SpaHook();
    std::memset(hook, 0, sizeof *hook);
    state->hooks.push_back(hook);
    if (state->functions->nodeAddListener(node, hook, &state->nodeEvents, state) == 0) {
        state->pending.fetch_add(1);
    }
    state->changed.store(true);
}

void onNodeInfo(void* data, const PwNodeInfo* info) {
    auto* state = static_cast<SnapshotState*>(data);
    if (info && info->props) {
        const auto& fn = *state->functions;
        InputSourceInfo source;
        source.alsaCard = parseIntProp(fn, info->props, "alsa.card", -1);
        source.alsaDevice = parseIntProp(fn, info->props, "alsa.device", -1);
        source.alsaSubdevice = parseIntProp(fn, info->props, "alsa.subdevice", -1);
        source.nodeName = getProp(fn, info->props, "node.name");
        source.description = getProp(fn, info->props, "node.description");
        source.alsaPath = getProp(fn, info->props, "api.alsa.path");

        // Keep only hardware-backed sources (BASS can only open raw ALSA PCMs).
        // Monitors, Bluetooth and virtual sources have no alsa.card/path.
        if (source.alsaCard >= 0 && !source.alsaPath.empty()) {
            const std::string position = getProp(fn, info->props, "api.alsa.split.position");
            if (!position.empty()) {
                source.captureChannel = parseSplitChannel(position);
                const std::string hwPosition = getProp(fn, info->props,
                    "api.alsa.split.hw-position");
                const int hwChannels = hwPosition.empty() ? 0 : countHwChannels(hwPosition);
                source.captureChannels = hwChannels >= 2 ? hwChannels : 2;
            }
            state->sources.push_back(std::move(source));
        }
    }
    state->done.fetch_add(1);
    state->changed.store(true);
}

template <typename T>
bool bindFn(const PlatformDynamicLibrary& module, const char* name, T& target) noexcept {
    target = reinterpret_cast<T>(module.symbol(name));
    return target != nullptr;
}

} // namespace

PipeWireSourceLister::~PipeWireSourceLister() {
    unload();
}

PipeWireSourceLister::PipeWireSourceLister(PipeWireSourceLister&&) noexcept = default;
PipeWireSourceLister& PipeWireSourceLister::operator=(PipeWireSourceLister&&) noexcept = default;

bool PipeWireSourceLister::load() noexcept {
    if (functions_) return true;
    module_ = PlatformDynamicLibrary::load("libpipewire-0.3.so.0");
    if (!module_) return false;

    auto functions = std::make_unique<Functions>();
    bool ok = true;
    ok &= bindFn(module_, "pw_init", functions->init);
    ok &= bindFn(module_, "pw_thread_loop_new", functions->threadLoopNew);
    ok &= bindFn(module_, "pw_thread_loop_get_loop", functions->threadLoopGetLoop);
    ok &= bindFn(module_, "pw_thread_loop_start", functions->threadLoopStart);
    ok &= bindFn(module_, "pw_thread_loop_stop", functions->threadLoopStop);
    ok &= bindFn(module_, "pw_thread_loop_destroy", functions->threadLoopDestroy);
    ok &= bindFn(module_, "pw_thread_loop_lock", functions->threadLoopLock);
    ok &= bindFn(module_, "pw_thread_loop_unlock", functions->threadLoopUnlock);
    ok &= bindFn(module_, "pw_context_new", functions->contextNew);
    ok &= bindFn(module_, "pw_context_connect", functions->contextConnect);
    ok &= bindFn(module_, "pw_context_destroy", functions->contextDestroy);
    ok &= bindFn(module_, "pw_core_get_registry", functions->coreGetRegistry);
    ok &= bindFn(module_, "pw_registry_add_listener", functions->registryAddListener);
    ok &= bindFn(module_, "pw_registry_bind", functions->registryBind);
    ok &= bindFn(module_, "pw_node_add_listener", functions->nodeAddListener);
    ok &= bindFn(module_, "pw_properties_new_dict", functions->propertiesNewDict);
    ok &= bindFn(module_, "pw_properties_get", functions->propertiesGet);
    ok &= bindFn(module_, "pw_properties_free", functions->propertiesFree);
    if (!ok) {
        unload();
        return false;
    }
    functions_ = functions.release();
    return true;
}

void PipeWireSourceLister::unload() noexcept {
    delete functions_;
    functions_ = nullptr;
    module_ = {};
}

int PipeWireSourceLister::list(std::vector<InputSourceInfo>& sources) {
    sources.clear();
    if (!load()) return -4; // YARG_AUDIO_ERROR_DEPENDENCY

    static std::once_flag initFlag;
    std::call_once(initFlag, [this] { functions_->init(nullptr, nullptr); });

    void* loop = functions_->threadLoopNew("yarg-audio", nullptr, 0);
    if (!loop) return -6; // YARG_AUDIO_ERROR_INTERNAL

    SnapshotState state;
    state.functions = functions_;
    state.nodeEvents.version = 0;
    state.nodeEvents.info = &onNodeInfo;

    SpaHook registryHook{};
    std::memset(&registryHook, 0, sizeof registryHook);
    PwRegistryEvents registryEvents{};
    registryEvents.version = 0;
    registryEvents.global = &onGlobal;

    if (functions_->threadLoopStart(loop) != 0) {
        functions_->threadLoopDestroy(loop);
        return -6;
    }

    functions_->threadLoopLock(loop);
    void* context = functions_->contextNew(functions_->threadLoopGetLoop(loop), nullptr, 0);
    void* core = context ? functions_->contextConnect(context, nullptr, 0) : nullptr;
    bool setupOk = core != nullptr;
    if (setupOk) {
        state.registry = functions_->coreGetRegistry(core, 0, 0);
        setupOk = state.registry != nullptr &&
            functions_->registryAddListener(state.registry, &registryHook,
                &registryEvents, &state) >= 0;
    }
    functions_->threadLoopUnlock(loop);

    if (!setupOk) {
        functions_->threadLoopStop(loop);
        if (context) functions_->contextDestroy(context);
        functions_->threadLoopDestroy(loop);
        // PipeWire is installed but the server is unreachable.
        return -4;
    }

    // Wait for the initial registry batch to settle: poll until no activity
    // for ~50ms with every bound source's info received, or the timeout.
    const auto deadline = std::chrono::steady_clock::now() + SnapshotTimeout;
    bool sawActivity = false;
    int quiet = 0;
    while (std::chrono::steady_clock::now() < deadline) {
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
        if (state.changed.exchange(false)) {
            sawActivity = true;
            quiet = 0;
        } else if (sawActivity && ++quiet >= QuietPollCount &&
            state.done.load() >= state.pending.load()) {
            break;
        }
    }

    functions_->threadLoopStop(loop);
    if (context) functions_->contextDestroy(context);
    functions_->threadLoopDestroy(loop);

    for (SpaHook* hook : state.hooks) {
        delete hook;
    }
    sources = std::move(state.sources);
    return 0;
}

} // namespace yarg::audio
