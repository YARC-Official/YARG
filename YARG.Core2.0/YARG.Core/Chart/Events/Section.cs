using System;

namespace YARG.Core.Chart
{
    public class Section : ChartEvent, ICloneable<Section>
    {
        public string Name { get; }

        public Section(string name, double time, uint tick) : base(time, 0, tick, 0)
        {
            Name = name;
        }

        public Section(Section other) : base(other)
        {
            Name = other.Name;
        }

        public Section Clone()
        {
            return new(this);
        }
    }
}