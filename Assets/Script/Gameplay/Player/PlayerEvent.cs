using System;

namespace YARG.Gameplay.Player
{
    public abstract record PlayerEvent
    {
        public sealed record StarPowerChanged(bool Active) : PlayerEvent;
        public sealed record ReplayTimeChanged(double Time) : PlayerEvent;
        public sealed record VisualsReset : PlayerEvent;
        public sealed record NoteHit : PlayerEvent;
        public sealed record NoteMissed : PlayerEvent;
        public sealed record SustainBroken : PlayerEvent;
        public sealed record SustainEnded : PlayerEvent;
        public sealed record WhammyDuringSustain(float WhammyFactor) : PlayerEvent;
    }
}