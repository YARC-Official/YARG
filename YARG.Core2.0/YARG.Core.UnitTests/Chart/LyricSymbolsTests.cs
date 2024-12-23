using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Chart;

public class LyricSymbolsTests
{
    private const string LYRICS_ALLOWED_TAGS = """
        <b>The</b> <i>quick</i> <color=#774331>brown</color> <u>fox</u><br>
        <sup>jumped</sup> <voffset=1em>over</voffset> <lowercase>THE</lowercase> <sub>lazy</sub> <s>dog</s>.<br>
        <mspace=2em>Afterwards</mspace>, <cspace=1em>it let out a</cspace> <smallcaps>hearty</smallcaps> <uppercase>scream</uppercase>.
        """;

    private const string VOCALS_TAGS_STRIPPED = """
        The quick brown fox
        jumped over THE lazy dog.
        Afterwards, it let out a hearty scream.
        """;

    private const string DISALLOWED_TAGS = """
        <align="right"><margin=5em>Lorem <allcaps>ipsum</allcaps> <alpha=#80>dolor</alpha> <font="NotoSans">sit</font> <font-weight=100>amet</font-weight></margin></align>,
        <indent=15%><gradient="someGradient">consectetur</gradient> <link="someLink">adipiscing</link> <mark=#ffff00aa>elit</mark>.</indent>
        <line-height=50%><rotate=15.0>Quisque</rotate> <size=50%>ut</size> <space=2em>orci</space> <sprite="someSprite">nec</sprite> <style="Title">neque</style> fringilla pulvinar a id nulla.</line-height>
        <line-indent=15%>Quisque facilisis ut lorem vestibulum rhoncus.
        <width=50%>Aenean in volutpat elit.</width></line-indent>
        <pos=75%><nobr>Sed iaculis</nobr>, ante vel ultricies tristique,</pos>
        <page>
        <noparse><><<easrt>>a><uy></noparse>
        """;

    private const string DISALLOWED_TAGS_REMOVED = """
        Lorem ipsum dolor sit amet,
        consectetur adipiscing elit.
        Quisque ut orci nec neque fringilla pulvinar a id nulla.
        Quisque facilisis ut lorem vestibulum rhoncus.
        Aenean in volutpat elit.
        Sed iaculis, ante vel ultricies tristique,
        
        <><<easrt>>a><uy>
        """;

    [Test]
    public void LyricsTagStripping()
    {
        string allowed = LyricSymbols.StripForLyrics(LYRICS_ALLOWED_TAGS);
        Assert.That(allowed, Is.EqualTo(LYRICS_ALLOWED_TAGS));

        string disallowed = LyricSymbols.StripForLyrics(DISALLOWED_TAGS);
        Assert.That(disallowed, Is.EqualTo(DISALLOWED_TAGS_REMOVED));
    }

    [Test]
    public void VocalsTagStripping()
    {
        string allowed = LyricSymbols.StripForVocals(LYRICS_ALLOWED_TAGS);
        Assert.That(allowed, Is.EqualTo(VOCALS_TAGS_STRIPPED));

        string disallowed = LyricSymbols.StripForLyrics(DISALLOWED_TAGS);
        Assert.That(disallowed, Is.EqualTo(DISALLOWED_TAGS_REMOVED));
    }
}