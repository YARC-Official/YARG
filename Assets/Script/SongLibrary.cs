using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Data;
using YARG.Serialization;
using YARG.Settings;

namespace YARG {
	public static class SongLibrary {

		/// <value>
		/// The location of the local sources file.
		/// </value>
		public static string SourcesFile => Path.Combine(GameManager.PersistentDataPath, "sources.txt");

		/// <value>
		/// The URL of the Clone Hero sources list.
		/// </value>
		public const string SOURCES_URL = "https://sources.clonehero.net/sources.txt";

		/// <value>
		/// A list of all of the playable songs, where keys are hashes.<br/>
		/// You must call <see cref="FetchSongSources"/> first.
		/// </value>
		public static Dictionary<string, string> SourceNames { get; private set; } = null;

		private static bool FetchSources() {
			try {
				// Retrieve sources file
				var request = WebRequest.Create(SOURCES_URL);
				request.UseDefaultCredentials = true;
				request.Timeout = 5000;
				using var response = request.GetResponse();
				using var responseReader = new StreamReader(response.GetResponseStream());

				// Store sources locally and load them
				string text = responseReader.ReadToEnd();
				File.WriteAllText(SourcesFile, text);
			} catch (Exception e) {
				Debug.LogException(e);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Reads the locally-cached sources file.<br/>
		/// Populates <see cref="SourceNames"/>
		/// </summary>
		private static bool ReadSources() {
			if (!File.Exists(SourcesFile)) {
				return false;
			}

			SourceNames ??= new();
			SourceNames.Clear();
			var sources = File.ReadAllText(SourcesFile).Split("\n");
			foreach (string source in sources) {
				if (string.IsNullOrWhiteSpace(source)) {
					continue;
				}

				// The sources are formatted as follows:
				// iconName '=' Display Name
				var pair = source.Split("'='", 2);
				if (pair.Length < 2) {
					Debug.LogWarning($"Invalid source entry when reading sources: {source}");
					continue;
				}
				SourceNames.Add(pair[0].Trim(), pair[1].Trim());
			}

			return true;
		}
	}
}