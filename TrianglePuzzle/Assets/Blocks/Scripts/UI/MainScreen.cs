using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if BBG_MT_ADS
using BBG.MobileTools;
#endif

namespace BBG.Blocks
{
	public class MainScreen : Screen
	{
		#region Inspector Variables

		[SerializeField] private GameObject	removeAdsButton = null;

		#endregion

		#region Unity Methods

		protected override void Start()
		{
			base.Start();

			#if BBG_MT_ADS
			bool adsRemoved = MobileAdsManager.Instance.AdsRemoved;

			removeAdsButton.SetActive(!adsRemoved);

			if (!adsRemoved)
			{
				MobileAdsManager.Instance.OnAdsRemoved += () => { removeAdsButton.SetActive(false); };
			}
			#else
			removeAdsButton.SetActive(false);
			#endif
		}

		#endregion
	}
}
