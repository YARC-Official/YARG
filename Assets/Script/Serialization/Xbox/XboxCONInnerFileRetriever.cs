using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YARG.Serialization {
	public static class XboxCONInnerFileRetriever {
		public static async UniTask<byte[]> RetrieveFile(string location, uint filesize, uint[] fileOffsets, CancellationToken? ct) {
			byte[] f = new byte[filesize];

			await UniTask.RunOnThreadPool(() => {
				uint lastSize = filesize % 0x1000;

				for (int i = 0; i < fileOffsets.Length; i++) {
					ct?.ThrowIfCancellationRequested();

					// TODO: This is unoptimized
					uint ReadLen = (i == fileOffsets.Length - 1) ? lastSize : 0x1000;
					using var fs = new FileStream(location, FileMode.Open, FileAccess.Read);
					using var br = new BinaryReader(fs, new ASCIIEncoding());
					fs.Seek(fileOffsets[i], SeekOrigin.Begin);
					Array.Copy(br.ReadBytes((int) ReadLen), 0, f, i * 0x1000, (int) ReadLen);
				}
			});

			return f;
		}

		public static byte[] RetrieveFile(string location, uint filesize, uint[] fileOffsets) {
			return RetrieveFile(location, filesize, fileOffsets, null).AsTask().Result;
		}
	}
}