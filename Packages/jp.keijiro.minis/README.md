Minis: MIDI Input for New Input System
======================================

![gif](https://i.imgur.com/xo9BgV4.gif)
![gif](https://i.imgur.com/UFqQcEz.gif)

**Minis** (MIDI Input for New Input System) is a Unity plugin that adds MIDI
input device support to [Unity's new Input System].

[Unity's new Input System]:
  https://blogs.unity3d.com/2019/10/14/introducing-the-new-input-system/

System Requirements
-------------------

- Unity 2019.3 or later
- 64-bit desktop platforms (Windows, macOS, Linux)

On Linux, ALSA (libasound2) must be installed on the system.

How To Install
--------------

This package uses the [scoped registry] feature to resolve package
dependencies. Please add the following sections to the manifest file
(Packages/manifest.json).

[scoped registry]: https://docs.unity3d.com/Manual/upm-scoped.html

To the `scopedRegistries` section:

```
{
  "name": "Keijiro",
  "url": "https://registry.npmjs.com",
  "scopes": [ "jp.keijiro" ]
}
```

To the `dependencies` section:

```
"jp.keijiro.minis": "1.0.10"
```

After changes, the manifest file should look like below:

```
{
  "scopedRegistries": [
    {
      "name": "Keijiro",
      "url": "https://registry.npmjs.com",
      "scopes": [ "jp.keijiro" ]
    }
  ],
  "dependencies": {
    "jp.keijiro.minis": "1.0.10",
    ...
```

How To Use
----------

### Input Controls

When Minis is installed to a project, MIDI control elements appear under
"Other" > "MIDI Device". You can also use the "Listen" button to select a
control.

![gif](https://i.imgur.com/nFzQM2M.gif)

**TIPS** - There is a small known issue where the listener only reacts to notes
with high velocity (higher than 63). You may have to press a key strongly.

The MIDI Notes are shown as button controls with names like "Note C4". These
controls work as pressure-sensitive buttons. These button values are normalized
as values between 0.0 to 1.0.

The MIDI Controls (CC) are shown as axis controls with names like "Control 10".
These axis values are normalized as values between 0.0 to 1.0.

For further usage of the new Input System, please see the [Input System manual].

[Input System manual]:
  https://docs.unity3d.com/Packages/com.unity.inputsystem@latest/

### MIDI Channels

Minis treats each MIDI channel as an individual device. MIDI devices are
dynamically added to the Input System when it detects a MIDI message from a
new channel.

**TIPS** - It means that the Input System can't detect a MIDI device until it
sends a message. You may have to prompt the user to press a key to activate the
device.

When multiple MIDI interfaces are connected to the system, channels under these
interfaces are also treated as individual devices. For instance, if you have
connected two MIDI interfaces to a computer, you can use up to 32 input devices
at the same time (as each interface can handle up to 16 channels).

### MIDI Device Assigner

![inspector](https://i.imgur.com/xHkTuOgm.jpg)

MIDI Device Assigner is a small utility that assigns a MIDI device to
PlayerInput. You can specify a MIDI channel and a product name as a search
condition. It assigns a found device to a PlayerInput instance that exists in
the same GameObject.

Scripting Examples
------------------

This repository contains some examples showing how to use Minis from C# scripts.

[**DeviceCallback.cs**](Assets/Script/DeviceCallback.cs) - This script shows
how to define a callback to get notified on MIDI device additions and removals.

[**DeviceQuery.cs**](Assets/Script/DeviceQuery.cs) - This script shows how to
search MIDI devices by a pattern matching with a product name and a channel
number.

[**NoteCallback.cs**](Assets/Script/NoteCallback.cs) - This script shows how to
define a callback to get notified on MIDI note-on/off events.

Frequently Asked Questions
--------------------------

#### Does it support MIDI out?

No, but the backend (RtMidi) supports MIDI out. You can use the output
functionality by directly accessing it. Please check the [RtMidi for Unity]
repository that contains a MIDI out sample script.

[RtMidi for Unity]: https://github.com/keijiro/jp.keijiro.rtmidi
