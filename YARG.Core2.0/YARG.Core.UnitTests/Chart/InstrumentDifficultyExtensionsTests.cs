using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.UnitTests.Utility;

namespace YARG.Core.UnitTests.Chart;

public class InstrumentDifficultyExtensionsTests
{

    [Test]
    public void DrumTrackWithKickDrumRemovedAndOnlyStarPowerKicks_ShouldWorkCorrectly()
    {
        var chart = GetChartWithStarPowerSectionOfOnlyKickDrum();
        var notes = chart.ProDrums.FirstDifficulty();

        var starPowerStartCount = notes.Notes.Count(note => note.IsStarPowerStart);
        var starPowerEndCount = notes.Notes.Count(note => note.IsStarPowerEnd);
        Assert.That(starPowerStartCount, Is.EqualTo(1));
        Assert.That(starPowerEndCount, Is.EqualTo(1));
        Assert.That(notes.Notes.Count, Is.EqualTo(4));

        notes.RemoveKickDrumNotes();

        starPowerStartCount = notes.Notes.Count(note => note.IsStarPowerStart);
        starPowerEndCount = notes.Notes.Count(note => note.IsStarPowerEnd);

        // The song we're testing has a star power section of only kick drums, so it should be completely removed.
        // The only note remaining should be the red drum note. However, it should not have any starpower starts/ends
        Assert.That(notes.Notes.Count, Is.EqualTo(1));
        Assert.That(starPowerStartCount, Is.EqualTo(0));
        Assert.That(starPowerEndCount, Is.EqualTo(0));
        Assert.That(notes.Notes.Last().NextNote, Is.Null);
    }

    [Test]
    public void DrumTrackWithKickDrumRemovedAndStarPowerEndOnKick_ShouldWorkCorrectly()
    {
        var chart = GetChartWithStarPowerEndingInKickDrum();
        var notes = chart.ProDrums.FirstDifficulty();

        var starPowerStartCount = notes.Notes.Count(note => note.IsStarPowerStart);
        var starPowerEndCount = notes.Notes.Count(note => note.IsStarPowerEnd);
        Assert.That(starPowerStartCount, Is.EqualTo(1));
        Assert.That(starPowerEndCount, Is.EqualTo(1));
        Assert.That(notes.Notes.Count, Is.EqualTo(3));

        notes.RemoveKickDrumNotes();

        starPowerStartCount = notes.Notes.Count(note => note.IsStarPowerStart);
        starPowerEndCount = notes.Notes.Count(note => note.IsStarPowerEnd);

        //the kick drum should be removed, but the star power end should be moved up
        Assert.That(notes.Notes.Count, Is.EqualTo(2));
        Assert.That(starPowerStartCount, Is.EqualTo(1));
        Assert.That(starPowerEndCount, Is.EqualTo(1));
        Assert.That(notes.Notes.Last().NextNote, Is.Null);
    }

    [Test]
    public void DrumTrackWithKickDrumRemovedAndStarPowerEndOnKickChord_ShouldWorkCorrectly()
    {
        var chart = GetChartWithStarPowerEndingInChordWithKickDrum();
        var notes = chart.ProDrums.FirstDifficulty();

        var starPowerStartCount = notes.Notes.Count(note => note.IsStarPowerStart);
        var starPowerEndCount = notes.Notes.Count(note => note.IsStarPowerEnd);
        Assert.That(starPowerStartCount, Is.EqualTo(1));
        Assert.That(starPowerEndCount, Is.EqualTo(1));
        Assert.That(notes.Notes.Count, Is.EqualTo(3));

        notes.RemoveKickDrumNotes();

        starPowerStartCount = notes.Notes.Count(note => note.IsStarPowerStart);
        starPowerEndCount = notes.Notes.Count(note => note.IsStarPowerEnd);

        //the kick drum should be removed, but the star power end should remain
        Assert.That(notes.Notes.Count, Is.EqualTo(2));
        Assert.That(starPowerStartCount, Is.EqualTo(1));
        Assert.That(starPowerEndCount, Is.EqualTo(1));
        Assert.That(notes.Notes.Last().NextNote, Is.Null);
    }

    private SongChart GetChartWithStarPowerSectionOfOnlyKickDrum()
    {
        return SongChartBuilder.New()
            .AddNote(FourLaneDrumPad.Kick, flags: NoteFlags.StarPowerStart)
            .AddNote(FourLaneDrumPad.Kick)
            .AddNote(FourLaneDrumPad.Kick, flags: NoteFlags.StarPowerEnd)
            .AddNote(FourLaneDrumPad.RedDrum)
            .Build();
    }

    private SongChart GetChartWithStarPowerEndingInChordWithKickDrum()
    {
        return SongChartBuilder.New()
            .AddNote(FourLaneDrumPad.Kick, flags: NoteFlags.StarPowerStart)
            .AddChildNote(FourLaneDrumPad.RedDrum)
            .AddNote(FourLaneDrumPad.Kick)
            .AddNote(FourLaneDrumPad.Kick, flags: NoteFlags.StarPowerEnd)
            .AddChildNote(FourLaneDrumPad.RedDrum)
            .Build();
    }

    private SongChart GetChartWithStarPowerEndingInKickDrum()
    {
        return SongChartBuilder.New()
            .AddNote(FourLaneDrumPad.Kick, flags: NoteFlags.StarPowerStart)
            .AddChildNote(FourLaneDrumPad.RedDrum)
            .AddNote(FourLaneDrumPad.RedDrum)
            .AddNote(FourLaneDrumPad.Kick, flags: NoteFlags.StarPowerEnd)
            .Build();
    }
}