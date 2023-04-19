![DryWetMIDI Logo](https://raw.githubusercontent.com/melanchall/drywetmidi/develop/Resources/Images/dwm-logo.png)

[![NuGet (full)](https://img.shields.io/nuget/v/Melanchall.DryWetMidi.svg?label=NuGet%20(full)&color=5295D0)](https://www.nuget.org/packages/Melanchall.DryWetMidi/) [![NuGet (nativeless)](https://img.shields.io/nuget/v/Melanchall.DryWetMidi.Nativeless.svg?label=NuGet%20(nativeless)&color=5295D0)](https://www.nuget.org/packages/Melanchall.DryWetMidi.Nativeless/) [![Unity asset (full)](https://img.shields.io/static/v1?label=Unity%20Asset%20(full)&message=v6.1.3&color=5295D0)](https://assetstore.unity.com/packages/tools/audio/drywetmidi-222171) [![Unity asset (nativeless)](https://img.shields.io/static/v1?label=Unity%20Asset%20(nativeless)&message=v6.1.3&color=5295D0)](https://assetstore.unity.com/packages/tools/audio/drywetmidi-nativeless-228998)

<!--OVERVIEW-->

DryWetMIDI is the .NET library to work with MIDI data and MIDI devices. It allows:

* Read, write and create [Standard MIDI Files (SMF)](https://www.midi.org/specifications/category/smf-specifications). It is also possible to read [RMID](https://www.loc.gov/preservation/digital/formats/fdd/fdd000120.shtml) files where SMF wrapped to RIFF chunk. You can easily catch specific error when reading or writing MIDI file since all possible errors in a MIDI file are presented as separate exception classes.
* [Send](https://melanchall.github.io/drywetmidi/articles/devices/Output-device.html) MIDI events to/[receive](https://melanchall.github.io/drywetmidi/articles/devices/Input-device.html) them from MIDI devices, [play](https://melanchall.github.io/drywetmidi/articles/playback/Overview.html) MIDI data and [record](https://melanchall.github.io/drywetmidi/articles/recording/Overview.html) it. This APIs support Windows and macOS.
* Finely adjust process of reading and writing. It allows, for example, to read corrupted files and repair them, or build MIDI file validators.
* Implement [custom meta events](https://melanchall.github.io/drywetmidi/articles/custom-data-structures/Custom-meta-events.html) and [custom chunks](https://melanchall.github.io/drywetmidi/articles/custom-data-structures/Custom-chunks.html) that can be written to and read from MIDI files.
* Manage content of a MIDI file either with low-level objects, like event, or high-level ones, like note (read the **High-level data managing** section of the [library docs](https://melanchall.github.io/drywetmidi)).
* Build musical compositions (see [Pattern](https://melanchall.github.io/drywetmidi/articles/composing/Pattern.html) page of the library docs) and use music theory API (see [Music Theory - Overview](https://melanchall.github.io/drywetmidi/articles/music-theory/Overview.html) article).
* Perform complex tasks like quantizing, notes splitting or converting MIDI file to CSV representation (see [Tools](https://melanchall.github.io/drywetmidi/articles/tools/Overview.html) page of the library docs).

Please see [Getting started](#getting-started) section below for quick jump into the library.

## Useful links

* [NuGet](https://www.nuget.org/packages/Melanchall.DryWetMidi)
* [Documentation](https://melanchall.github.io/drywetmidi)
* [Project health](https://melanchall.github.io/drywetmidi/articles/dev/Project-health.html)
* CodeProject articles:
  * [DryWetMIDI: High-Level Processing of MIDI Files](https://www.codeproject.com/Articles/1200014/DryWetMIDI-High-level-processing-of-MIDI-files)
  * [DryWetMIDI: Notes Quantization](https://www.codeproject.com/Articles/1204629/DryWetMIDI-Notes-Quantization)
  * [DryWetMIDI: Working with MIDI Devices](https://www.codeproject.com/Articles/1275475/DryWetMIDI-Working-with-MIDI-Devices)

## Projects using DryWetMIDI

Here the list of noticeable projects that use DryWetMIDI:

* [CoyoteMIDI](https://coyotemidi.com)  
  CoyoteMIDI extends the functionality of your MIDI devices to include keyboard and mouse input, including complex key combinations and multi-step macros.
* [Clone Hero](https://clonehero.net)  
  Free rhythm game, which can be played with any 5 or 6 button guitar controller, game controllers, or just your standard computer keyboard. The game is a clone of Guitar Hero.
* [Electrophonics](https://kaiclavier.itch.io/electrophonics)  
  A collection of virtual musical instruments that features real MIDI output.
* [Rustissimo](https://store.steampowered.com/app/1222580/Rustissimo)  
  Using Rustissimo you can create a concert with your friends and play instruments with synchronization.
* Sample applications from [CIRCE-EYES](https://github.com/CIRCE-EYES):
  * https://github.com/melanchall/drywetmidi/issues/105
  * https://github.com/melanchall/drywetmidi/issues/139

## Getting Started

Let's see some examples of what you can do with DryWetMIDI.

To [read a MIDI file](https://melanchall.github.io/drywetmidi/articles/file-reading-writing/MIDI-file-reading.html) you have to use ```Read``` static method of the ```MidiFile```:

```csharp
var midiFile = MidiFile.Read("Some Great Song.mid");
```

or, in more advanced form (visit [Reading settings](https://melanchall.github.io/drywetmidi/api/Melanchall.DryWetMidi.Core.ReadingSettings.html) page on the library docs to learn more about how to adjust process of reading)

```csharp
var midiFile = MidiFile.Read(
    "Some Great Song.mid",
    new ReadingSettings
    {
        NoHeaderChunkPolicy = NoHeaderChunkPolicy.Abort,
        CustomChunkTypes = new ChunkTypesCollection
        {
            { typeof(MyCustomChunk), "Cstm" }
        }
    });
```

To [write MIDI data to a file](https://melanchall.github.io/drywetmidi/articles/file-reading-writing/MIDI-file-writing.html) you have to use ```Write``` method of the ```MidiFile```:

```csharp
midiFile.Write("My Great Song.mid");
```

or, in more advanced form (visit [Writing settings](https://melanchall.github.io/drywetmidi/api/Melanchall.DryWetMidi.Core.WritingSettings.html) page on the library docs to learn more about how to adjust process of writing)

```csharp
midiFile.Write(
    "My Great Song.mid",
    true,
    MidiFileFormat.SingleTrack,
    new WritingSettings
    {
        UseRunningStatus = true,
        NoteOffAsSilentNoteOn = true
    });
```

Of course you can create a MIDI file from scratch by creating an instance of the ```MidiFile``` and writing it:

```csharp
var midiFile = new MidiFile(
    new TrackChunk(
        new SetTempoEvent(500000)),
    new TrackChunk(
        new TextEvent("It's just single note track..."),
        new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)45),
        new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0)
        {
            DeltaTime = 400
        }));

midiFile.Write("My Future Great Song.mid");
```

or

```csharp
var midiFile = new MidiFile();
TempoMap tempoMap = midiFile.GetTempoMap();

var trackChunk = new TrackChunk();
using (var notesManager = trackChunk.ManageNotes())
{
    NotesCollection notes = notesManager.Notes;
    notes.Add(new Note(
        NoteName.A,
        4,
        LengthConverter.ConvertFrom(
            new MetricTimeSpan(hours: 0, minutes: 0, seconds: 10),
            0,
            tempoMap)));
}

midiFile.Chunks.Add(trackChunk);
midiFile.Write("My Future Great Song.mid");
```

If you want to speed up playing back a MIDI file by two times you can do it with this code:

```csharp                   
foreach (var trackChunk in midiFile.Chunks.OfType<TrackChunk>())
{
    foreach (var setTempoEvent in trackChunk.Events.OfType<SetTempoEvent>())
    {
        setTempoEvent.MicrosecondsPerQuarterNote /= 2;
    }
}
```

Of course this code is simplified. In practice a MIDI file may not contain SetTempo event which means it has the default one (500,000 microseconds per beat).

Instead of modifying a MIDI file you can use [`Playback`](https://melanchall.github.io/drywetmidi/api/Melanchall.DryWetMidi.Multimedia.Playback.html) class:

```csharp
using (var outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth"))
using (var playback = midiFile.GetPlayback(outputDevice))
{
    playback.Speed = 2.0;
    playback.Play();
}
```

To get duration of a MIDI file as `TimeSpan` use this code:

```csharp
TempoMap tempoMap = midiFile.GetTempoMap();
TimeSpan midiFileDuration = midiFile
    .GetTimedEvents()
    .LastOrDefault(e => e.Event is NoteOffEvent)
    ?.TimeAs<MetricTimeSpan>(tempoMap) ?? new MetricTimeSpan();
```

or simply:

```csharp
TimeSpan midiFileDuration = midiFile.GetDuration<MetricTimeSpan>();
```

Suppose you want to remove all C# notes from a MIDI file. It can be done with this code:

```csharp
foreach (var trackChunk in midiFile.GetTrackChunks())
{
    using (var notesManager = trackChunk.ManageNotes())
    {
        notesManager.Notes.RemoveAll(n => n.NoteName == NoteName.CSharp);
    }
}
```

or

```csharp
midiFile.RemoveNotes(n => n.NoteName == NoteName.CSharp);
```

To get all chords of a MIDI file at 20 seconds from the start of the file write this:

```csharp
TempoMap tempoMap = midiFile.GetTempoMap();
IEnumerable<Chord> chordsAt20seconds = midiFile
    .GetChords()
    .AtTime(
        new MetricTimeSpan(0, 0, 20),
        tempoMap,
        LengthedObjectPart.Entire);
```

To create a MIDI file with single note which length will be equal to length of two triplet eighth notes you can use this code:

```csharp
var midiFile = new MidiFile();
var tempoMap = midiFile.GetTempoMap();

var trackChunk = new TrackChunk();
using (var notesManager = trackChunk.ManageNotes())
{
    var length = LengthConverter.ConvertFrom(
        2 * MusicalTimeSpan.Eighth.Triplet(),
        0,
        tempoMap);
    var note = new Note(NoteName.A, 4, length);
    notesManager.Notes.Add(note);
}

midiFile.Chunks.Add(trackChunk);
midiFile.Write("Single note great song.mid");
```

You can even build a musical composition:

```csharp
Pattern pattern = new PatternBuilder()
     
    // Insert a pause of 5 seconds
    .StepForward(new MetricTimeSpan(0, 0, 5))

    // Insert an eighth C# note of the 4th octave
    .Note(Octave.Get(4).CSharp, MusicalTimeSpan.Eighth)

    // Set default note length to triplet eighth and default octave to 5
    .SetNoteLength(MusicalTimeSpan.Eighth.Triplet())
    .SetOctave(Octave.Get(5))

    // Now we can add triplet eighth notes of the 5th octave in a simple way
    .Note(NoteName.A)
    .Note(NoteName.B)
    .Note(NoteName.GSharp)

    // Get pattern
    .Build();

MidiFile midiFile = pattern.ToFile(TempoMap.Default);
```

DryWetMIDI provides [devices API](https://melanchall.github.io/drywetmidi/articles/devices/Overview.html) allowing to send MIDI events to and receive them from MIDI devices. Following example shows how to send events to MIDI device and handle them as they are received by the device:

```csharp
using System;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Core;

// ...

using (var outputDevice = OutputDevice.GetByName("MIDI Device"))
{
    outputDevice.EventSent += OnEventSent;

    using (var inputDevice = InputDevice.GetByName("MIDI Device"))
    {
        inputDevice.EventReceived += OnEventReceived;
        inputDevice.StartEventsListening();

        outputDevice.SendEvent(new NoteOnEvent());
        outputDevice.SendEvent(new NoteOffEvent());
    }
}

// ...

private void OnEventReceived(object sender, MidiEventReceivedEventArgs e)
{
    var midiDevice = (MidiDevice)sender;
    Console.WriteLine($"Event received from '{midiDevice.Name}' at {DateTime.Now}: {e.Event}");
}

private void OnEventSent(object sender, MidiEventSentEventArgs e)
{
    var midiDevice = (MidiDevice)sender;
    Console.WriteLine($"Event sent to '{midiDevice.Name}' at {DateTime.Now}: {e.Event}");
}
```
