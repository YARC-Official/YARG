using System.Collections.Generic;
using System.Linq;
using YARG.Core.Input;

namespace YARG.Input
{
    /// <summary>
    /// A button binding which sends press events for each control individually.
    /// </summary>
    public class DrumPadButtonBinding : IndividualButtonBinding
    {
        public DrumPadButtonBinding(string name, int action) : base(name, action)
        {
        }
        public DrumPadButtonBinding(string name, string nameLefty, int action) : base(name, nameLefty, action)
        {
        }

        protected override void FireInputEvent(double time, bool wasPressed)
        {
            //convert Boolean on/off inputs to float values from the pressed SingleBinding
            float pressVelocity = 0;

            if (wasPressed)
            {
                pressVelocity = _bindings.Max(binding => binding.Control.value);
            }

            base.FireInputEvent(time, pressVelocity);
        }
    }
}