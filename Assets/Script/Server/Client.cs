using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using YARG.UI;

namespace YARG.Server {
	public static class Client {
		public static FileInfo remoteCache;
		private static string remotePath;

		private static Thread thread;
		private static TcpClient client;

		public static void Start(string ip) {
			Menu.remoteMode = true;
			remotePath = Path.Combine(Application.persistentDataPath, "remote");

			// Make sure remote path exists
			var dirInfo = new DirectoryInfo(remotePath);
			if (!dirInfo.Exists) {
				dirInfo.Create();
			}

			client = new TcpClient(ip, 6145);

			thread = new Thread(ClientThread);
			thread.Start();
		}

		private static void ClientThread() {
			var stream = client.GetStream();

			// Get cache
			var send = Encoding.ASCII.GetBytes("ReqCache");
			stream.Write(send, 0, send.Length);
			stream.Flush();

			// Read cache from server
			remoteCache = new(Path.Combine(remotePath, "yarg_cache.json"));
			ReadFile(stream, remoteCache);
		}

		private static void ReadFile(NetworkStream stream, FileInfo output) {
			const int BUF_SIZE = 81920;

			// Wait until data is available
			while (!stream.DataAvailable) {
				Thread.Sleep(10);
			}

			// Get file size
			var buffer = new byte[sizeof(long)];
			stream.Read(buffer, 0, sizeof(long));
			long size = BitConverter.ToInt64(buffer);

			// Copy data to disk
			// We can't use CopyTo on a infinite stream (like NetworkStream)
			long totalRead = 0;
			var fileBuf = new byte[BUF_SIZE];
			using var fs = output.OpenWrite();
			while (totalRead < size) {
				int bytesRead = stream.Read(fileBuf, 0, BUF_SIZE);
				fs.Write(fileBuf, 0, bytesRead);
				totalRead += bytesRead;
			}
		}

		public static void Stop() {
			if (client == null) {
				return;
			}

			thread.Abort();

			// Send "End" packet
			var stream = client.GetStream();
			var send = Encoding.ASCII.GetBytes("End");
			stream.Write(send, 0, send.Length);
			stream.Flush();
			client.Close();
		}
	}
}