using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBG.Blocks
{
	public class Tile : MonoBehaviour
	{
		#region Inspector Variables
		
		public Image tileBkg;
		
		#endregion // Inspector Variables

		#region Properties
		
		public RectTransform RectT { get { return transform as RectTransform; } }
		
		#endregion // Properties
	}
}
