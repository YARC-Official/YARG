// GameController-framework bridge for instrument controllers.
//
// Unity's iOS InputSystem backend only surfaces profile-conforming pads as
// Gamepad devices, so anything beyond the standard gamepad elements (extra
// frets, effects switches, drum pads) is invisible. This bridge flattens each
// connected controller's physicalInputProfile into a stable list of named
// float values that the managed side (IOSGameControllerBackend) republishes
// as InputSystem devices with per-controller layouts.
#import <Foundation/Foundation.h>
#import <GameController/GameController.h>
#include <TargetConditionals.h>
#include <stdlib.h>
#include <string.h>

API_AVAILABLE(ios(14.0))
@interface YargGCEntry : NSObject
@property (nonatomic, strong) GCController* controller;
@property (nonatomic, assign) int generation;
@property (nonatomic, strong) NSMutableArray<NSString*>* valueNames;
// Parallel arrays: for each value, the element it reads and which part
// (0 = button/axis value, 1 = dpad x, 2 = dpad y)
@property (nonatomic, strong) NSMutableArray<GCControllerElement*>* valueElements;
@property (nonatomic, strong) NSMutableArray<NSNumber*>* valueKinds;
@end

@implementation YargGCEntry
@end

static NSMutableArray* s_entries; // YargGCEntry, iOS 14+
static int s_nextGeneration = 1;
static bool s_started = false;

API_AVAILABLE(ios(14.0))
static void AddControllerEntry(GCController* controller)
{
    for (YargGCEntry* existing in s_entries)
    {
        if (existing.controller == controller)
        {
            return;
        }
    }

    YargGCEntry* entry = [YargGCEntry new];
    entry.controller = controller;
    entry.generation = s_nextGeneration++;
    entry.valueNames = [NSMutableArray new];
    entry.valueElements = [NSMutableArray new];
    entry.valueKinds = [NSMutableArray new];

    GCPhysicalInputProfile* profile = controller.physicalInputProfile;
    // Stable ordering so the managed layout matches the read order
    NSArray<NSString*>* keys =
        [profile.elements.allKeys sortedArrayUsingSelector:@selector(compare:)];

    // Element inventory, logged for every controller (skipped categories
    // included): shows whether an instrument hides extra elements behind a
    // gamepad product category, which decides publish-vs-skip questions
    // without a debugger attached.
    NSLog(@"[YARG] GC controller '%@' (category '%@', profile %@): %lu elements: %@",
        controller.vendorName, controller.productCategory,
        NSStringFromClass([profile class]),
        (unsigned long) keys.count, [keys componentsJoinedByString:@", "]);
    for (NSString* key in keys)
    {
        GCControllerElement* element = profile.elements[key];
        if ([element isKindOfClass:[GCControllerDirectionPad class]])
        {
            [entry.valueNames addObject:[key stringByAppendingString:@"/x"]];
            [entry.valueElements addObject:element];
            [entry.valueKinds addObject:@1];
            [entry.valueNames addObject:[key stringByAppendingString:@"/y"]];
            [entry.valueElements addObject:element];
            [entry.valueKinds addObject:@2];
        }
        else if ([element isKindOfClass:[GCControllerButtonInput class]] ||
                 [element isKindOfClass:[GCControllerAxisInput class]])
        {
            [entry.valueNames addObject:key];
            [entry.valueElements addObject:element];
            [entry.valueKinds addObject:@0];
        }
    }

    [s_entries addObject:entry];
}

// Test hook: spawns Apple's software GCVirtualController (iOS 15+), which
// connects a genuine GCController the bridge then publishes like hardware.
// Lets the whole layer be exercised on the simulator, where physical
// controllers cannot be attached from automation. Simulator-only: on a real
// device the on-screen gamepad would cover the UI and intercept touches
// (touch there is plain pointer input; real controllers connect directly).
static id s_virtualController; // GCVirtualController*, retained while shown

extern "C" int yarg_gc_spawn_virtual(void)
{
#if !TARGET_OS_SIMULATOR
    return 0;
#else
    if (@available(iOS 15.0, *))
    {
        if (s_virtualController != nil)
        {
            return 1;
        }

        dispatch_async(dispatch_get_main_queue(), ^{
            GCVirtualControllerConfiguration* config = [GCVirtualControllerConfiguration new];
            config.elements = [NSSet setWithObjects:
                GCInputDirectionPad, GCInputButtonA, GCInputButtonB,
                GCInputButtonX, GCInputButtonY,
                GCInputLeftShoulder, GCInputRightShoulder, nil];
            GCVirtualController* controller =
                [GCVirtualController virtualControllerWithConfiguration:config];
            s_virtualController = controller;
            [controller connectWithReplyHandler:^(NSError* error) {
                if (error != nil)
                {
                    NSLog(@"[YARG] Virtual controller connect failed: %@", error);
                }
            }];
        });
        return 1;
    }
    return 0;
#endif
}

extern "C" void yarg_gc_start(void)
{
    if (s_started)
    {
        return;
    }
    s_started = true;

    if (@available(iOS 14.0, *))
    {
        s_entries = [NSMutableArray new];

        for (GCController* controller in GCController.controllers)
        {
            AddControllerEntry(controller);
        }

        [NSNotificationCenter.defaultCenter
            addObserverForName:GCControllerDidConnectNotification
                        object:nil
                         queue:NSOperationQueue.mainQueue
                    usingBlock:^(NSNotification* note) {
            AddControllerEntry((GCController*) note.object);
        }];
        [NSNotificationCenter.defaultCenter
            addObserverForName:GCControllerDidDisconnectNotification
                        object:nil
                         queue:NSOperationQueue.mainQueue
                    usingBlock:^(NSNotification* note) {
            GCController* controller = (GCController*) note.object;
            for (YargGCEntry* entry in [s_entries copy])
            {
                if (entry.controller == controller)
                {
                    [s_entries removeObject:entry];
                }
            }
        }];
    }
}

extern "C" int yarg_gc_get_controller_count(void)
{
    if (@available(iOS 14.0, *))
    {
        return (int) s_entries.count;
    }
    return 0;
}

// Monotonically increasing id, unique per connection (reconnects get new ids)
extern "C" int yarg_gc_get_controller_generation(int index)
{
    if (@available(iOS 14.0, *))
    {
        if (index >= 0 && index < (int) s_entries.count)
        {
            return ((YargGCEntry*) s_entries[index]).generation;
        }
    }
    return 0;
}

// strdup'd; freed by the managed marshaller
extern "C" const char* yarg_gc_get_controller_name(int index)
{
    if (@available(iOS 14.0, *))
    {
        if (index >= 0 && index < (int) s_entries.count)
        {
            YargGCEntry* entry = s_entries[index];
            NSString* vendor = entry.controller.vendorName ?: @"Controller";
            NSString* category = entry.controller.productCategory ?: @"";
            NSString* name = category.length > 0
                ? [NSString stringWithFormat:@"%@ (%@)", vendor, category]
                : vendor;
            return strdup(name.UTF8String);
        }
    }
    return NULL;
}

extern "C" const char* yarg_gc_get_product_category(int index)
{
    if (@available(iOS 14.0, *))
    {
        if (index >= 0 && index < (int) s_entries.count)
        {
            NSString* category =
                ((YargGCEntry*) s_entries[index]).controller.productCategory ?: @"";
            return strdup(category.UTF8String);
        }
    }
    return NULL;
}

extern "C" int yarg_gc_get_value_count(int index)
{
    if (@available(iOS 14.0, *))
    {
        if (index >= 0 && index < (int) s_entries.count)
        {
            return (int) ((YargGCEntry*) s_entries[index]).valueNames.count;
        }
    }
    return 0;
}

// strdup'd; freed by the managed marshaller
extern "C" const char* yarg_gc_get_value_name(int index, int valueIndex)
{
    if (@available(iOS 14.0, *))
    {
        if (index >= 0 && index < (int) s_entries.count)
        {
            YargGCEntry* entry = s_entries[index];
            if (valueIndex >= 0 && valueIndex < (int) entry.valueNames.count)
            {
                return strdup(entry.valueNames[valueIndex].UTF8String);
            }
        }
    }
    return NULL;
}

// Reads all current values into buffer; returns how many were written.
// Values are read on whichever thread calls this; GCController elements are
// safe to read off the main thread (their handlers fire on main).
extern "C" int yarg_gc_read_values(int index, float* buffer, int capacity)
{
    if (@available(iOS 14.0, *))
    {
        if (index < 0 || index >= (int) s_entries.count || buffer == NULL)
        {
            return 0;
        }

        YargGCEntry* entry = s_entries[index];
        int count = (int) entry.valueElements.count;
        if (count > capacity)
        {
            count = capacity;
        }

        for (int i = 0; i < count; i++)
        {
            GCControllerElement* element = entry.valueElements[i];
            switch (entry.valueKinds[i].intValue)
            {
                case 1:
                    buffer[i] = ((GCControllerDirectionPad*) element).xAxis.value;
                    break;
                case 2:
                    buffer[i] = ((GCControllerDirectionPad*) element).yAxis.value;
                    break;
                default:
                    if ([element isKindOfClass:[GCControllerButtonInput class]])
                    {
                        buffer[i] = ((GCControllerButtonInput*) element).value;
                    }
                    else
                    {
                        buffer[i] = ((GCControllerAxisInput*) element).value;
                    }
                    break;
            }
        }

        return count;
    }
    return 0;
}
