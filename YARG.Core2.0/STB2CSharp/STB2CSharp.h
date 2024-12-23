#pragma once
#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"

#if defined _WIN32 || defined __CYGWIN__
    #ifdef STB_EXPORTS
        #ifdef __GNUC__
            #define LIB_API(RetType) extern "C" __attribute__ ((dllexport)) RetType
        #else
            #define LIB_API(RetType) extern "C" __declspec(dllexport) RetType
        #endif
    #else
        #ifdef __GNUC__
            #define LIB_API(RetType) extern "C" __attribute__ ((dllimport)) RetType
        #else
            #define LIB_API(RetType) extern "C" __declspec(dllimport) RetType
        #endif
    #endif
#elif __GNUC__ >= 4
    #define LIB_API(RetType) extern "C" __attribute__ ((visibility ("default"))) RetType
#else
    #define LIB_API(RetType) extern "C" RetType
#endif

LIB_API(unsigned char*) load_image_from_memory(const unsigned char* bytes, int length, int* width, int* height, int* components);

LIB_API(void) free_image(unsigned char* image);
