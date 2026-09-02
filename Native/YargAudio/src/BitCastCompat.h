#pragma once

// std::bit_cast requires GCC >= 11, but the Linux CI container uses gcc 10
// (Ubuntu 20.04, see .github/workflows/native-audio.yml) to keep the glibc
// floor at 2.31. Provide an identical memcpy-based fallback so the source
// builds on older toolchains as well. Semantics match std::bit_cast:
// bit-preserving conversion, noexcept, compile-time size/trivially-copyable
// checks.

#include <cstring>
#include <type_traits>

#if defined(__GNUC__) && !defined(__clang__) && __GNUC__ < 11

namespace yarg::audio {

template <typename To, typename From>
To bitCast(const From& from) noexcept
{
    static_assert(sizeof(To) == sizeof(From));
    static_assert(std::is_trivially_copyable_v<To>);
    static_assert(std::is_trivially_copyable_v<From>);
    To to;
    std::memcpy(&to, &from, sizeof(To));
    return to;
}

} // namespace yarg::audio

#else

#include <bit>

namespace yarg::audio {

template <typename To, typename From>
To bitCast(const From& from) noexcept
{
    return std::bit_cast<To>(from);
}

} // namespace yarg::audio

#endif
