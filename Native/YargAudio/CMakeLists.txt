cmake_minimum_required(VERSION 3.25)
project(YargAudio VERSION 0.1.0 LANGUAGES CXX)

if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "YargAudio supports 64-bit desktop targets only")
endif()
if(NOT WIN32 AND NOT APPLE AND NOT (UNIX AND CMAKE_SYSTEM_PROCESSOR MATCHES "^(x86_64|amd64|AMD64)$"))
    message(FATAL_ERROR "Unsupported YargAudio target: ${CMAKE_SYSTEM_NAME}/${CMAKE_SYSTEM_PROCESSOR}")
endif()

option(YARG_AUDIO_BUILD_TESTS "Build native unit tests" ON)
find_package(Threads REQUIRED)

set(YARG_AUDIO_SOURCES
    src/BassCoreBindings.cpp
    src/BassMixBindings.cpp
    src/dsp/GainDsp.cpp
    src/dsp/FreeverbDsp.cpp
    src/one_shot/ScheduledSampleSource.cpp
    src/one_shot/NativeOneShotStream.cpp
    src/PlatformDynamicLibrary.cpp
    src/yarg_audio_c_api.cpp
)

add_library(yarg_audio SHARED ${YARG_AUDIO_SOURCES})
target_compile_features(yarg_audio PRIVATE cxx_std_20)
target_compile_definitions(yarg_audio PRIVATE YARG_AUDIO_BUILD)
target_include_directories(yarg_audio
    PUBLIC include
    PRIVATE src
)
target_link_libraries(yarg_audio PRIVATE Threads::Threads ${CMAKE_DL_LIBS})
set_target_properties(yarg_audio PROPERTIES
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN YES
)

if(MSVC)
    target_compile_definitions(yarg_audio PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)
    target_compile_options(yarg_audio PRIVATE /W4 /permissive- /EHsc /Brepro)
    target_link_options(yarg_audio PRIVATE /Brepro)
    set_property(TARGET yarg_audio PROPERTY MSVC_RUNTIME_LIBRARY
        "MultiThreaded$<$<CONFIG:Debug>:Debug>")
else()
    target_compile_options(yarg_audio PRIVATE -Wall -Wextra -Wpedantic)
    if(APPLE)
        set_target_properties(yarg_audio PROPERTIES
            BUILD_WITH_INSTALL_RPATH YES
            INSTALL_NAME_DIR "@rpath"
        )
        # dyld4 (macOS 15+) refuses to load dylibs without an LC_UUID, so do
        # NOT pass -Wl,-no_uuid. But the linker's random UUID breaks the
        # byte-reproducible builds CI uses to verify the committed plugin.
        # Patch a fixed UUID post-link to get both determinism and loadability.
        # The modern linker ad-hoc signs its output, so the patch invalidates
        # the signature; re-sign ad-hoc afterwards to keep dlopen happy.
        add_custom_command(TARGET yarg_audio POST_BUILD
            COMMAND python3
                ${CMAKE_CURRENT_SOURCE_DIR}/cmake/patch_fixed_uuid.py
                $<TARGET_FILE:yarg_audio>
            COMMAND codesign --force --sign - $<TARGET_FILE:yarg_audio>
            COMMENT "Patching fixed LC_UUID and re-signing"
            VERBATIM)
    elseif(UNIX)
        target_link_options(yarg_audio PRIVATE -Wl,--build-id=none)
    endif()
endif()

if(YARG_AUDIO_BUILD_TESTS)
    enable_testing()
    add_executable(yarg_audio_tests
        tests/GainDspTests.cpp
        tests/FreeverbDspTests.cpp
        tests/ScheduledSampleSourceTests.cpp
        tests/NativeOneShotStreamTests.cpp
        tests/main.cpp
        src/BassCoreBindings.cpp
        src/BassMixBindings.cpp
        src/dsp/GainDsp.cpp
        src/dsp/FreeverbDsp.cpp
        src/one_shot/ScheduledSampleSource.cpp
        src/one_shot/NativeOneShotStream.cpp
        src/PlatformDynamicLibrary.cpp
    )
    target_compile_features(yarg_audio_tests PRIVATE cxx_std_20)
    target_include_directories(yarg_audio_tests PRIVATE include src)
    target_link_libraries(yarg_audio_tests PRIVATE Threads::Threads ${CMAKE_DL_LIBS})
    if(MSVC)
        target_compile_definitions(yarg_audio_tests PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)
        target_compile_options(yarg_audio_tests PRIVATE /W4 /permissive- /EHsc /Brepro)
        target_link_options(yarg_audio_tests PRIVATE /Brepro)
        set_property(TARGET yarg_audio_tests PROPERTY MSVC_RUNTIME_LIBRARY
            "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    else()
        target_compile_options(yarg_audio_tests PRIVATE -Wall -Wextra -Wpedantic)
        if(APPLE)
            # Fixed UUID only (see yarg_audio target above); tests are not
            # committed, so they need no reproducibility constraint.
        elseif(UNIX)
            target_link_options(yarg_audio_tests PRIVATE -Wl,--build-id=none)
        endif()
    endif()
    add_test(NAME yarg_audio_tests COMMAND yarg_audio_tests)
endif()
