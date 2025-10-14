using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBG.Blocks
{
	public class GiftBox : MonoBehaviour
	{
		#region Inspector Variables

		[SerializeField] private CanvasGroup	giftBase		= null;
		[SerializeField] private CanvasGroup	giftTop			= null;
		[SerializeField] private CanvasGroup	giftContainer	= null;
		[SerializeField] private AnimationCurve	animCurve	= null;

		#endregion

		#region Member Variables

		private Transform	originalParent;
		private Vector3		originalScale;
		private Vector3		originalPosition;


		#endregion

		#region Unity Methods

		private void Awake()
		{
			originalParent		= transform.parent;
			originalScale		= transform.localScale;
			originalPosition	= (transform as RectTransform).anchoredPosition;
		}

		#endregion

		#region Public Methods

		public IEnumerator PlayOpenAnimation()
		{
			giftContainer.gameObject.SetActive(false);

			UIAnimation anim;
			RectTransform rectT = transform as RectTransform;

			// Move the gift to the middle of the screen
			anim				= UIAnimation.PositionY(rectT, 0f, 1f);
			anim.style			= UIAnimation.Style.Custom;
			anim.animationCurve	= animCurve;
			anim.Play();

			anim				= UIAnimation.PositionX(rectT, 0f, 1f);
			anim.style			= UIAnimation.Style.Custom;
			anim.animationCurve	= animCurve;
			anim.Play();

			// Scale the gift so it's larger
			anim				= UIAnimation.ScaleX(rectT, 1f, 1f);
			anim.style			= UIAnimation.Style.Custom;
			anim.animationCurve	= animCurve;
			anim.Play();

			anim				= UIAnimation.ScaleY(rectT, 1f, 1f);
			anim.style			= UIAnimation.Style.Custom;
			anim.animationCurve	= animCurve;
			anim.Play();

			while (anim.IsPlaying)
			{
				yield return null;
			}

			yield return new WaitForSeconds(0.5f);

			// Move the top up
			anim		= UIAnimation.PositionY(giftTop.transform as RectTransform, 100f, 0.5f);
			anim.style	= UIAnimation.Style.EaseOut;
			anim.Play();

			// Fade out the top
			anim		= UIAnimation.Alpha(giftTop, 0f, 0.5f);
			anim.style	= UIAnimation.Style.EaseOut;
			anim.OnAnimationFinished += (GameObject obj) => { obj.SetActive(false); };
			anim.Play();

			while (anim.IsPlaying)
			{
				yield return null;
			}

			giftContainer.gameObject.SetActive(true);

			// Move the gift up
			anim		= UIAnimation.PositionY(giftContainer.transform as RectTransform, 300f, 0.5f);
			anim.style	= UIAnimation.Style.EaseOut;
			anim.Play();
			
			// Fade in the gift
			anim					= UIAnimation.Alpha(giftContainer, 0f, 1f, 0.5f);
			anim.style				= UIAnimation.Style.EaseOut;
			anim.startOnFirstFrame	= true;
			anim.Play();

			yield return new WaitForSeconds(0.15f);

			// Fade out the base
			anim		= UIAnimation.Alpha(giftBase, 0f, 0.5f);
			anim.style	= UIAnimation.Style.EaseOut;
			anim.OnAnimationFinished += (GameObject obj) => { obj.SetActive(false); };
			anim.Play();

			while (anim.IsPlaying)
			{
				yield return null;
			}
		}

		public void ResetUI()
		{
			UIAnimation.DestroyAllAnimations(giftBase.gameObject);
			UIAnimation.DestroyAllAnimations(giftContainer.gameObject);
			UIAnimation.DestroyAllAnimations(giftTop.gameObject);
			UIAnimation.DestroyAllAnimations(gameObject);

			transform.SetParent(originalParent);

			transform.localScale							= originalScale;
			(transform as RectTransform).anchoredPosition	= originalPosition;

			giftContainer.gameObject.SetActive(false);
			giftBase.gameObject.SetActive(true);
			giftTop.gameObject.SetActive(true);

			giftBase.alpha = 1f;
			giftTop.alpha = 1f;
			giftContainer.alpha = 1f;

			(giftContainer.transform as RectTransform).anchoredPosition	= Vector2.zero;
			(giftTop.transform as RectTransform).anchoredPosition		= Vector2.zero;
		}

		#endregion
	}
}
