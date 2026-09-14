// Native folder picker for iOS: UIDocumentPickerViewController in folder mode,
// so users can add song folders from anywhere the Files app reaches (iCloud,
// SMB network shares, USB drives). Selected folders come back with a
// security-scoped bookmark that the managed side persists and restores on
// later launches.
#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#include <stdlib.h>
#include <string.h>

extern "C" UIViewController* UnityGetGLViewController();

typedef void (*YargFolderPickedCallback)(const char* path, const char* bookmarkBase64);

API_AVAILABLE(ios(14.0))
@interface YargFolderPickerDelegate : NSObject <UIDocumentPickerDelegate>
@property (nonatomic, assign) YargFolderPickedCallback callback;
@end

static YargFolderPickerDelegate* s_activeDelegate;

@implementation YargFolderPickerDelegate

- (void)documentPicker:(UIDocumentPickerViewController *)controller
    didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls
{
    NSURL* url = urls.firstObject;
    const char* path = NULL;
    const char* bookmark = NULL;

    if (url != nil)
    {
        // Keep access for the app's lifetime; song scanning and audio
        // streaming read from this folder at arbitrary later times.
        [url startAccessingSecurityScopedResource];

        NSError* error = nil;
        NSData* bookmarkData = [url bookmarkDataWithOptions:0
                             includingResourceValuesForKeys:nil
                                              relativeToURL:nil
                                                      error:&error];
        if (error != nil)
        {
            NSLog(@"[YARG] Failed to create folder bookmark: %@", error);
        }

        path = url.path.UTF8String;
        bookmark = [bookmarkData base64EncodedStringWithOptions:0].UTF8String;
    }

    if (self.callback != NULL)
    {
        self.callback(path, bookmark);
    }
    s_activeDelegate = nil;
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller
{
    if (self.callback != NULL)
    {
        self.callback(NULL, NULL);
    }
    s_activeDelegate = nil;
}

@end

extern "C" void yarg_pick_folder(YargFolderPickedCallback callback)
{
    if (@available(iOS 14.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            YargFolderPickerDelegate* delegate = [YargFolderPickerDelegate new];
            delegate.callback = callback;
            s_activeDelegate = delegate; // retain until dismissed

            UIDocumentPickerViewController* picker = [[UIDocumentPickerViewController alloc]
                initForOpeningContentTypes:@[UTTypeFolder]];
            picker.delegate = delegate;
            picker.allowsMultipleSelection = NO;
            [UnityGetGLViewController() presentViewController:picker animated:YES completion:nil];
        });
    }
    else if (callback != NULL)
    {
        callback(NULL, NULL);
    }
}

// Resolves a persisted bookmark, re-establishes security-scoped access, and
// returns the folder's current path (strdup'd; caller-side marshalling copies
// it). Returns NULL when the bookmark can no longer be resolved.
extern "C" const char* yarg_resolve_folder_bookmark(const char* bookmarkBase64)
{
    if (bookmarkBase64 == NULL)
    {
        return NULL;
    }

    @autoreleasepool
    {
        NSData* data = [[NSData alloc] initWithBase64EncodedString:@(bookmarkBase64) options:0];
        if (data == nil)
        {
            return NULL;
        }

        BOOL stale = NO;
        NSError* error = nil;
        NSURL* url = [NSURL URLByResolvingBookmarkData:data
                                               options:NSURLBookmarkResolutionWithoutUI
                                         relativeToURL:nil
                                   bookmarkDataIsStale:&stale
                                                 error:&error];
        if (url == nil || error != nil)
        {
            NSLog(@"[YARG] Failed to resolve folder bookmark: %@", error);
            return NULL;
        }

        [url startAccessingSecurityScopedResource];
        return strdup(url.path.UTF8String);
    }
}
