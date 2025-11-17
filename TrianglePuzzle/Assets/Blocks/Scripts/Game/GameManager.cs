using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if BBG_MT_ADS || BBG_MT_IAP
using BBG.MobileTools;
#endif

namespace BBG.Blocks
{
	public class GameManager : SaveableManager<GameManager>
	{
		#region Inspector Variables

		[Header("Data")]
		[SerializeField] private List<BundleInfo>	bundleInfos			= null;
		[SerializeField] private int				numLevelsForGift	= 25;
		[SerializeField] private int				numCoinsPerGift		= 50;
		[SerializeField] private int				hintCoinCost		= 50;

		[Header("Ads")]
		[SerializeField] private int				numLevelsBetweenAds	= 0;
		[SerializeField] private int				minTimeBetweenAds	= 0;	// Amount of time that must pass since the last interstitial ad before the next interstitial ad is shown

		[Header("Debug")]
		[SerializeField] private bool				unlockAllPacks	= false;	// Sets all packs to be unlocked
		[SerializeField] private bool				unlockAllLevels	= false;	// Sets all levels to be unlocked (does not unlock packs)
		[SerializeField] private bool				freeHints		= false;	// Hints won't deduct coins

		#endregion

		#region Member Variables

		private HashSet<string>						unlockedPacks;
		private Dictionary<string, int>				packLastCompletedLevel;
		private Dictionary<string, LevelSaveData>	levelSaveDatas;

		#endregion

		#region Properties

		public override string SaveId { get { return "game"; } }

		public List<BundleInfo>	BundleInfos			{ get { return bundleInfos; } }
		public PackInfo			ActivePackInfo		{ get; private set; }
		public LevelData		ActiveLevelData		{ get; private set; }
		public LevelSaveData	ActiveLevelSaveData	{ get; private set; }
		public int				NumLevelsTillAd		{ get; private set; }
		public double			LastAdTimestamp		{ get; private set; }

		public bool IsLevelDataAvailable { get { return ActiveLevelData != null && ActiveLevelSaveData != null; } }

		#endregion

		#region Unity Methods

		protected override void Awake()
		{
			base.Awake();

			unlockedPacks			= new HashSet<string>();
			packLastCompletedLevel	= new Dictionary<string, int>();
			levelSaveDatas			= new Dictionary<string, LevelSaveData>();

			InitSave();
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.S))
			{
				ScreenCapture.CaptureScreenshot(Application.dataPath + "/" + (long)Utilities.SystemTimeInMilliseconds + ".png");
			}
		}

		#endregion

		#region Public Variables

		/// <summary>
		/// Starts the level.
		/// </summary>
		public void StartLevel(PackInfo packInfo, LevelData levelData)
		{
			ActivePackInfo		= packInfo;
			ActiveLevelData		= levelData;
			ActiveLevelSaveData	= GetLevelSaveData(levelData);

			GameEventManager.Instance.SendEvent(GameEventManager.LevelStartedEventId);

			ScreenManager.Instance.Show("game");

			NumLevelsTillAd--;

			// Check if it's time to show an interstitial ad
			if (NumLevelsTillAd <= 0 && Utilities.SystemTimeInMilliseconds - LastAdTimestamp >= minTimeBetweenAds * 1000)
			{
				#if BBG_MT_ADS
				if (MobileAdsManager.Instance.ShowInterstitialAd(null))
				{
					NumLevelsTillAd = numLevelsBetweenAds;
					LastAdTimestamp	= Utilities.SystemTimeInMilliseconds;
				}
				#endif
			}
		}

		/// <summary>
		/// Plays the next level based on the current active PackInfo and LevelData
		/// </summary>
		public void NextLevel()
		{
			int nextLevelIndex = ActiveLevelData.LevelIndex + 1;

			if (nextLevelIndex < ActivePackInfo.LevelDatas.Count)
			{
				StartLevel(ActivePackInfo, ActivePackInfo.LevelDatas[nextLevelIndex]);
			}
		}

		/// <summary>
		/// Sets the shape as placed on the grid in the save data, if gridCellPos is null then the shape was removed from the grid
		/// </summary>
		public void SetShapePlaced(LevelData.Shape shape, CellPos gridCellPos)
		{
			if (!IsLevelDataAvailable)
			{
				Debug.LogError("[GameManager] SetShapePlaced | Level data is null");
				return;
			}

			if (gridCellPos == null)
			{
				ActiveLevelSaveData.placedCellPositions[shape.index] = null;
			}
			else
			{
				ActiveLevelSaveData.placedCellPositions[shape.index] = gridCellPos.Copy();

				// Check if the level is now completed
				if (CheckActiveLevelCompleted())
				{
					ActiveLevelComplete();
				}
			}
		}

		/// <summary>
		/// Resets the active level so that no shapes are placed on the grid
		/// </summary>
		public void ResetActiveLevel()
		{
			if (!IsLevelDataAvailable)
			{
				Debug.LogError("[GameManager] ResetActiveLevel | Level data is null");
				return;
			}

			// Set all placed cell positions to null meaning they are no placed on the grid
			for (int i = 0; i < ActiveLevelSaveData.placedCellPositions.Count; i++)
			{
				ActiveLevelSaveData.placedCellPositions[i] = null;
			}
		}

		/// <summary>
		/// Attempts to spend the coins required to use a hint, if the player has enough coins they are deducted and a hint is set in the save data
		/// </summary>
		public bool TryUseHint(out int shapeIndex)
		{
			shapeIndex = -1;

			if (!IsLevelDataAvailable)
			{
				Debug.LogError("[GameManager] TryUseHint | Level data is null");
				return false;
			}

			List<LevelData.Shape> possibleShapes = new List<LevelData.Shape>();

			// Get all the shapes that are not placed in the proper spot
			for (int i = 0; i < ActiveLevelData.Shapes.Count; i++)
			{
				LevelData.Shape	shape			= ActiveLevelData.Shapes[i];
				CellPos			placedCellPos	= ActiveLevelSaveData.placedCellPositions[i];

				// Check if the shape is not placed or the placed position is not proper (Anchor) position and a hint for the shape has not already been used
				if ((placedCellPos == null || !shape.Anchor.Equals(placedCellPos)) && !ActiveLevelSaveData.hintsPlaced.Contains(i))
				{
					possibleShapes.Add(shape);
				}
			}

			// Check if there are no possible hints to show
			if (possibleShapes.Count == 0)
			{
				return false;
			}

			// Now that we know we can display a hint, try and spend the required coins
			if (freeHints || CurrencyManager.Instance.TrySpend("coins", hintCoinCost))
			{
				// Player had enough coins so pick a random shape to display and show it
				LevelData.Shape	shapeToDisplay = possibleShapes[Random.Range(0, possibleShapes.Count)];

				// Set the hint in the save data
				ActiveLevelSaveData.hintsPlaced.Add(shapeToDisplay.index);

				shapeIndex = shapeToDisplay.index;

				return true;
			}

			return false;
		}

		/// <summary>
		/// Returns true if the level has been completed atleast once
		/// </summary>
		public bool IsLevelCompleted(LevelData levelData)
		{
			if (!packLastCompletedLevel.ContainsKey(levelData.PackId))
			{
				return false;
			}

			return levelData.LevelIndex <= packLastCompletedLevel[levelData.PackId];
		}

		/// <summary>
		/// Returns true if the level is locked, false if it can be played
		/// </summary>
		public bool IsLevelLocked(LevelData levelData)
		{
			if (unlockAllLevels) return false;

			return levelData.LevelIndex > 0 && (!packLastCompletedLevel.ContainsKey(levelData.PackId) || levelData.LevelIndex > packLastCompletedLevel[levelData.PackId] + 1);
		}

		/// <summary>
		/// Returns true if the pack is locked
		/// </summary>
		public bool IsPackLocked(PackInfo packInfo)
		{
			if (unlockAllPacks) return false;

			switch (packInfo.unlockType)
			{
				case PackUnlockType.Coins:
					return !unlockedPacks.Contains(packInfo.packId);
				case PackUnlockType.IAP:
					#if BBG_MT_IAP
					return IAPManager.Exists() && !IAPManager.Instance.IsProductPurchased(packInfo.unlockIAPProductId);
					#else
					return true;
					#endif
			}

			return false;
		}

		/// <summary>
		/// Unlocks the given pack
		/// </summary>
		public bool TryUnlockPackWithCoins(PackInfo packInfo)
		{
			if (CurrencyManager.Instance.TrySpend("coins", packInfo.unlockCoinsAmount))
			{
				unlockedPacks.Add(packInfo.packId);

				return true;
			}

			return false;
		}

		/// <summary>
		/// Gets the pack progress percentage
		/// </summary>
		public int GetNumCompletedLevels(PackInfo packInfo)
		{
			if (!packLastCompletedLevel.ContainsKey(packInfo.packId))
			{
				return 0;
			}

			return packLastCompletedLevel[packInfo.packId] + 1;
		}

		/// <summary>
		/// Gets the pack progress percentage
		/// </summary>
		public float GetPackProgress(PackInfo packInfo)
		{
			return (float)(GetNumCompletedLevels(packInfo)) / (float)packInfo.levelFiles.Count;
		}

		#endregion

		#region Private Variables

		/// <summary>
		/// Gets the LevelSaveData reference to use for the given level
		/// </summary>
		private LevelSaveData GetLevelSaveData(LevelData levelData)
		{
			LevelSaveData levelSaveData = null;

			// Check if the level has not been started and if there is loaded save data for it
			if (!levelSaveDatas.ContainsKey(levelData.Id))
			{
				levelSaveData = CreateLevelSaveData(levelData);
			}
			else
			{
				levelSaveData = levelSaveDatas[levelData.Id];

				// Check if the timestamps no longer match, if they don't then the level file has been changed 
				if (levelSaveData.timestamp != levelData.Timestamp)
				{
					// Remove the old LevelSaveData
					levelSaveDatas.Remove(levelData.Id);

					// Create a new one
					levelSaveData = CreateLevelSaveData(levelData);
				}
			}

			return levelSaveData;
		}

		/// <summary>
		/// Creates a new LevelSaveData for the given level
		/// </summary>
		private LevelSaveData CreateLevelSaveData(LevelData levelData)
		{
			LevelSaveData levelSaveData = new LevelSaveData(levelData);

			levelSaveDatas.Add(levelData.Id, levelSaveData);

			return levelSaveData;
		}

		/// <summary>
		/// Checks if the active level is completed by checking that all shapes have been placed
		/// </summary>
		private bool CheckActiveLevelCompleted() // Level complete
		{
			for (int i = 0; i < ActiveLevelSaveData.placedCellPositions.Count; i++)
			{
				// If the shape has a null placedCellPositions then it is not on the game grid anywhere so it is not placed
				if (ActiveLevelSaveData.placedCellPositions[i] == null)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Invoked by GameGrid when the active level has all the lines placed on the grid
		/// </summary>
		private void ActiveLevelComplete()
		{
			// Get gift progress information
			int		lastLevelCompleted	= (packLastCompletedLevel.ContainsKey(ActiveLevelData.PackId) ? packLastCompletedLevel[ActiveLevelData.PackId] : -1);
			bool	giftProgressed 		= (ActiveLevelData.LevelIndex > lastLevelCompleted);
			int		fromGiftProgress	= (lastLevelCompleted + 1);
			int		toGiftProgress		= (ActiveLevelData.LevelIndex + 1);
			bool	giftAwarded			= (giftProgressed && toGiftProgress % numLevelsForGift == 0);

			// Give one hint if a gift should be awarded
			if (giftAwarded)
			{
				CurrencyManager.Instance.Give("coins", numCoinsPerGift);
			}

			// Set the active level as completed
			SetLevelComplete(ActiveLevelData);

			// Remove the save data since it's only for levels which have been started but not completed
			levelSaveDatas.Remove(ActiveLevelData.Id);

			bool isLastLevel = (ActiveLevelData.LevelIndex == ActivePackInfo.LevelDatas.Count - 1);

			// Create the data object array to pass to the level complete popup
			object[] popupData = 
			{
				isLastLevel,
				giftProgressed,
				giftAwarded,
				fromGiftProgress,
				toGiftProgress,
				numLevelsForGift,
				numCoinsPerGift
			};

			SoundManager.Instance.Play("level-completed");

			// Show the level completed popup
			PopupManager.Instance.Show("level_complete", popupData, OnLevelCompletePopupClosed);

			// Send an active level completed game event 
			GameEventManager.Instance.SendEvent(GameEventManager.ActiveLevelCompletedEventId);
		}

		private void OnLevelCompletePopupClosed(bool cancelled, object[] data)
		{
			string action = data[0] as string;

			switch (action)
			{
				case "next_level":
					NextLevel();
					break;
				case "back_to_level_list":
					ScreenManager.Instance.Back();
					break;
				case "back_to_bundle_list":
					ScreenManager.Instance.BackTo("bundles");
					break;
			}
		}

		/// <summary>
		/// Sets the level status
		/// </summary>
		private void SetLevelComplete(LevelData levelData)
		{
			// Set the last completed level in the pack
			int curLastCompletedLevel = packLastCompletedLevel.ContainsKey(levelData.PackId) ? packLastCompletedLevel[levelData.PackId] : -1;

			if (levelData.LevelIndex > curLastCompletedLevel)
			{
				packLastCompletedLevel[levelData.PackId] = levelData.LevelIndex;
			}
		}

		public override Dictionary<string, object> Save()
		{
			Dictionary<string, object> json = new Dictionary<string, object>();

			json["unlocked_packs"]		= new List<string>(unlockedPacks);
			json["last_completed"]		= SaveLastCompleteLevels();
			json["level_save_datas"]	= SaveLevelDatas();
			json["num_levels_till_ad"]	= NumLevelsTillAd;
			json["last_ad_timestamp"]	= LastAdTimestamp;

			return json;
		}

		private List<object> SaveLastCompleteLevels()
		{
			List<object> json = new List<object>();

			foreach (KeyValuePair<string, int> pair in packLastCompletedLevel)
			{
				Dictionary<string, object> packJson = new Dictionary<string, object>();

				packJson["pack_id"]					= pair.Key;
				packJson["last_completed_level"]	= pair.Value;

				json.Add(packJson);
			}

			return json;
		}

		private List<object> SaveLevelDatas()
		{
			List<object> savedLevelDatas = new List<object>();

			foreach (KeyValuePair<string, LevelSaveData> pair in levelSaveDatas)
			{
				Dictionary<string, object> levelSaveDataJson = new Dictionary<string, object>();

				levelSaveDataJson["id"]		= pair.Key;
				levelSaveDataJson["data"]	= pair.Value.Save();

				savedLevelDatas.Add(levelSaveDataJson);
			}

			return savedLevelDatas;
		}

		protected override void LoadSaveData(bool exists, JSONNode saveData)
		{
			if (!exists)
			{
				NumLevelsTillAd	= numLevelsBetweenAds;
				LastAdTimestamp	= Utilities.SystemTimeInMilliseconds;

				return;
			}

			LoadUnlockedPacks(saveData["unlocked_packs"].AsArray);
			LoadLastCompleteLevels(saveData["last_completed"].AsArray);
			LoadLevelSaveDatas(saveData["level_save_datas"].AsArray);

			NumLevelsTillAd	= saveData["num_levels_till_ad"].AsInt;
			LastAdTimestamp	= saveData["last_ad_timestamp"].AsInt;
		}

		private void LoadUnlockedPacks(JSONArray json)
		{
			for (int i = 0; i < json.Count; i++)
			{
				unlockedPacks.Add(json[i].Value);
			}
		}

		private void LoadLastCompleteLevels(JSONArray json)
		{
			for (int i = 0; i < json.Count; i++)
			{
				JSONNode childJson = json[i];

				string	packId				= childJson["pack_id"].Value;
				int		lastCompletedLevel	= childJson["last_completed_level"].AsInt;

				packLastCompletedLevel.Add(packId, lastCompletedLevel);
			}
		}

		/// <summary>
		/// Loads the game from the saved json file
		/// </summary>
		private void LoadLevelSaveDatas(JSONArray savedLevelDatasJson)
		{
			// Load all the placed line segments for levels that have progress
			for (int i = 0; i < savedLevelDatasJson.Count; i++)
			{
				JSONNode	savedLevelJson	= savedLevelDatasJson[i];
				string		levelId			= savedLevelJson["id"].Value;
				JSONNode	savedLevelData	= savedLevelJson["data"];

				LevelSaveData levelSaveData = new LevelSaveData(savedLevelData);

				levelSaveDatas.Add(levelId, levelSaveData);
			}
		}

		#endregion
	}
}
