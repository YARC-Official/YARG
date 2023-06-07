using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace YARG.Data {
	public static class SongSources {
		public static readonly Dictionary<string, string> DEFAULT_SOURCES = new() {
			{ "yarg", "YARG" },

			{ "gh1", "Guitar Hero" },
			{ "gh", "Guitar Hero" },
			{ "gh2", "Guitar Hero II" },
			{ "gh2dlc", "Guitar Hero II DLC" },
			{ "gh80s", "Guitar Hero Encore: Rocks the 80s" },
			{ "gh3", "Guitar Hero III: Legends of Rock" },
			{ "ghot", "Guitar Hero: On Tour" },
			{ "gha", "Guitar Hero: Aerosmith" },
			{ "ghwt", "Guitar Hero: World Tour" },
			{ "ghm", "Guitar Hero: Metallica" },
			{ "ghwor", "Guitar Hero: Warriors of Rock" },
			{ "ghvh", "Guitar Hero: Van Halen" },
			{ "gh2dx", "Guitar Hero II Deluxe" },
			{ "gh2dxdlc", "Guitar Hero Rocks the 360" },
			{ "gh80sdx", "Guitar Hero Encore Deluxe" },
			{ "gh3dlc", "Guitar Hero III DLC" },
			{ "ghwtdlc", "Guitar Hero: World Tour DLC" },
			{ "ghmdlc", "Death Magnetic DLC" },
			{ "djhero", "DJ Hero" },
			{ "ghsh", "Guitar Hero: Smash Hits" },
			{ "ghwordlc", "Guitar Hero: Warriors of Rock DLC" },
			{ "gh5", "Guitar Hero 5" },
			{ "gh5dlc", "Guitar Hero 5 DLC" },
			{ "ghotd", "Guitar Hero On Tour: Decades" },
			{ "ghotmh", "Guitar Hero On Tour: Modern Hits" },
			{ "bandhero", "Band Hero" },
			{ "bh", "Band Hero" },
			{ "bhds", "Band Hero DS" },
			{ "ghl", "Guitar Hero Live" },
			{ "ghtv", "Guitar Hero TV" },

			{ "rb1", "Rock Band 1" },
			{ "rb2", "Rock Band 2" },
			{ "rb3", "Rock Band 3" },
			{ "rb4", "Rock Band 4" },
			{ "tbrb", "The Beatles Rock Band" },
			{ "beatles", "The Beatles Rock Band" },
			{ "tbrbdlc", "The Beatles: Rock Band DLC" },
			{ "tbrbcdlc", "The Beatles: Rock Band Custom DLC Project" },
			{ "rbacdc", "AC/DC Live: Rock Band Track Pack" },
			{ "lrb", "Lego Rock Band" },
			{ "lego", "Lego Rock Band" },
			{ "rbn", "Rock Band Network" },
			{ "ugc", "Rock Band Network 1.0" },
			{ "ugc_plus", "Rock Band Network 2.0" },
			{ "ugc1", "Rock Band Network 1.0" },
			{ "ugc2", "Rock Band Network 2.0" },
			{ "ugc_lost", "Lost Rock Band Network" },
			{ "rb1_dlc", "Rock Band 1 DLC" },
			{ "rb1dlc", "Rock Band 1 DLC" },
			{ "rb2_dlc", "Rock Band 2 DLC" },
			{ "rb2dlc", "Rock Band 2 DLC" },
			{ "rb3_dlc", "Rock Band 3 DLC" },
			{ "rb3dlc", "Rock Band 3 DLC" },
			{ "rb4_dlc", "Rock Band 4 DLC" },
			{ "rb4dlc", "Rock Band 4 DLC" },
			{ "rb4_rivals", "Rock Band Rivals" },
			{ "rbtp_acdc", "Rock Band Track Pack: AC/DC Live" },
			{ "rbtp_classic_rock", "Rock Band Track Pack: Classic Rock" },
			{ "rbtp_country_1", "Rock Band Track Pack: Country 1" },
			{ "rbtp_country_2", "Rock Band Track Pack: Country 2" },
			{ "rbtp_metal", "Rock Band Track Pack: Metal" },
			{ "rbtp_vol_1", "Rock Band Track Pack: Volume 1" },
			{ "rbtp_vol_2", "Rock Band Track Pack: Volume 2" },
			{ "rb_blitz", "Rock Band Blitz" },
			{ "pearljam", "Pearl Jam: Rock Band" },
			{ "greenday", "Green Day: Rock Band" },
			{ "gdrb", "Green Day: Rock Band" },
			{ "rbvr", "Rock Band VR" },
			{ "rbr", "Rock Band Rivals" },

			{ "311hero", "311 Hero" },
			{ "a2z", "A-Z Pack" },
			{ "ah1", "Angevil Hero" },
			{ "ah2", "Angevil Hero 2" },
			{ "ah3", "Angevil Hero 3" },
			{ "ah4", "Angevil Hero 4" },
			{ "antihero", "Anti Hero" },
			{ "ahbe", "Anti Hero - Beach Episode" },
			{ "antihero2", "Anti Hero 2" },
			{ "a7xmegapack", "Avenged Sevenfold Mega Pack" },
			{ "bitcrusher", "BITCRUSHER" },
			{ "bitcrusherdlc", "BITCRUSHER DLC" },
			{ "blackhole", "Black Hole" },
			{ "bs", "Blanket Statement" },
			{ "bleepbloops", "Bleep Bloops" },
			{ "bleepbloopuc", "Bleep Bloop Undercharts" },
			{ "ugc_c3", "C3 Customs" },
			{ "c3customs", "C3 Customs" },
			{ "c3legacy", "C3 Legacy" },
			{ "cth1", "Carpal Tunnel Hero" },
			{ "cth1r", "Carpal Tunnel Hero 1: Remastered" },
			{ "cth2", "Carpal Tunnel Hero 2" },
			{ "cth3", "Carpal Tunnel Hero 3" },
			{ "cth3dlc", "Carpal Tunnel Hero 3 DLC" },
			{ "charts", "CHARTS" },
			{ "charts2", "CHARTS 2" },
			{ "chelhero", "Chel Hero" },
			{ "cb", "Circuit Breaker" },
			{ "ch", "Clone Hero" },
			{ "codered", "Code Red" },
			{ "comtpi", "Community Track Pack I" },
			{ "comtpii", "Community Track Pack II" },
			{ "comtpiii", "Community Track Pack III" },
			{ "comtpiv", "Community Track Pack IV" },
			{ "comtp45", "Community Track Pack 4.5" },
			{ "comtpv", "Community Track Pack V" },
			{ "cowhero", "Cow Hero" },
			{ "cowherodlc1", "Cow Hero DLC 1 - Bull Conqueror" },
			{ "cowherodlc2", "Cow Hero DLC 2 - Bovine Champion" },
			{ "cowherodlc3", "Cow Hero DLC 3 - Cattle Guardian" },
			{ "creativech", "Creative Commons Hero" },
			{ "csc", "Custom Songs Central" },
			{ "customs", "Custom Songs" },
			{ "digi", "Digitizer" },
			{ "dissonancehero", "Dissonance Hero" },
			{ "djenthero", "Djent Hero" },
			{ "dhc", "Djent Hero Collection" },
			{ "djentherodlc", "Djent Hero DLC" },
			{ "facelift", "Facelift" },
			{ "tfoth", "The Fall of Troy Hero" },
			{ "fp", "Focal Point" },
			{ "fp2", "Focal Point 2" },
			{ "fp3", "Focal Point 3" },
			{ "fof", "Frets on Fire" },
			{ "fuse", "Fuse Box" },
			{ "gd", "GITADORA" },
			{ "gf1", "GuitarFreaks" },
			{ "gf2dm1", "GuitarFreaks 2ndMIX & DrumMania" },
			{ "addygh", "Guitar Hero II: Addy's Disc" },
			{ "ghtorb3", "Guitar Hero To Rock Band 3" },
			{ "ghxsetlist", "Guitar Hero X" },
			{ "ghx2setlist", "Guitar Hero X-II" },
			{ "praise", "Guitar Praise" },
			{ "praisedlc", "Guitar Praise: Expansion Pack 1" },
			{ "stryper", "Guitar Praise: Stryper" },
			{ "guitarzero2", "Guitar Zero 2" },
			{ "guitarherodlc", "Guitar Zero 2 DLC" },
			{ "harmonyhero", "Harmony Hero" },
			{ "imetal", "Instru-Metal" },
			{ "jrb", "J-Rock Band" },
			{ "kh", "Koreaboo Hero" },
			{ "kh2", "Koreaboo Hero 2" },
			{ "marathon", "Marathon Hero" },
			{ "marathonhero2", "Marathon Hero 2" },
			{ "ma", "Max Altitude" },
			{ "meme", "Meme Songs" },
			{ "milohax", "MiloHax Customs" },
			{ "miscellaneous", "Miscellaneous Packs" },
			{ "paradigm", "Paradigm" },
			{ "paramoremegapack", "Paramore Mega Pack" },
			{ "phaseshift", "Phase Shift" },
			{ "psgp4", "Phase Shift Guitar Project 4" },
			{ "psh", "Plastic Shred Hero: Legends of Apahetic Charting" },
			{ "psh2", "Plastic Shred Hero 2" },
			{ "pg", "PowerGig: Rise of the SixString" },
			{ "pgdlc", "PowerGig: Rise of the SixString DLC" },
			{ "ph1", "Puppetz Hero I" },
			{ "ph2", "Puppetz Hero II" },
			{ "ph3", "Puppetz Hero III" },
			{ "ph4", "Puppetz Hero IV" },
			{ "ragequit", "Rage Quit" },
			{ "ra", "Redemption Arc" },
			{ "revolved", "REVOLVED" },
			{ "rr", "Rock Revolution" },
			{ "rrdlc", "Rock Revolution DLC" },
			{ "scorespy", "ScoreSpy" },
			{ "s_hero", "S Hero" },
			{ "solomedley", "Solo Medleys" },
			{ "finnish", "Suomibiisit" },
			{ "se", "Symphonic Effect" },
			{ "sxdisc", "Symphony X Discography Setlist" },
			{ "synergy", "Synergy" },
			{ "vortex_hero", "Vortex Hero" },
			{ "wcc", "World Charts Community" },
			{ "zancharted", "Zancharted" },
			{ "zerogravity", "Zero Gravity" },
			{ "zgsb", "Zero Gravity - Space Battle" },
		};

		/// <value>
		/// The URL of the Clone Hero sources list.
		/// </value>
		private const string SOURCES_URL = "https://sources.clonehero.net/sources.txt";

		/// <value>
		/// The location of the local sources file.
		/// </value>
		private static string SourcesFile => Path.Combine(GameManager.PersistentDataPath, "sources.txt");

		/// <value>
		/// A dictionary of source IDs to source names.<br/>
		/// You must call <see cref="FetchSourcesFromWeb"/> first.
		/// </value>
		private static Dictionary<string, string> webSourceNames = null;

		private static async UniTask FetchSourcesFromWeb() {
			try {
				// Retrieve sources file
				var request = WebRequest.Create(SOURCES_URL);
				request.UseDefaultCredentials = true;
				request.Timeout = 5000;

				// Send the request and wait for the response
				using var response = await request.GetResponseAsync();

				// Store sources locally and load them
				using var fileWriter = File.Create(SourcesFile);
				await response.GetResponseStream().CopyToAsync(fileWriter);
			} catch (Exception e) {
				Debug.LogException(e);
			}
		}

		private static async UniTask ReadSources() {
			// Skip if the sources file doesn't exist
			if (!File.Exists(SourcesFile)) {
				return;
			}

			webSourceNames ??= new();

			var sources = (await File.ReadAllTextAsync(SourcesFile)).Split("\n");
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

				webSourceNames.Add(pair[0].Trim(), pair[1].Trim());
			}
		}

		public static async UniTask LoadSources() {
			await FetchSourcesFromWeb();
			await ReadSources();
		}

		/// <returns>
		/// The converted short name (e.g. gh1) into the game name (e.g. Guitar Hero 1).
		/// </returns>
		public static string SourceToGameName(string source) {
			if (string.IsNullOrEmpty(source)) {
				return "Unknown Source";
			}

			// Try get from web sources
			if (webSourceNames != null && webSourceNames.TryGetValue(source, out string name)) {
				return name;
			}

			// If not, get from default sources
			if (DEFAULT_SOURCES.TryGetValue(source, out name)) {
				return name;
			}

			return "Unknown Source";
		}
	}
}
