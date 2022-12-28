using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace YARG.Server {
	public partial class Host : MonoBehaviour {
		private List<Thread> threads = new();
		private int connectionCount = 0;
		private TcpListener server;

		private void Start() {
			// Lower graphics to save power or something
			GameManager.Instance.LowQualityMode = true;
			QualitySettings.vSyncCount = 0;
			Application.targetFrameRate = 5;

			// Fetch songs first so we have a cache file to send
			SongLibrary.FetchSongs();

			// Create the TcpListener
			server = new TcpListener(IPAddress.Any, 6145);
			server.Start();
			Log("Opened server on localhost:6145.");

			// Accept TcpClient(s)
			server.BeginAcceptTcpClient(AcceptTcpClient, server);
			Log("Waiting for clients...");
		}

		private void AcceptTcpClient(IAsyncResult result) {
			TcpClient client = server.EndAcceptTcpClient(result);

			if (connectionCount >= 5) {
				Log("<color=red>Max connection count reached (5).</color>");
				return;
			}

			// Create a thread for this connection
			var thread = new Thread(() => ServerThread(client));
			thread.Start();
			threads.Add(thread);
			connectionCount++;

			server.BeginAcceptTcpClient(AcceptTcpClient, server);
		}

		private void ServerThread(TcpClient client) {
			Log("<color=green>Client accepted!</color>");

			var stream = client.GetStream();
			while (true) {
				try {
					if (stream.DataAvailable) {
						// Get data from client
						byte[] bytes = new byte[1024];
						int size = stream.Read(bytes, 0, bytes.Length);

						// Get request
						var str = System.Text.Encoding.UTF8.GetString(bytes, 0, size);
						Log($"Received: `{str}`.");

						// Do something
						if (str == "End") {
							break;
						} else if (str == "ReqCache") {
							SendFile(stream, SongLibrary.CacheFile);
						} else if (str.StartsWith("ReqSong,")) {
							// Get the folder
							string path = str[8..^0];

							// See if valid
							if (!path.StartsWith(SongLibrary.SONG_FOLDER.FullName)) {
								return;
							}

							// Create unique temp file name
							string name = $"temp_{Thread.CurrentThread.ManagedThreadId}.zip";
							name = Path.Combine(SongLibrary.SONG_FOLDER.FullName, name);

							// Zip up folder
							ZipFile.CreateFromDirectory(path, name);

							// Send it over
							var info = new FileInfo(name);
							SendFile(stream, info);

							// Delete temp
							info.Delete();
						}
					} else {
						// Prevent CPU burn
						Thread.Sleep(250);
					}
				} catch (Exception e) {
					Log($"<color=red>Error: {e.Message}</color>");
				}
			}

			Log("<color=yellow>Client disconnected.</color>");
			connectionCount--;
		}

		private void SendFile(NetworkStream stream, FileInfo file) {
			Log($"Sending file `{file.FullName}`...");
			using var fs = file.OpenRead();

			// Send file size
			stream.Write(BitConverter.GetBytes(fs.Length));

			// Send file itself
			fs.CopyTo(stream);
		}

		private void OnDestroy() {
			foreach (var thread in threads) {
				thread.Abort();
			}
			server.Stop();
		}
	}
}