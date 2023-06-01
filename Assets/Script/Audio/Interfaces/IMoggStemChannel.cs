using System.Collections.Generic;

namespace YARG {
	public interface IMoggStemChannel : IStemChannel {

		public IReadOnlyList<IStemChannel> Channels { get; }

	}
}
