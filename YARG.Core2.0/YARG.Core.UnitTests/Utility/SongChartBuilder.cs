using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Utility;

public class SongChartBuilder
{
    public static SongChartBuilder New(uint resolution = 480)
    {
        return new SongChartBuilder(resolution);
    }

    private readonly SongChart        _chart;
    private readonly List<DrumNote>   _drumNotes;
    private readonly List<GuitarNote> _guitarNotes;

    private double _currentTime;
    private uint   _currentTick;

    private SongChartBuilder(uint resolution)
    {
        _chart = new SongChart(resolution);
        _currentTime = 0;
        _currentTick = 0;

        var drumTrack = new InstrumentTrack<DrumNote>(Instrument.ProDrums);
        _drumNotes = new List<DrumNote>();
        var difficulty = Difficulty.Expert;
        drumTrack.AddDifficulty(difficulty, new InstrumentDifficulty<DrumNote>(Instrument.ProDrums, difficulty, _drumNotes, new(), new()));

        var guitarTrack = new InstrumentTrack<GuitarNote>(Instrument.FiveFretGuitar);
        _guitarNotes = new List<GuitarNote>();
        guitarTrack.AddDifficulty(difficulty, new InstrumentDifficulty<GuitarNote>(Instrument.FiveFretGuitar, difficulty, _guitarNotes, new(), new()));

        _chart.FiveFretGuitar = guitarTrack;
        _chart.FourLaneDrums = drumTrack;
        _chart.ProDrums = drumTrack;
        _chart.FiveLaneDrums = drumTrack;
    }

    public SongChartBuilder AddNote(FourLaneDrumPad pad, DrumNoteType noteType = DrumNoteType.Neutral, DrumNoteFlags drumFlags = DrumNoteFlags.None,
        NoteFlags flags = NoteFlags.None, double? time = null)
    {
        var drumNote = new DrumNote(pad, noteType, drumFlags, flags, time ?? _currentTime, _currentTick);
        _drumNotes.Add(drumNote);

        _currentTime++;
        _currentTick++;
        return this;
    }

    public SongChartBuilder AddChildNote(FourLaneDrumPad pad)
    {
        if (_drumNotes.Count == 0) throw new InvalidOperationException("You're trying to add a child note to a chart with no notes.");
        var lastNote = _drumNotes.Last();
        var drumNote = new DrumNote(pad, lastNote.Type, lastNote.DrumFlags, lastNote.Flags, lastNote.Time, lastNote.Tick);
        lastNote.AddChildNote(drumNote);
        return this;
    }

    public SongChartBuilder AddNote(FiveFretGuitarFret fret, GuitarNoteType noteType = GuitarNoteType.Strum, GuitarNoteFlags guitarFlags = GuitarNoteFlags.None,
        NoteFlags flags = NoteFlags.None, double? time = null)
    {
        var guitarNote = new GuitarNote(fret, noteType, guitarFlags, flags, time ?? _currentTime, 1, _currentTick, 1);
        _guitarNotes.Add(guitarNote);

        _currentTime++;
        _currentTick++;
        return this;
    }

    public SongChartBuilder AddChildNote(FiveFretGuitarFret fret)
    {
        if (_guitarNotes.Count == 0) throw new InvalidOperationException("You're trying to add a child note to a chart with no notes.");
        var lastNote = _guitarNotes.Last();
        var guitarNote = new GuitarNote(fret, lastNote.Type, lastNote.GuitarFlags, lastNote.Flags, lastNote.Time, lastNote.TimeLength, lastNote.Tick, lastNote.TickLength);
        lastNote.AddChildNote(guitarNote);
        return this;
    }

    public SongChart Build()
    {
        return _chart;
    }
}