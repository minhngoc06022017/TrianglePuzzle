using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BBG.Blocks
{
	public class LevelSaveData
	{
		#region Member Variables

		public string			timestamp;
		public List<CellPos>	placedCellPositions;
		public List<int>		hintsPlaced;

		#endregion

		#region Public Methods

		public LevelSaveData(LevelData levelData)
		{
			placedCellPositions = new List<CellPos>();
			hintsPlaced			= new List<int>();

			timestamp = levelData.Timestamp;

			for (int i = 0; i < levelData.Shapes.Count; i++)
			{
				placedCellPositions.Add(null);
			}
		}

		public LevelSaveData(JSONNode saveData)
		{
			placedCellPositions = new List<CellPos>();
			hintsPlaced			= new List<int>();

			LoadSave(saveData);
		}

		public Dictionary<string, object> Save()
		{
			string placedCellPositionStr = "";

			for (int i = 0; i < placedCellPositions.Count; i++)
			{
				if (i != 0)
				{
					placedCellPositionStr += ",";
				}

				CellPos cellPos = placedCellPositions[i];

				if (cellPos == null)
				{
					placedCellPositionStr += "-1,-1";
				}
				else
				{
					placedCellPositionStr += string.Format("{0},{1}", cellPos.x, cellPos.y);
				}
			}

			Dictionary<string, object> saveData = new Dictionary<string, object>();

			saveData["timestamp"]				= timestamp;
			saveData["placed_cell_positions"]	= placedCellPositionStr;
			saveData["hints_placed"]			= hintsPlaced;

			return saveData;
		}

		public void LoadSave(JSONNode saveData)
		{
			timestamp = saveData["timestamp"].Value;

			string[] placedCellPositionStr = saveData["placed_cell_positions"].Value.Split(',');

			for (int i = 0; i < placedCellPositionStr.Length; i += 2)
			{
				int x = int.Parse(placedCellPositionStr[i]);
				int y = int.Parse(placedCellPositionStr[i+1]);

				if (x == -1 || y == -1)
				{
					placedCellPositions.Add(null);
				}
				else
				{
					placedCellPositions.Add(new CellPos(x, y));
				}
			}

			foreach (JSONNode node in saveData["hints_placed"].AsArray)
			{
				hintsPlaced.Add(node.AsInt);
			}
		}

		#endregion
	}
}
