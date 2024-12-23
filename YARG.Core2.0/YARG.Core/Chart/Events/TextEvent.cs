using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A text event that occurs in a chart.
    /// </summary>
    public class TextEvent : ChartEvent, ICloneable<TextEvent>
    {
        public string Text { get; }

        public TextEvent(string text, double time, uint tick)
            : base(time, 0, tick, 0)
        {
            Text = text;
        }

        public TextEvent(TextEvent other) : base(other)
        {
            Text = other.Text;
        }

        public TextEvent Clone()
        {
            return new(this);
        }
    }
}