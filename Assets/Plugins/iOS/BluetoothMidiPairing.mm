// Bluetooth MIDI pairing for iOS: presents CoreAudioKit's
// CABTMIDICentralViewController so users can discover and connect BLE MIDI
// instruments (keyboards, pads, adapters). Once paired, devices show up as
// regular CoreMIDI sources and flow through the existing RtMidi/Minis input
// path with no further work.
#import <UIKit/UIKit.h>
#import <CoreAudioKit/CoreAudioKit.h>

extern "C" UIViewController* UnityGetGLViewController();

typedef void (*YargBTMidiDismissedCallback)(void);

// Owns the presented navigation controller and forwards the Done button.
@interface YargBTMidiPairingHost : NSObject
@property (nonatomic, assign) YargBTMidiDismissedCallback callback;
@property (nonatomic, strong) UINavigationController* navController;
- (void)donePressed:(id)sender;
@end

static YargBTMidiPairingHost* s_activeHost;

@implementation YargBTMidiPairingHost

- (void)donePressed:(id)sender
{
    [self.navController.presentingViewController dismissViewControllerAnimated:YES completion:^{
        if (self.callback != NULL)
        {
            self.callback();
        }
        s_activeHost = nil;
    }];
}

@end

extern "C" void yarg_show_bluetooth_midi_pairing(YargBTMidiDismissedCallback callback)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (s_activeHost != nil)
        {
            return; // already presented
        }

        CABTMIDICentralViewController* central = [CABTMIDICentralViewController new];

        YargBTMidiPairingHost* host = [YargBTMidiPairingHost new];
        host.callback = callback;
        s_activeHost = host; // retain until dismissed

        // CABTMIDICentralViewController has no chrome of its own; wrap it in a
        // navigation controller and give it a Done button to dismiss.
        UINavigationController* nav =
            [[UINavigationController alloc] initWithRootViewController:central];
        host.navController = nav;
        central.navigationItem.rightBarButtonItem =
            [[UIBarButtonItem alloc] initWithBarButtonSystemItem:UIBarButtonSystemItemDone
                                                          target:host
                                                          action:@selector(donePressed:)];
        nav.modalPresentationStyle = UIModalPresentationFormSheet;

        [UnityGetGLViewController() presentViewController:nav animated:YES completion:nil];
    });
}
