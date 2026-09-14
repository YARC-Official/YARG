// BASS ships dynamic frameworks on iOS, embedded at <app>/Frameworks/.
// IL2CPP resolves DllImport("bass") by probing "<app>/bass[.dylib...]" and a
// plain dlopen("bass"), none of which can find an embedded framework — so
// install il2cpp's find-plugin callback and rewrite the library names to the
// frameworks' real paths.
#import <Foundation/Foundation.h>
#include <map>
#include <mutex>
#include <string>

extern "C" void il2cpp_set_find_plugin_callback(const char* (*callback)(const char*));

static const char* YARGFindPlugin(const char* name)
{
    // Leaked on purpose: il2cpp keeps the returned pointer indefinitely.
    static std::map<std::string, char*>* cache = new std::map<std::string, char*>();
    static std::mutex mutex;

    std::lock_guard<std::mutex> lock(mutex);
    auto found = cache->find(name);
    if (found == cache->end())
    {
        char* remapped = NULL;
        @autoreleasepool
        {
            NSString* key = @(name);
            NSString* path = [NSString stringWithFormat:@"%@/%@.framework/%@",
                              NSBundle.mainBundle.privateFrameworksPath, key, key];
            if ([NSFileManager.defaultManager fileExistsAtPath:path])
                remapped = strdup(path.UTF8String);
        }
        found = cache->emplace(name, remapped).first;
    }

    return found->second != NULL ? found->second : name;
}

__attribute__((constructor))
static void YARGInstallPluginRemap(void)
{
    il2cpp_set_find_plugin_callback(YARGFindPlugin);
}
