using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBG.Blocks
{
	public class TopBar : MonoBehaviour
	{
		#region Inspector Variables

		[SerializeField] private CanvasGroup	backButton		= null;
		[SerializeField] private Text			headerText		= null;

		#endregion

		#region Member Variables

		private BundleInfo selectedBundleInfo;

		#endregion

		#region Unity Methods

		private void Start()
		{
			backButton.alpha = 0f;

			ScreenManager.Instance.OnSwitchingScreens += OnSwitchingScreens;

			GameEventManager.Instance.RegisterEventHandler(GameEventManager.BundleSelectedEventId, OnBundleSelected);
			GameEventManager.Instance.RegisterEventHandler(GameEventManager.LevelStartedEventId, OnLevelStarted);
		}

		#endregion

		#region Private Methods

		private void OnBundleSelected(string eventId, object[] data)
		{
			selectedBundleInfo = data[0] as BundleInfo;
		}

		private void OnSwitchingScreens(string fromScreenId, string toScreenId)
		{
			if (fromScreenId == ScreenManager.Instance.HomeScreenId)
			{
				backButton.interactable = true;

				UIAnimation anim = UIAnimation.Alpha(backButton, 1f, 0.35f);

				anim.style = UIAnimation.Style.EaseOut;

				anim.Play();
			}
			else if (toScreenId == ScreenManager.Instance.HomeScreenId)
			{
				backButton.interactable = false;

				UIAnimation anim = UIAnimation.Alpha(backButton, 0f, 0.35f);

				anim.style = UIAnimation.Style.EaseOut;

				anim.Play();
			}

			UpdateHeaderText(toScreenId);
		}

		private void OnLevelStarted(string eventId, object[] data)
		{
			string text = string.Format("LEVEL {0}", GameManager.Instance.ActiveLevelData.LevelIndex + 1);

			if (ScreenManager.Instance.CurrentScreenId != "game")
			{
				UIAnimation.SwapText(headerText, text, 0.5f);
			}
			else
			{
				headerText.text = text;
			}
		}

		private void UpdateHeaderText(string toScreenId)
		{
			switch (toScreenId)
			{
				case "main":
					UIAnimation.SwapText(headerText, "", 0.5f);
					break;
				case "bundles":
					UIAnimation.SwapText(headerText, "BUNDLES", 0.5f);
					break;
				case "pack_levels":
					UIAnimation.SwapText(headerText, selectedBundleInfo.bundleName, 0.5f);
					break;
			}
		}

		#endregion
	}
}
