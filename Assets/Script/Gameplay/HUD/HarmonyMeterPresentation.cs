// pattern: Functional Core
namespace YARG.Gameplay.HUD
{
    public readonly struct HarmonyMeterState
    {
        public readonly bool Active;
        public readonly bool Warning;
        public readonly float Fill;
        public readonly float Pulse;

        public HarmonyMeterState(bool active, bool warning, float fill, float pulse)
        {
            Active = active;
            Warning = warning;
            Fill = fill;
            Pulse = pulse;
        }
    }

    public static class HarmonyMeterPresentation
    {
        public static HarmonyMeterState Evaluate(bool current, bool next, double progress,
            double rawFill, double threshold, double strongBeatPhase)
        {
            if (current)
            {
                double scale = threshold > 0 && !double.IsInfinity(threshold) ? threshold : 1;
                return new HarmonyMeterState(true, false, Clamp01(rawFill / scale), 0);
            }
            if (!next) return default;

            float phraseProgress = Clamp01(progress);
            float pulse = 1 - Clamp01(strongBeatPhase);
            return new HarmonyMeterState(false, true, 1 - phraseProgress, pulse * phraseProgress);
        }

        public static float AdvanceFill(float previousFill, bool previouslyActive,
            HarmonyMeterState state, float deltaTime)
        {
            if (!state.Active) return state.Fill;

            // A countdown is not earned credit. Discard it before easing into live progress.
            float start = previouslyActive ? Clamp01(previousFill) : 0;
            float amount = Clamp01(deltaTime * 12);
            return start + (state.Fill - start) * amount;
        }

        private static float Clamp01(double value)
        {
            if (double.IsNaN(value)) return 0;
            return (float) System.Math.Max(0, System.Math.Min(1, value));
        }
    }
}