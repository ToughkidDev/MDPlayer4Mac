// Small AppKit bridge for the one macOS-specific interaction we need here.
// Avalonia currently treats Force Touch as a normal mouse pointer, so its
// PointerPointProperties.Pressure remains at the fallback value. AppKit emits
// NSEventTypePressure separately; its stage 2 is the physical Force Click.
#import <AppKit/AppKit.h>

typedef void (*MDPlayerForceTouchCallback)(void);

static MDPlayerForceTouchCallback forceTouchCallback = NULL;
static id forceTouchMonitor = nil;

void MDPlayerInstallForceTouchMonitor(MDPlayerForceTouchCallback callback)
{
    forceTouchCallback = callback;
    if (forceTouchMonitor != nil)
        return;

    forceTouchMonitor = [NSEvent
        addLocalMonitorForEventsMatchingMask:NSEventMaskPressure
        handler:^NSEvent *(NSEvent *event) {
            // Stage 1 is the usual trackpad click; stage 2 is Force Click.
            if (event.stage >= 2 && forceTouchCallback != NULL)
                forceTouchCallback();
            return event;
        }];
}
