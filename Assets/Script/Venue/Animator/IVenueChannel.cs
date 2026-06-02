using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    /// <summary>
    /// A self-contained handler for one "lane" of venue animation.
    /// Chart-driven channels populate the command queue on load.
    /// Reactive channels override Update() instead.
    /// </summary>
    public interface IVenueChannel
    {
        /// <summary>
        /// Called once after chart load. Chart-driven channels should add all
        /// their commands to the queue here and do nothing in Update.
        /// </summary>
        void BuildCommands(SongChart chart, AnimatorCommandQueue queue);

        /// <summary>
        /// Called every frame for reactive channels (happiness, crowd).
        /// Chart-driven channels can leave this empty.
        /// </summary>
        void Update(double visualTime);
    }
}