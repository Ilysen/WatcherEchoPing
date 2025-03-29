using BepInEx;
using MoreSlugcats;
using System;
using System.Linq;

namespace Ilysen.WatcherEchoPing
{
	[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
	public class WatcherEchoPingMain : BaseUnityPlugin
	{
		public const string PLUGIN_GUID = "ilysen.watcherechoping";
		public const string PLUGIN_NAME = "Watcher Echo Ping";
		public const string PLUGIN_VERSION = "0.1";

		private readonly bool DEBUG = false;

		/// <summary>
		/// The string ID of the last region that was pinged.
		/// </summary>
		public static string lastPingRegion = null;

		/// <summary>
		/// If true, the game will attempt to run a ping as soon as it gets the chance.
		/// This is used to defer the animation until after the game has loaded, or after a wormhole animation has fully played out.
		/// </summary>
		public static bool queuedPing = false;

		/// <summary>
		/// Used in conjunction with <c><see cref="queuedPing"/></c> to delay the ping animation until after the game has had time to load.
		/// </summary>
		public static float shelterTimer = 0f;

		private void OnEnable()
		{
			try
			{
				On.SaveState.LoadGame += ResetValues;
				On.Player.WatcherUpdate += WatcherUpdateHook;
				Logger.LogInfo($"Loaded {PLUGIN_NAME} version {PLUGIN_VERSION}.");
			}
			catch (Exception e)
			{
				Logger.LogInfo($"{PLUGIN_NAME} version {PLUGIN_VERSION} caught an error while initializing!!");
				Logger.LogError(e);
			}
		}

		private void OnDisable()
		{
			On.SaveState.LoadGame -= ResetValues;
			On.Player.WatcherUpdate -= WatcherUpdateHook;
		}

		/// <summary>
		/// Resets the mod's interval values after the game is loaded.
		/// This makes sure that the ping runs between deaths, etc.
		/// </summary>
		private void ResetValues(On.SaveState.orig_LoadGame orig, SaveState self, string str, RainWorldGame game)
		{
			lastPingRegion = null;
			queuedPing = false;
			shelterTimer = 0f;
			orig(self, str, game);
		}

		/// <summary>
		/// Main logic hook into <c><see cref="Player.WatcherUpdate"/></c>.
		/// </summary>
		private void WatcherUpdateHook(On.Player.orig_WatcherUpdate orig, Player self)
		{
			if (queuedPing)
			{
				if (self.room != null && !Watcher.WarpPoint.WarpInProgress)
				{
					shelterTimer = self.room.shelterDoor == null ? 1f : shelterTimer + UnityEngine.Time.deltaTime;
					if (shelterTimer >= 1f)
					{
						self.room.AddObject(new GhostPing(self.room));
						shelterTimer = 0f;
						queuedPing = false;
						LogInfo("Ping created.");
					}
				}
			}
			else
			{
				string regionName = self.room?.world?.region?.name;
				if (lastPingRegion != regionName &&
					!regionName.IsNullOrWhiteSpace() &&
					self.SlugCatClass == Watcher.WatcherEnums.SlugcatStatsName.Watcher &&
					self.abstractCreature.world.game.IsStorySession)
				{
					LogInfo($"Searching region: {regionName} (last ping: {(lastPingRegion.IsNullOrWhiteSpace() ? "null" : lastPingRegion)})");
					lastPingRegion = regionName;
					var ghostPresence = self.room.world.spinningTopPresences.FirstOrDefault(x => x.ghostRoom.world.region.name == regionName);
					if (ghostPresence != default)
					{
						LogInfo("Positive match!");
						LogInfo($"Room ID: {ghostPresence.ghostRoom.name}");
						LogInfo($"Room region: {ghostPresence.ghostRoom.world.region.name}");
						if (!self.room.game.GetStorySession.saveState.deathPersistentSaveData.spinningTopEncounters.Contains(ghostPresence.spinningTopSpawnId))
						{
							queuedPing = true;
							LogInfo("Prepping a ping.");
						}
						else
							LogInfo("...but we've already encountered it, so we aren't pinging it.");
					}
					else
					{
						LogInfo($"No echo is present in this region.");
					}
				}
			}
			orig(self);
		}

		private void LogInfo(string content)
		{
			if (DEBUG)
				Logger.LogInfo(content);
		}
	}
}
