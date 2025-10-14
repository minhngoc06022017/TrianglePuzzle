using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if BBG_MT_ADS
using BBG.MobileTools;
#endif

namespace BBG
{
	public class NotEnoughCurrencyPopup : Popup
	{
		#region Inspector Variables

		[Space]

		[SerializeField] private Text		titleText				= null;
		[SerializeField] private Text		messageText				= null;
		[SerializeField] private Text		rewardAdButtonText		= null;
		[SerializeField] private GameObject rewardAdButton			= null;
		[SerializeField] private GameObject storeButton				= null;
		[SerializeField] private GameObject buttonsContainer		= null;
		[SerializeField] private bool		testRewardAdsInEditor	= false;

		#endregion

		#region Member Variables

		private CurrencyManager.Settings currencySettings;

		#endregion

		#region Public Methods

		public override void OnShowing(object[] inData)
		{
			base.OnShowing(inData);

			this.currencySettings = inData[0] as CurrencyManager.Settings;

			titleText.text			= currencySettings.popupTitleText;
			messageText.text		= currencySettings.popupMessageText;
			rewardAdButtonText.text = currencySettings.rewardButtonText;

			bool showStoreButton = currencySettings.popupHasStoreButton;

			#if BBG_MT_ADS
			bool showRewardAdButton	= currencySettings.popupHasRewardAdButton && MobileAdsManager.Instance.AreRewardAdsEnabled;
			#else
			bool showRewardAdButton	= false;
			#endif

			storeButton.SetActive(showStoreButton);

			if (buttonsContainer != null)
			{
				buttonsContainer.SetActive(showRewardAdButton || showStoreButton);
			}

			#if BBG_MT_ADS
			if (showRewardAdButton)
			{
				rewardAdButton.SetActive(MobileAdsManager.Instance.RewardAdState == AdNetworkHandler.AdState.Loaded);

				MobileAdsManager.Instance.OnRewardAdLoaded	+= OnRewardAdLoaded;
				MobileAdsManager.Instance.OnAdsRemoved		+= OnAdsRemoved;
			}
			else
			#endif
			{
				rewardAdButton.SetActive(false);
			}

			#if UNITY_EDITOR
			if (testRewardAdsInEditor && currencySettings.popupHasRewardAdButton)
			{
				rewardAdButton.SetActive(true);
			}
			#endif
		}

		public override void OnHiding(bool cancelled)
		{
			base.OnHiding(cancelled);

			#if BBG_MT_ADS
			MobileAdsManager.Instance.OnRewardAdLoaded	-= OnRewardAdLoaded;
			MobileAdsManager.Instance.OnAdsRemoved		-= OnAdsRemoved;
			#endif
		}

		public void OnRewardAdButtonClick()
		{
			#if UNITY_EDITOR
			if (testRewardAdsInEditor)
			{
				OnRewardAdGranted();
				Hide(false);
				return;
			}
			#endif

			#if BBG_MT_ADS
			if (MobileAdsManager.Instance.RewardAdState != AdNetworkHandler.AdState.Loaded)
			{
				rewardAdButton.SetActive(false);

				Debug.LogError("[NotEnoughCurrencyPopup] The reward button was clicked but there is no ad loaded to show.");

				return;
			}

			MobileAdsManager.Instance.ShowRewardAd(OnRewardAdClosed, OnRewardAdGranted);
			#endif

			Hide(false);
		}

		#endregion

		#region Private Methods

		private void OnRewardAdLoaded()
		{
			rewardAdButton.SetActive(true);
		}

		private void OnRewardAdClosed()
		{
			rewardAdButton.SetActive(false);
		}

		private void OnRewardAdGranted()
		{
			CurrencyManager.Instance.Give(currencySettings.rewardCurrencyId, currencySettings.rewardAmount);

			object[] popupData =
			{
				currencySettings.rewardAdGrantedPopupTitle,
				currencySettings.rewardAdGrantedPopupMessage
			};

			PopupManager.Instance.Show(currencySettings.rewardAdGrantedPopupId, popupData);
		}

		private void OnAdsRemoved()
		{
			#if BBG_MT_ADS
			MobileAdsManager.Instance.OnRewardAdLoaded -= OnRewardAdLoaded;
			#endif

			rewardAdButton.SetActive(false);
		}

		#endregion
	}
}
