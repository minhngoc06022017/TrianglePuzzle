using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBG.Blocks
{
	public class PurchasePackPopup : Popup
	{
		#region Inspector Variables
		
		[Space]

		[SerializeField] private Text messageText	= null;
		[SerializeField] private Text amountText	= null;
		
		#endregion // Inspector Variables

		#region Member Variables
		
		private PackInfo packInfo;
		
		#endregion // Member Variables

		#region Public Methods
		
		public override void OnShowing(object[] inData)
		{
			base.OnShowing(inData);


			BundleInfo bundleInfo = inData[0] as BundleInfo;

			packInfo = inData[1] as PackInfo;

			messageText.text	= string.Format("Unlock pack {0} - {1} for:", bundleInfo.bundleName, packInfo.packName);
			amountText.text		= "x " + packInfo.unlockCoinsAmount;
		}

		public void Unlock()
		{
			Hide(false, new object[] { packInfo });
		}
		
		#endregion // Public Methods
	}
}
