using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBG.Blocks
{
	public class LevelCompletePopup : Popup
	{
		#region Inspector Variables

		[Space]
		[SerializeField] private GameObject		nextLevelButton		= null;
		[SerializeField] private GameObject		backToMenuButton	= null;
		[Space]
		[SerializeField] private ProgressBar	giftProgressBar		= null;
		[SerializeField] private Text			giftProgressText	= null;
		[SerializeField] private Text			giftAmountText		= null;
		[SerializeField] private GiftBox		giftBox				= null;
		[SerializeField] private CanvasGroup	giftAnimContainer	= null;
		[SerializeField] private CanvasGroup	giftAnimBkgFade		= null;

		#endregion

		#region Member Variables

		private const float GiftProgressAnimDuration = 0.5f;

		private IEnumerator giveGiftAnimRoutine;
		private IEnumerator giftBoxAnimRoutine;

		#endregion

		#region Public Methods

		public override void OnShowing(object[] inData)
		{
			base.OnShowing(inData);

			bool	isLastLevel			= (bool)inData[0];
			bool	giftProgressed		= (bool)inData[1];
			bool	giftAwarded			= (bool)inData[2];
			int		fromGiftProgress	= (int)inData[3];
			int		toGiftProgress		= (int)inData[4];
			int		numLevelsForGift	= (int)inData[5];
			int		giftAmount			= (int)inData[6];

			ResetUI();

			nextLevelButton.SetActive(!isLastLevel);
			backToMenuButton.SetActive(isLastLevel);

			int giftFromAmt	= (fromGiftProgress % numLevelsForGift);
			int giftToAmt	= (giftAwarded ? numLevelsForGift : toGiftProgress % numLevelsForGift);

			if (giftProgressed)
			{
				giftProgressText.text = string.Format("{0} / {1}", giftToAmt, numLevelsForGift);

				float fromProgress	= (float)giftFromAmt / (float)numLevelsForGift;
				float toProgress	= (float)giftToAmt / (float)numLevelsForGift;

				float giftProgressStartDelay = animDuration + 0.25f;

				giftProgressBar.SetProgressAnimated(fromProgress, toProgress, GiftProgressAnimDuration, giftProgressStartDelay);

				if (giftAwarded)
				{
					giftAmountText.text = "+" + giftAmount;
					StartCoroutine(giveGiftAnimRoutine = PlayGiftAwardedAnimation(giftProgressStartDelay + GiftProgressAnimDuration + 0.25f));
				}
			}
			else
			{
				giftProgressText.text = string.Format("{0} / {1}", giftFromAmt, numLevelsForGift);

				giftProgressBar.SetProgress((float)giftFromAmt / (float)numLevelsForGift);
			}
		}

		#endregion

		#region Private Methods

		private IEnumerator PlayGiftAwardedAnimation(float startDelay)
		{
			// Wait before starting the gift animations
			yield return new WaitForSeconds(startDelay);

			SoundManager.Instance.Play("gift-awarded");

			// Set the gift to the gift animation container so it will appear ontop of everything
			giftBox.transform.SetParent(giftAnimContainer.transform);

			// Set the gift animation container as active
			giftAnimContainer.gameObject.SetActive(true);

			UIAnimation anim;

			// Fade in the gift container background
			anim = UIAnimation.Alpha(giftAnimBkgFade, 0f, 1f, 0.5f);
			anim.startOnFirstFrame = true;
			anim.Play();

			giftBoxAnimRoutine = giftBox.PlayOpenAnimation();

			// Play the gift open animations and wait for them to finish
			yield return giftBoxAnimRoutine;

			giftBoxAnimRoutine = null;

			// Wait a bit so the player can see what they got
			yield return new WaitForSeconds(1f);

			// Fade out the whole gift container so the popup is now visible again
			anim = UIAnimation.Alpha(giftAnimContainer, 1f, 0f, 0.5f);
			anim.startOnFirstFrame = true;
			anim.Play();

			while (anim.IsPlaying)
			{
				yield return null;
			}

			// Set the gift container to de-active so the player can click buttons on the popup again
			giftAnimContainer.gameObject.SetActive(false);
			giftAnimContainer.alpha = 1f;

			giveGiftAnimRoutine = null;
		}

		private void ResetUI()
		{
			if (giveGiftAnimRoutine != null)
			{
				StopCoroutine(giveGiftAnimRoutine);
				giveGiftAnimRoutine = null;
			}

			if (giftBoxAnimRoutine != null)
			{
				StopCoroutine(giftBoxAnimRoutine);
				giftBoxAnimRoutine = null;
			}

			giftBox.ResetUI();

			UIAnimation.DestroyAllAnimations(giftAnimBkgFade.gameObject);
			UIAnimation.DestroyAllAnimations(giftAnimContainer.gameObject);
		}

		#endregion
	}
}
