using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BBG.Blocks
{
	public class LevelCreatorData : ScriptableObject
	{
		#region Static Instance
		
		private static LevelCreatorData instance;
		
		public static LevelCreatorData Instance
		{
			get
			{
				if (instance == null)
				{
					instance = ScriptableObjectUtilities.CreateFromAssetPath<LevelCreatorData>(LevelCreatorPaths.DataAssetPath);
				}

				return instance;
			}
		}
		
		#endregion // Static Instance

		#region Data

		// Settings
		public LevelData.LevelType	levelType;
		public bool					rotateHexagon;
		public int					xCells;
		public int					yCells;
		public int					numShapes;

		// Export
		public string filename;
		public string outputFolderAssetPath;

		// Auto Generation
		public int		minShapeSize;
		public int		maxShapeSize;
		public int		numLevels;

		#endregion // Data
	}
}
