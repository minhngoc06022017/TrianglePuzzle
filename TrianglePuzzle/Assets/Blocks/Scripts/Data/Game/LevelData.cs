using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BBG.Blocks
{
	public class LevelData
	{
		#region Classes

		public class Shape
		{
			public int				index;
			public RectInt			bounds;
			public List<CellPos>	cellPositions;

			/// <summary>
			/// Gets the CellPosition to use when positioning the shape on the grid
			/// </summary>
			public CellPos	Anchor			{ get { return cellPositions[0]; } }

			/// <summary>
			/// For triangle levels, returns true if the tile at the Anchor cell position is upside down
			/// </summary>
			public bool		IsAnchorFlipped	{ get { return (Anchor.x + Anchor.y) % 2 == 1; } }
		}

		#endregion // Classes

		#region Enums

        public enum LevelType
        {
            Square,
            Triangle,
            Hexagon
        }

		public enum CellType
		{
			Blank,
			Block,
			Normal
		}

		public enum HexagonOrientation
		{
			Vertical,
			Horizontal
		}

        #endregion

		#region Member Variables

		private TextAsset				levelFile;
		private string					levelFileText;
		private bool					isLevelFileParsed;

		// Values parsed from level file
		private string					timestamp;
		private LevelType				levelType;
		private HexagonOrientation		hexagonOrientation;
		private int						yCells;
		private int						xCells;
		private List<List<CellType>>	gridCellTypes;
		private List<Shape>				shapes;

		#endregion

		#region Properties

		public string				Id				{ get; private set; }
		public string				PackId			{ get; private set; }
		public int					LevelIndex		{ get; private set; }

		public string				Timestamp		{ get { if (!isLevelFileParsed) ParseLevelFile(); return timestamp; } }
		public LevelType			Type			{ get { if (!isLevelFileParsed) ParseLevelFile(); return levelType; } }
		public bool					IsVertHexagons	{ get { if (!isLevelFileParsed) ParseLevelFile(); return hexagonOrientation == HexagonOrientation.Vertical; } }
		public int					YCells			{ get { if (!isLevelFileParsed) ParseLevelFile(); return yCells; } }
		public int					XCells			{ get { if (!isLevelFileParsed) ParseLevelFile(); return xCells; } }
		public List<List<CellType>>	GridCellTypes	{ get { if (!isLevelFileParsed) ParseLevelFile(); return gridCellTypes; } }
		public List<Shape>			Shapes			{ get { if (!isLevelFileParsed) ParseLevelFile(); return shapes; } }

		private string LevelFileText
		{
			get
			{
				if (string.IsNullOrEmpty(levelFileText) && levelFile != null)
				{
					levelFileText	= levelFile.text;
					levelFile		= null;
				}

				return levelFileText;
			}
		}

		#endregion

		#region Constructor

		public LevelData(TextAsset levelFile, string packId, int levelIndex)
		{
			this.levelFile	= levelFile;
			PackId			= packId;
			LevelIndex		= levelIndex;
			Id				= string.Format("{0}_{1}", packId, levelIndex);
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Parse the json in the level file
		/// </summary>
		private void ParseLevelFile()
		{
			if (isLevelFileParsed) return;

			string		levelFileContents	= LevelFileText;
			string[]	items				= levelFileContents.Split(',');

			int itemIndex = 0;

			// First item is the timestamp for when the level file was generated
			timestamp = items[itemIndex++];

			// Next item is the level type
			levelType = (LevelType)int.Parse(items[itemIndex++]);

			// If the level type is Hexagon the the next value will determine the orentation of the hexagons 
			hexagonOrientation = (bool)bool.Parse(items[itemIndex++]) ? HexagonOrientation.Horizontal : HexagonOrientation.Vertical;

			// Next two items are the yCells, and xCells
			yCells = int.Parse(items[itemIndex++]);
			xCells = int.Parse(items[itemIndex++]);

			gridCellTypes = new List<List<CellType>>();

			Dictionary<int, List<int>> shapeDatas = new Dictionary<int, List<int>>(); 

			// Rest of the items are the grid cell types and where the shapes are placed on the grid
			// Value of 0 means its a blank cell, 1 means its a block, > 1 are the shapes
			for (int y = 0; y < yCells; y++)
			{
				gridCellTypes.Add(new List<CellType>());

				for (int x = 0; x < xCells; x++)
				{
					int value = int.Parse(items[itemIndex++]);

					if (value == 0)
					{
						gridCellTypes[y].Add(CellType.Blank);
					}
					else if (value == 1)
					{
						gridCellTypes[y].Add(CellType.Block);
					}
					else
					{
						gridCellTypes[y].Add(CellType.Normal);

						List<int> shapeData = null;

						if (!shapeDatas.ContainsKey(value))
						{
							shapeData = new List<int>();
							shapeData.Add(int.MaxValue);
							shapeData.Add(int.MinValue);
							shapeData.Add(int.MaxValue);
							shapeData.Add(int.MinValue);

							shapeDatas[value] = shapeData;
						}
						else
						{
							shapeData = shapeDatas[value];
						}

						// Update the bounds of the shape
						shapeData[0] = System.Math.Min(shapeData[0], x);	// Set left
						shapeData[1] = System.Math.Max(shapeData[1], x);	// Set right
						shapeData[2] = System.Math.Min(shapeData[2], y);	// Set top
						shapeData[3] = System.Math.Max(shapeData[3], y);	// Set bottom

						// Add the cell x/y
						shapeData.Add(x);
						shapeData.Add(y);
					}
				}
			}

			// Create the shapes list
			shapes = new List<Shape>();
			int index = 0;

			foreach (KeyValuePair<int, List<int>> pair in shapeDatas)
			{
				List<int> shapeData = pair.Value;

				int left	= shapeData[0];
				int right	= shapeData[1];
				int top		= shapeData[2];
				int bottom	= shapeData[3];

				Shape shape = new Shape();

				shape.index			= index++;
				shape.bounds		= new RectInt(left, top, right - left + 1, bottom - top + 1);
				shape.cellPositions	= new List<CellPos>();

				for (int i = 4; i < shapeData.Count; i += 2)
				{
					shape.cellPositions.Add(new CellPos(shapeData[i], shapeData[i + 1]));
				}

				shapes.Add(shape);
			}

			isLevelFileParsed = true;
		}

		#endregion
	}
}
