using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBG.Blocks
{
	public class LevelListScreen : Screen
	{
		#region Inspector Variables

		[Space]

		[SerializeField] private LevelListItem	levelListItemPrefab	= null;
		[SerializeField] private RectTransform	levelListContainer	= null;
		[SerializeField] private ScrollRect		levelListScrollRect	= null;
		[SerializeField] private Text			packNameText		= null;

		#endregion

		#region Member Variables

		private PackInfo							currentPackInfo;
		private RecyclableListHandler<LevelData>	levelListHandler;

		#endregion

		#region Public Methods

		public override void Initialize()
		{
			base.Initialize();

			GameEventManager.Instance.RegisterEventHandler(GameEventManager.PackSelectedEventId, OnPackSelected);
		}

		public override void Show(bool back, bool immediate)
		{
			base.Show(back, immediate);

			levelListHandler.Refresh();
		}

		#endregion

		#region Private Methods

		private void OnPackSelected(string eventId, object[] data)
		{
			PackInfo selectedPackInfo = data[0] as PackInfo;

			if (currentPackInfo != selectedPackInfo)
			{
				UpdateList(selectedPackInfo);
			}
		}

		private void UpdateList(PackInfo packInfo)
		{
			currentPackInfo = packInfo;

			packNameText.text = packInfo.packName;

			if (levelListHandler == null)
			{
				levelListHandler					= new RecyclableListHandler<LevelData>(packInfo.LevelDatas, levelListItemPrefab, levelListContainer, levelListScrollRect);
				levelListHandler.OnListItemClicked	= OnLevelListItemClicked;
				levelListHandler.Setup();
			}
			else
			{
				levelListHandler.UpdateDataObjects(packInfo.LevelDatas);
			}
		}

		private void OnLevelListItemClicked(LevelData levelData)
		{
			if (!GameManager.Instance.IsLevelLocked(levelData))
			{
				GameManager.Instance.StartLevel(currentPackInfo, levelData);
			}
		}

		#endregion
	}
}
