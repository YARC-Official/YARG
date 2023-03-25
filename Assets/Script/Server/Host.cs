using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using YARG.Settings;
using YARG.Util;

namespace YARG.Server {
	public partial class Host : MonoBehaviour {
		private List<Thread> threads = new();
		private int connectionCount = 0;
		private TcpListener server;

		private void Start() {
			// Lower graphics to save power or something
			SettingsManager.SetSettingValue("lowQuality", true);
			Application.targetFrameRate = 5;

			// Fetch songs and scores first so we have a cache file to send
			SongLibrary.FetchSongs();
			ScoreManager.FetchScores();

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
						var str = Encoding.UTF8.GetString(bytes, 0, size);
						Log($"Received: `{str}`.");

						// Do something
						if (str == "ReqEnd") {
							Log("Requesting client data...");
							Send(stream, "ReqInfoPkgThenEnd");

							// Wait for file on the stream
							while (true) {
								if (stream.DataAvailable) {
									string name = GetUniqueZipName();

									// Read zipped info package from server
									Utils.ReadFile(stream, new(name));

									// When done, dump all files into local path
									ZipFile.ExtractToDirectory(name, SongLibrary.SongFolder, true);

									// Delete zip
									File.Delete(name);

									break;
								}

								// Prevent CPU burn
								Thread.Sleep(10);
							}

							Log("Synced client data.");

							// All done!
							break;
						} else if (str == "ReqInfoPkg") {
							string name = GetUniqueZipName();

							// Zip up yarg_cache, yarg_score, etc.
							Utils.CreateZipFromFiles(name, SongLibrary.CacheFile, ScoreManager.ScoreFile);

							// Send it over
							var info = new FileInfo(name);
							SendFile(stream, info);

							// Delete temp
							info.Delete();

							Log("Sent info package to client.");
						} else if (str.StartsWith("ReqSong,")) {
							// Get the folder
							string path = str[8..];

							// See if valid
							if (!path.StartsWith(SongLibrary.SongFolder.ToUpperInvariant())) {
								Log($"<color=yellow>Kicking client for foreign path `{path}`.</color>");
								connectionCount--;
								return;
							}

							string name = GetUniqueZipName();

							// Zip up folder
							ZipFile.CreateFromDirectory(path, name);

							// Send it over
							var info = new FileInfo(name);
							SendFile(stream, info);

							// Delete temp
							info.Delete();
						} else if (str.StartsWith("ReqAlbumCover,")) {
							// Get the folder
							string path = str[14..];

							// See if valid
							if (!path.StartsWith(SongLibrary.SongFolder.ToUpperInvariant())) {
								Log($"<color=yellow>Kicking client for foreign path `{path}`.</color>");
								connectionCount--;
								return;
							}

							// Send album cover over
							var info = new FileInfo(Path.Combine(path, "album.png"));
							if (info.Exists) {
								SendFile(stream, info);
							} else {
								SendNoFile(stream);
							}
						}
					} else {
						// Prevent CPU burn
						Thread.Sleep(100);
					}
				} catch (Exception e) {
					Log($"<color=red>Error: {e.Message}</color>");
				}
			}

			Log("<color=yellow>Client disconnected.</color>");
			connectionCount--;
		}

		private static string GetUniqueZipName() {
			string name = $"temp_{Thread.CurrentThread.ManagedThreadId}.zip";
			name = Path.Combine(SongLibrary.SongFolder, name);
			File.Delete(name);

			return name;
		}

		private void SendFile(NetworkStream stream, FileInfo file) {
			Log($"Sending file `{file.FullName}`...");
			using var fs = file.OpenRead();

			// Send file size
			stream.Write(BitConverter.GetBytes(fs.Length));

			// Send file itself
			fs.CopyTo(stream);
		}

		private void SendNoFile(NetworkStream stream) {
			// Send over a size of zero
			stream.Write(BitConverter.GetBytes(0));
		}

		private void Send(NetworkStream stream, string str) {
			var send = Encoding.UTF8.GetBytes(str);
			stream.Write(send, 0, send.Length);
			stream.Flush();
		}

		private void OnDestroy() {
			foreach (var thread in threads) {
				thread.Abort();
			}
			server.Stop();
		}
	}
}