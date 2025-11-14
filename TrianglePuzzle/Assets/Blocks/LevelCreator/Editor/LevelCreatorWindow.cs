using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace BBG.Blocks
{
	public class LevelCreatorWindow : EditorWindow
	{
		#region Classes

		private class GridCell
		{
			public int		row;
			public int		col;
			public Image	imageField;
			public int		shapeIndex;
		}

		#endregion // Classes

		#region Enums

		#endregion // Enums

		#region Member Variables

		private const int	MaxCells			= 15;	// Maximum number of cells that can be in a row/column
		private const float MaxCellSize 		= 50;

		private const float ShapeBlockSize		= 25;
		private const float ShapeBlockSpacing	= 3;

		private const int	BlankCellShapeIndex = 0;
		private const int	BlockCellShapeIndex = 1;
		private const int	EmptyCellShapeIndex = 2;

		private Texture squareTexture;
		private Texture triangleTexture;
		private Texture hexagonTexture;

		private List<string> Levels;
        private List<List<GridCell>>	grid;
		private GridCell				hoveredGridCell;
		private bool					isDragging;
		private List<VisualElement>		shapeBlocks;
		private List<Color>				shapeColors;
		private int						selectedShapeIndex;
		private AutoGenerationWorker	autoGenerationWorker;

		private bool	isGeneratingGrid;
		private bool	isBatchGenerating;
		private int		numLevelsLeftToGenerate;

		#endregion // Member Variables

		#region Properties

		// Containers
		private VisualElement	GridContainerElement	{ get { return rootVisualElement.Q("gridContainer") as VisualElement; } }
		private VisualElement	ShapesContainerElement	{ get { return rootVisualElement.Q("shapesContainer") as VisualElement; } }

		// Settings fields
		private EnumField		LevelTypeField			{ get { return rootVisualElement.Q("levelType") as EnumField; } }
		private Toggle			RotateHexagonField		{ get { return rootVisualElement.Q("rotateHexagon") as Toggle; } }
		private IntegerField	XCellsField				{ get { return rootVisualElement.Q("xCells") as IntegerField; } }
		private IntegerField	YCellsField				{ get { return rootVisualElement.Q("yCells") as IntegerField; } }
		private IntegerField	NumShapesField			{ get { return rootVisualElement.Q("numShapes") as IntegerField; } }

		private LevelData.LevelType LevelType
		{
			get { return LevelCreatorData.Instance.levelType; }
			set { LevelCreatorData.Instance.levelType = value; }
		}

		private bool RotateHexagon
		{
			get { return LevelCreatorData.Instance.rotateHexagon; }
			set { LevelCreatorData.Instance.rotateHexagon = value; }
		}

		private int XCells
		{
			get { return LevelCreatorData.Instance.xCells; }
			set { LevelCreatorData.Instance.xCells = value; }
		}

		private int YCells
		{
			get { return LevelCreatorData.Instance.yCells; }
			set { LevelCreatorData.Instance.yCells = value; }
		}

		private int NumShapes
		{
			get { return LevelCreatorData.Instance.numShapes; }
			set { LevelCreatorData.Instance.numShapes = value; }
		}

        // Export fields
        private TextField		FilenameField			{ get { return rootVisualElement.Q("filename") as TextField; } }
		private ObjectField		OutputFolderField		{ get { return rootVisualElement.Q("outputFolder") as ObjectField; } }

		private string Filename
		{
			get { return FilenameField.value; }
			set { FilenameField.value = value; }
		}

		private string OutputFolderAssetPath
		{
			get { return LevelCreatorData.Instance.outputFolderAssetPath; }
			set { LevelCreatorData.Instance.outputFolderAssetPath = value; }
		}

		// Auto Generation fields
		private IntegerField	MinShapeSizeField	{ get { return rootVisualElement.Q("minShapeSize") as IntegerField; } }
		private IntegerField	MaxShapeSizeField	{ get { return rootVisualElement.Q("maxShapeSize") as IntegerField; } }
		private IntegerField	NumberOfLevelsField	{ get { return rootVisualElement.Q("numLevels") as IntegerField; } }

		private int MinShapeSize
		{
			get { return LevelCreatorData.Instance.minShapeSize; }
			set { LevelCreatorData.Instance.minShapeSize = value; }
		}

		private int MaxShapeSize
		{
			get { return LevelCreatorData.Instance.maxShapeSize; }
			set { LevelCreatorData.Instance.maxShapeSize = value; }
		}

		private int NumberOfLevels
		{
			get { return LevelCreatorData.Instance.numLevels; }
			set { LevelCreatorData.Instance.numLevels = value; }
		}

		// Textures
		private Texture SquareTexture	{ get { return (squareTexture == null ? squareTexture = AssetDatabase.LoadAssetAtPath<Texture>(LevelCreatorPaths.SquareTexturePath) : squareTexture); } }
		private Texture TriangleTexture	{ get { return (triangleTexture == null ? triangleTexture = AssetDatabase.LoadAssetAtPath<Texture>(LevelCreatorPaths.TriangleTexturePath) : triangleTexture); } }
		private Texture HexagonTexture	{ get { return (hexagonTexture == null ? hexagonTexture = AssetDatabase.LoadAssetAtPath<Texture>(LevelCreatorPaths.HexagonTexturePath) : hexagonTexture); } }

		#endregion // Properties

		#region Unity Methods

		[MenuItem("Tools/Bizzy Bee Games/Level Creator Window")]
		public static void ShowWindow()
		{
			LevelCreatorWindow wnd = GetWindow<LevelCreatorWindow>();
			wnd.titleContent = new GUIContent("Level Creator");
		}

		private void OnEnable()
		{
			grid				= new List<List<GridCell>>();
			shapeColors			= new List<Color>();
			shapeBlocks			= new List<VisualElement>();

			SetupWindowUI();
		}

		private void Update()
		{
			if (autoGenerationWorker != null)
			{
				if (autoGenerationWorker.Stopped)
				{
					if (!string.IsNullOrEmpty(autoGenerationWorker.error))
					{
						Debug.LogError(autoGenerationWorker.error);

						StopAutoGeneration();

						return;
					}

					AutoGenerationWorkerFinished();

					return;
				}

				string message = "";
				float progress = 1;

				if (isBatchGenerating)
				{
					int levelNumber = (NumberOfLevels - numLevelsLeftToGenerate) + 1;

					progress	= (float)levelNumber / (float)NumberOfLevels;
					message		= string.Format("Generating level {0} of {1}... Click the 'X' to cancel.", levelNumber, NumberOfLevels);
				}
				else if (isGeneratingGrid)
				{
					message = "Generating grid... Click the 'X' to cancel.";
				}
				else
				{
					message = "Generating shapes... Click the 'X' to cancel.";
				}

				if (EditorUtility.DisplayCancelableProgressBar("Auto Generation", message, progress))
				{
					autoGenerationWorker.Stop();

					StopAutoGeneration();
				}
			}
		}

		#endregion // Unity Methods

		#region Private Methods
		
		private void SetupWindowUI()
		{
			rootVisualElement.Clear();

			selectedShapeIndex = 2;

			// Get a reference to the UXML and USS files
			VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LevelCreatorPaths.UXMLFilePath);
			StyleSheet      styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(LevelCreatorPaths.USSFilePath);

			if (visualTree == null || styleSheet == null)
			{
				Debug.LogError("The .uxml and/or .uss file could not be found for the Level Creator Window. If you have changed the location of these files please update the UXMLFilePath and USSFilePath inside LevelCreatorPaths.cs");
				
				return;
			}

			// Setup the base UI of the window
			visualTree.CloneTree(rootVisualElement);
			rootVisualElement.styleSheets.Add(styleSheet);

			// Setup the enums
			LevelTypeField.Init(LevelCreatorData.Instance.levelType);

			// Set the accepted type of the output folder object field to Object, we will check if it is actually a folder when the user drags something into the field
			OutputFolderField.objectType = typeof(Object);

			// Register a GeometryChangedEvent on the grid-container VisualElement so when it re-sizes we can update the size of the cells
			// This will be called once right away when the window first opens and sizes itself
			rootVisualElement.RegisterCallback<GeometryChangedEvent>((evt) => { ResizeGrid(); });
			rootVisualElement.RegisterCallback<GeometryChangedEvent>((evt) => { ResizeShapeBlocks(); });

			// Register value changed events
			LevelTypeField.RegisterCallback<ChangeEvent<System.Enum>>(LevelTypeChanged);
			RotateHexagonField.RegisterCallback<ChangeEvent<bool>>(RotateHexagonChanged);
			XCellsField.RegisterCallback<ChangeEvent<int>>(XYCellsChanged);
			YCellsField.RegisterCallback<ChangeEvent<int>>(XYCellsChanged);
			NumShapesField.RegisterCallback<ChangeEvent<int>>(NumShapesChanged);
			OutputFolderField.RegisterCallback<ChangeEvent<Object>>((evt) => { 
				UpdateOutputPaths();
                LoadLevelsFromFolder();
                RefreshLevelListUI();
            });

			CreateLevelListField();

            FilenameField.RegisterCallback<ChangeEvent<string>>((evt) => { UpdateOutputPaths(); });
			MinShapeSizeField.RegisterCallback<ChangeEvent<int>>(MinMaxCellsChanged);
			MaxShapeSizeField.RegisterCallback<ChangeEvent<int>>(MinMaxCellsChanged);

			// Setup button click listeners
			(rootVisualElement.Q("clearShapesButton") as Button).clickable.clicked	+= ClearShapes;
			(rootVisualElement.Q("resetGridButton") as Button).clickable.clicked	+= OnResetGridBtn;
			(rootVisualElement.Q("autoFillShapes") as Button).clickable.clicked		+= AutoFill;
			(rootVisualElement.Q("export") as Button).clickable.clicked				+= ExportClicked;
            (rootVisualElement.Q("loadMapButton") as Button).clickable.clicked += LoadMapClicked;
            (rootVisualElement.Q("generateBatch") as Button).clickable.clicked		+= GenerateBatch;

			// Bind the UI elements to the LevelCreatorData ScriptableObject fields
			XCellsField.bindingPath			= "xCells";
			YCellsField.bindingPath			= "yCells";
			NumShapesField.bindingPath		= "numShapes";
			FilenameField.bindingPath		= "filename";
			MinShapeSizeField.bindingPath	= "minShapeSize";
			MaxShapeSizeField.bindingPath	= "maxShapeSize";
			NumberOfLevelsField.bindingPath	= "numLevels";
			//LevelListField.bindingPath = "mapLevels";

            MinShapeSizeField.isDelayed = true;
			MaxShapeSizeField.isDelayed = true;

			// Bind the root element to the instance of LevelCreatorData
			rootVisualElement.Bind(new SerializedObject(LevelCreatorData.Instance));

			// Set the reference to the output folder
			if (!string.IsNullOrEmpty(OutputFolderAssetPath))
			{
				Object outputFolder = AssetDatabase.LoadAssetAtPath<Object>(OutputFolderAssetPath);

				// Check if the folder still exists
				if (outputFolder == null)
				{
					OutputFolderAssetPath = null;
				}

				OutputFolderField.value = outputFolder;
			}

			// Generate the inital list of shape colors
			GenerateShapeColors();

			// Build the initial grid cells matrix
			RebuildGridCells();

			UpdateOutputPaths();

			RotateHexagonField.value = RotateHexagon;
			RotateHexagonField.style.display = (LevelType == LevelData.LevelType.Hexagon) ? DisplayStyle.Flex : DisplayStyle.None;
		}

        #region Extend

        private DropdownField LevelListField;

        private void SetupLevelListField()
        {
            // Lấy phần tử đầu tiên làm mặc định
            string defaultValue = Levels[0];

            // Khởi tạo DropdownField
            LevelListField = new DropdownField("Select Level", Levels, defaultValue)
            {
                name = "levelList"  // <--- quan trọng phải set name
            };

            // Thêm style nếu muốn
            LevelListField.style.marginTop = 5;
            LevelListField.style.marginBottom = 5;
            // Thêm vào UI dưới export box
            var exportBox = rootVisualElement.Q<Box>("exportBox"); // đổi tên box export nếu cần
            exportBox.Add(LevelListField);

            if (LevelListField != null)
            {
                LevelListField.RegisterCallback<ChangeEvent<string>>((evt) =>
                {
                    Debug.Log("Level selected: " + evt.newValue);
                    // Bạn có thể gọi LoadLevelMap(evt.newValue) nếu muốn load ngay
                });
            }
            else
            {
                Debug.LogWarning("LevelListField is null, cannot register callback.");
            }
        }

        private void LoadLevelMap(string filename)
        {
            string folder = GetOutputFolderAssetPath();
            string fullPath = Application.dataPath + folder.Substring("Assets".Length) + "/" + filename;

            if (!System.IO.File.Exists(fullPath))
            {
                Debug.LogError("File not found: " + fullPath);
                return;
            }

            string contents = System.IO.File.ReadAllText(fullPath);
            string[] parts = contents.Split(',');

            if (parts.Length < 5)
            {
                Debug.LogError("Invalid level file");
                return;
            }

            int index = 0;

            // Bỏ timestamp
            index++;

            LevelType = (LevelData.LevelType)int.Parse(parts[index++]);
            RotateHexagon = bool.Parse(parts[index++]);
            int yCells = int.Parse(parts[index++]);
            int xCells = int.Parse(parts[index++]);

            // Parse grid
            List<List<int>> grid = new List<List<int>>();

            for (int y = 0; y < yCells; y++)
            {
                List<int> row = new List<int>();
                for (int x = 0; x < xCells; x++)
                {
                    row.Add(int.Parse(parts[index++]));
                }
                grid.Add(row);
            }

            // Cập nhật lại UI & Data
            UpdateDataMap(LevelType, RotateHexagon, xCells, yCells, grid);

            // Update UI fields
            XCellsField.value = xCells;
            YCellsField.value = yCells;
            RotateHexagonField.value = RotateHexagon;
        }

        private void UpdateDataMap(LevelData.LevelType levelType, bool rotateHex, int xCells, int yCells, List<List<int>> gridValues)
        {
            LevelCreatorData.Instance.levelType = levelType;
            LevelCreatorData.Instance.rotateHexagon = rotateHex;
            LevelCreatorData.Instance.xCells = xCells;
            LevelCreatorData.Instance.yCells = yCells;

			ResetGrid(gridValues);
        }

        private void RefreshLevelListUI()
        {
			if(Levels.Count == 0)
			{
                // Xóa DropdownField cũ nếu tồn tại
                var oldField = rootVisualElement.Q<DropdownField>("levelList");
                if (oldField != null)
                    oldField.RemoveFromHierarchy();
				LevelListField = null;
				return;
            }

            if (LevelListField == null)
			{
				CreateLevelListField();
                return;
			}

            LevelListField.choices = Levels;

            if (Levels.Count > 0)
                LevelListField.value = Levels[0];
        }

		private void CreateLevelListField()
		{
            if (Levels.Count == 0)
            {
                return;
            }

            SetupLevelListField();
        }

        private void LoadLevelsFromFolder()
        {
            Levels.Clear();

            string folder = GetOutputFolderAssetPath();
            if (!folder.StartsWith("Assets/Blocks/AssetFiles/LevelFiles/"))
                return;

            string fullFolderPath = Application.dataPath + folder.Substring("Assets".Length);

            if (!System.IO.Directory.Exists(fullFolderPath))
                return;

            string[] files = System.IO.Directory.GetFiles(fullFolderPath, "*.txt");

            foreach (string f in files)
            {
                Levels.Add(System.IO.Path.GetFileName(f));  // ví dụ: level 1.txt
            }
        }

		private void LoadMapClicked()
		{
			if (Levels.Count == 0 || LevelListField == null || string.IsNullOrEmpty(LevelListField.value))
            {
                Debug.LogError("No level selected!");
                return;
            }

            LoadLevelMap(LevelListField.value);
        }

        #endregion

        private void LevelTypeChanged(ChangeEvent<System.Enum> evt)
		{
			// There is a bug with binding enums, need to manually set it when it changes
			LevelType = (LevelData.LevelType)LevelTypeField.value;

			// If Hexagon level type was selected show the Rotate Hexagon field
			RotateHexagonField.style.display = (LevelType == LevelData.LevelType.Hexagon) ? DisplayStyle.Flex : DisplayStyle.None;

			RebuildGridCells();
			ResizeGrid();
		}

		private void RotateHexagonChanged(ChangeEvent<bool> evt)
		{
			RotateHexagon = RotateHexagonField.value;

			ResizeGrid();
		}

		private void XYCellsChanged(ChangeEvent<int> evt)
		{
			XCellsField.value	= Mathf.Clamp(XCellsField.value, 1, MaxCells);
			YCellsField.value	= Mathf.Clamp(YCellsField.value, 1, MaxCells);
			XCells				= XCellsField.value;
			YCells				= YCellsField.value;

			RebuildGridCells();
			ResizeGrid();
		}

		private void NumShapesChanged(ChangeEvent<int> evt)
		{
			NumShapesField.value	= Mathf.Clamp(NumShapesField.value, 1, int.MaxValue);
			NumShapes				= NumShapesField.value;

			selectedShapeIndex = Mathf.Clamp(selectedShapeIndex, 0, NumShapes);

			ValidateMinMaxShapeSize();
			GenerateShapeColors();
			UpdateGridCellColors();
			ResizeShapeBlocks();
		}

		private void MinMaxCellsChanged(ChangeEvent<int> evt)
		{
			ValidateMinMaxShapeSize();
		}

		private void ValidateMinMaxShapeSize()
		{
			int cellCount = CountCells();

			// Cell count is invalid, there are not enough empty cells to fit all shapes
			if (cellCount < NumShapes)
			{
				MinShapeSizeField.value = 1;
				MaxShapeSizeField.value = 1;

				return;
			}

			float cellsPerShape = (float)cellCount / (float)NumShapes;

			int maximumMinShapeSize = Mathf.Max(1, Mathf.FloorToInt(cellsPerShape));

			MinShapeSizeField.value		= Mathf.Clamp(MinShapeSizeField.value, 1, maximumMinShapeSize);
			MinShapeSize				= MinShapeSizeField.value;

			int minimumMaxShapeSize = Mathf.CeilToInt(cellsPerShape);
			int maximumMaxShapeSize = cellCount - MinShapeSize * (NumShapes - 1);

			MaxShapeSizeField.value		= Mathf.Clamp(MaxShapeSizeField.value, minimumMaxShapeSize, maximumMaxShapeSize);
			MaxShapeSize				= MaxShapeSizeField.value;
		}

		private int CountCells()
		{
			int xCells = XCells;
			int yCells = YCells;

			int count = 0;

			for (int y = 0; y < yCells; y++)
			{
				for (int x = 0; x < xCells; x++)
				{
					GridCell gridCell = grid[y][x];

					if (gridCell.shapeIndex >= EmptyCellShapeIndex)
					{
						count++;
					}
				}
			}

			return count;
		}

		private void UpdateOutputPaths()
		{
			if (OutputFolderField.value != null)
			{
				// Get the asset path of the object
				string assetPath = AssetDatabase.GetAssetPath(OutputFolderField.value);

				// Check if it is a directory
				bool isDir = ((System.IO.File.GetAttributes(assetPath) & System.IO.FileAttributes.Directory) == System.IO.FileAttributes.Directory);

				// Show the error message if it's not a directory
				rootVisualElement.Q("outputFolderErrorContainer").style.display = isDir ? DisplayStyle.None : DisplayStyle.Flex;

				if (isDir)
				{
					OutputFolderAssetPath = assetPath;
				}
			}
			else
			{
				OutputFolderAssetPath = null;
			}

			(rootVisualElement.Q("outputFolderPath") as TextElement).text = "Output path: " + GetOutputFileAssetPath(GetOutputFolderAssetPath());
		}

		private void ClearShapes()
		{
			int xCells = XCells;
			int yCells = YCells;

			for (int y = 0; y < yCells; y++)
			{
				for (int x = 0; x < xCells; x++)
				{
					GridCell gridCell = grid[y][x];

					// Set any shape index that is above the empty shape index (IE all the colors) back to the empty shape index
					if (gridCell.shapeIndex > EmptyCellShapeIndex)
					{
						gridCell.shapeIndex = EmptyCellShapeIndex;
					}
				}
			}

			UpdateGridCellColors();
		}

		private void OnResetGridBtn()
		{
			ResetGrid();
        }

		private void ResetGrid(List<List<int>> _inputData = null)
		{
			RebuildGridCells(_inputData);
			ResizeGrid();
		}

		private void RebuildGridCells(List<List<int>> _inputData = null)
		{
			VisualElement gridContainer = GridContainerElement;

			bool _hasInputData = _inputData != null && _inputData.Count > 0;

            gridContainer.Clear();
			grid.Clear();

			int					xCells		= XCells;
			int					yCells		= YCells;
			LevelData.LevelType	levelType	= LevelType;

			// Get the texture to use for the Image field
			Texture cellTexture = GetCellTexture(levelType);

			// Create a container that all the cells will be added to
			VisualElement gridCellContainer = new VisualElement();

			gridCellContainer.name = "gridCellContainer";

			gridCellContainer.RegisterCallback<MouseMoveEvent>(OnMouseMoved);
			gridCellContainer.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
			gridCellContainer.RegisterCallback<MouseDownEvent>(OnMouseDown);
			gridCellContainer.RegisterCallback<MouseUpEvent>(OnMouseUp);

			// Add the cell container to the grid container, the cell container will be centered because of grid-containers uss styling
			gridContainer.Add(gridCellContainer);

			for (int y = 0; y < yCells; y++)
			{
				grid.Add(new List<GridCell>());

				for (int x = 0; x < xCells; x++)
				{
					Image cellImage = new Image();

					cellImage.AddToClassList("grid-cell");

					cellImage.image = cellTexture;

					gridCellContainer.Add(cellImage);

					GridCell gridCell = new GridCell();

					gridCell.row		= y;
					gridCell.col		= x;

					if (_hasInputData)
					{
                        gridCell.imageField = cellImage;

						int _index = _inputData[y][x];

						gridCell.shapeIndex = _index;
                        cellImage.tintColor = GetShapeColor(_index);
                    }
					else
					{
                        gridCell.imageField = cellImage;
                        gridCell.shapeIndex = EmptyCellShapeIndex; // Default to the white color
                    }

					grid[y].Add(gridCell);
				}
			}

			ValidateMinMaxShapeSize();
		}

		private void ResizeGrid()
		{
			VisualElement gridContainer = GridContainerElement;

			int					xCells		= XCells;
			int					yCells		= YCells;
			LevelData.LevelType	levelType	= LevelType;

			// Get the max cell size for each cell
			float maxCellSize = GetMaxCellSize(gridContainer, levelType, xCells);

			// Get the cells width/height based on the type of level
			float cellWidth, cellHeight;
			GetCellSize(levelType, maxCellSize, out cellWidth, out cellHeight);

			// Set the width / height of the cell container to exactly what we need
			VisualElement gridCellContainer = gridContainer.Q("gridCellContainer");
			SetCellContainerSize(gridCellContainer, levelType, xCells, yCells, cellWidth, cellHeight);

			for (int y = 0; y < yCells; y++)
			{
				grid.Add(new List<GridCell>());

				for (int x = 0; x < xCells; x++)
				{
					GridCell gridCell = grid[y][x];

					Image cellImage = gridCell.imageField;

					// Set the width/height of the cell
					if (levelType == LevelData.LevelType.Hexagon && RotateHexagon)
					{
						cellImage.style.width	= cellHeight;
						cellImage.style.height	= cellWidth;
					}
					else
					{
						cellImage.style.width	= cellWidth;
						cellImage.style.height	= cellHeight;
					}

					// Position the cell in the container
					PositionCell(cellImage, levelType, x, y, cellWidth, cellHeight);
				}
			}
		}

		/// <summary>
		/// Gets the max width a cell can be on the grid
		/// </summary>
		private float GetMaxCellSize(VisualElement gridContainer, LevelData.LevelType levelType, int xCells)
		{
			float maxCellWidth = 0;

			switch (levelType)
			{
				case LevelData.LevelType.Square:
				{
					maxCellWidth = gridContainer.localBound.width / xCells;
					break;
				}
				case LevelData.LevelType.Triangle:
				{
					maxCellWidth = gridContainer.localBound.width / ((xCells + 1f) / 2f);
					break;
				}
				case LevelData.LevelType.Hexagon:
				{
					if (RotateHexagon)
					{
						float partWidth = gridContainer.localBound.width / (xCells + 0.25f);

						maxCellWidth = (4f/3f) * partWidth;
					}
					else
					{
						maxCellWidth = gridContainer.localBound.width / (xCells + 0.5f);
					}
					break;
				}
			}

			return Mathf.Min(MaxCellSize, maxCellWidth);
		}

		/// <summary>
		/// Gets a cells with/height on the grid
		/// </summary>
		private void GetCellSize(LevelData.LevelType levelType, float maxCellSize, out float cellWidth, out float cellHeight)
		{
			cellWidth	= 1;
			cellHeight	= 1;

			switch (levelType)
			{
				case LevelData.LevelType.Square:
				{
					cellWidth	= maxCellSize;
					cellHeight	= maxCellSize;
					break;
				}
				case LevelData.LevelType.Triangle:
				{
					cellWidth	= maxCellSize;
					cellHeight	= maxCellSize * ((float)TriangleTexture.height / (float)TriangleTexture.width);
					break;
				}
				case LevelData.LevelType.Hexagon:
				{
					if (RotateHexagon)
					{
						cellWidth	= maxCellSize;
						cellHeight	= maxCellSize * (Mathf.Sqrt(3) / 2f);
					}
					else
					{
						cellWidth	= maxCellSize;
						cellHeight	= maxCellSize / (Mathf.Sqrt(3) / 2f);
					}
					break;
				}
			}
		}

		/// <summary>
		/// Gets the Texture associated with the level type
		/// </summary>
		private Texture GetCellTexture(LevelData.LevelType levelType)
		{
			switch (levelType)
			{
				case LevelData.LevelType.Square:
				{
					return SquareTexture;
				}
				case LevelData.LevelType.Triangle:
				{
					return TriangleTexture;
				}
				case LevelData.LevelType.Hexagon:
				{
					return HexagonTexture;
				}
			}

			return null;
		}

		/// <summary>
		/// Sets the grid cell containers widht/height to be exactly what it needs to be to fit all cells on the grid
		/// </summary>
		private void SetCellContainerSize(VisualElement gridCellContainer, LevelData.LevelType levelType, int xCells, int yCells, float cellWidth, float cellHeight)
		{
			switch (levelType)
			{
				case LevelData.LevelType.Square:
				{
					gridCellContainer.style.width	= xCells * cellWidth;
					gridCellContainer.style.height	= yCells * cellHeight;
					break;
				}
				case LevelData.LevelType.Triangle:
				{
					gridCellContainer.style.width	= Mathf.Ceil(xCells / 2f) * cellWidth + (xCells % 2 == 0 ? cellWidth / 2f : 0);
					gridCellContainer.style.height	= yCells * cellHeight;
					break;
				}
				case LevelData.LevelType.Hexagon:
				{
					if (RotateHexagon)
					{
						float width = 0;

						width += Mathf.Ceil(xCells / 2f) * cellWidth;
						width += Mathf.Floor(xCells / 2f) * (cellWidth / 2f);
						width += (xCells % 2 == 0 ? cellWidth / 4f : 0);

						gridCellContainer.style.width	= width;
						gridCellContainer.style.height	= yCells * cellHeight + cellHeight / 2f;
					}
					else
					{
						float height = 0;

						height += Mathf.Ceil(yCells / 2f) * cellHeight;
						height += Mathf.Floor(yCells / 2f) * (cellHeight / 2f);
						height += (yCells % 2 == 0 ? cellHeight / 4f : 0);

						gridCellContainer.style.width	= xCells * cellWidth;
						gridCellContainer.style.height	= height;
					}
					break;
				}
			}
		}

		/// <summary>
		/// Positions the cell on the grid based on the level type
		/// </summary>
		private void PositionCell(VisualElement cell, LevelData.LevelType levelType, int x, int y, float cellWidth, float cellHeight)
		{
			switch (levelType)
			{
				case LevelData.LevelType.Square:
				{
					float xPos = x * cellWidth;
					float yPos = y * cellHeight;

					cell.transform.position = new Vector3(xPos, yPos, 0f);
					break;
				}
				case LevelData.LevelType.Triangle:
				{
                        // Tam giác đều - xếp sát nhau
                        float xPos = x * (cellWidth / 2f);
                        float yPos = y * cellHeight;

                        // Hàng lẻ shift sang phải nửa cell
                        if (y % 2 != 0)
                        {
                            xPos += cellWidth / 2f;
                        }

                        // Xác định lật tam giác (xoay 180)
                        bool upsideDown = (x + y) % 2 != 0;
                        if (upsideDown)
                            cell.transform.rotation = Quaternion.Euler(0, 0, 180);
                        else
                            cell.transform.rotation = Quaternion.identity;

                        cell.transform.position = new Vector3(xPos, yPos, 0f);

                        break;
				}
				case LevelData.LevelType.Hexagon:
				{
					if (RotateHexagon)
					{
						float xPos = x * ((3f / 4f) * cellWidth);
						float yPos = y * cellHeight;
						
						if (x % 2 != 0)
						{
							yPos += cellHeight / 2f;
						}

						// // Make sure the rotation is set to 90
						Quaternion rotation = new Quaternion();
						rotation.eulerAngles = new Vector3(0, 0, 90);
						cell.transform.rotation = rotation;

						xPos += cellWidth;

						cell.transform.position = new Vector3(xPos, yPos, 0f);
                        }
					else
					{
						float xPos = x * cellWidth - cellWidth / 4f;
						float yPos = y * ((3f / 4f) * cellHeight);
						
						if (y % 2 != 0)
						{
							xPos += cellWidth / 2f;
						}

						// // Make sure the rotation is set to 0
						Quaternion rotation = new Quaternion();
						rotation.eulerAngles = new Vector3(0, 0, 0);
						cell.transform.rotation = rotation;

						cell.transform.position = new Vector3(xPos, yPos, 0f);
                        }

                        
                        break;
				}
			}
		}

		/// <summary>
		/// Invoked when the mouse hovers over the grid cell container
		/// </summary>
		private void OnMouseMoved(MouseMoveEvent evt)
		{
			GridCell gridCell = GetClosestCell(evt.localMousePosition);

			// Only update if the hover target has changed
			if (hoveredGridCell != gridCell)
			{
				if (hoveredGridCell != null)
				{
					SetHover(hoveredGridCell, false);
				}

				SetHover(gridCell, true);

				hoveredGridCell = gridCell;
			}

			if (isDragging)
			{
				SetCellToSelectedShape(gridCell);
			}
		}

		/// <summary>
		/// Invoked when the mouse moves out of the grid cell container
		/// </summary>
		private void OnMouseLeave(MouseLeaveEvent evt)
		{
			if (hoveredGridCell != null)
			{
				SetHover(hoveredGridCell, false);
			}

			hoveredGridCell	= null;
			isDragging		= false;
		}

		/// <summary>
		/// Invoked when the mouse clicks on the grid cell container
		/// </summary>
		private void OnMouseDown(MouseDownEvent evt)
		{
			SetCellToSelectedShape(GetClosestCell(evt.localMousePosition));

			isDragging = true;
		}

		/// <summary>
		/// Invoked when the mouse clicks on the grid cell container
		/// </summary>
		private void OnMouseUp(MouseUpEvent evt)
		{
			isDragging = false;
		}

		/// <summary>
		/// Sets the cell to the currently selected shape
		/// </summary>
		private void SetCellToSelectedShape(GridCell gridCell)
		{
			if (gridCell.shapeIndex != selectedShapeIndex)
			{
				gridCell.imageField.tintColor	= GetShapeColor(selectedShapeIndex);
				gridCell.shapeIndex				= selectedShapeIndex;

			}
			else
			{
                gridCell.imageField.tintColor = GetShapeColor(EmptyCellShapeIndex);
                gridCell.shapeIndex = EmptyCellShapeIndex;
            }

            ValidateMinMaxShapeSize();
        }

		/// <summary>
		/// Gets the closest grid cell to the given mouse position
		/// </summary>
		private GridCell GetClosestCell(Vector2 mousePosition)
		{
            if (LevelType == LevelData.LevelType.Triangle)
                return GetClosestCell_Triangle(mousePosition);

            return GetClosestCell_Default(mousePosition);
        }

        private GridCell GetClosestCell_Triangle(Vector2 mousePosition)
        {
            int xCells = XCells;
            int yCells = YCells;

            float minDistance = float.MaxValue;
            int targetX = 0;
            int targetY = 0;

            for (int y = 0; y < yCells; y++)
            {
                for (int x = 0; x < xCells; x++)
                {
                    Image gridCell = grid[y][x].imageField;

                    float cellWidth = gridCell.style.width.value.value;
                    float cellHeight = gridCell.style.height.value.value;

                    // ===== Logic vẽ mới (KHÔNG offset upsideDown) =====
                    float xPos = x * (cellWidth / 2f);
                    float yPos = y * cellHeight;

                    if (y % 2 != 0)               // stagger hàng lẻ
                        xPos += cellWidth / 2f;

                    // Center
                    Vector2 cellMiddle = new Vector2(
                        xPos + cellWidth / 2f,
                        yPos + cellHeight / 2f
                    );

                    float distance = Vector2.Distance(cellMiddle, mousePosition);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        targetX = x;
                        targetY = y;
                    }
                }
            }

            return grid[targetY][targetX];
        }

        private GridCell GetClosestCell_Default(Vector2 mousePosition)
        {
            int xCells = XCells;
            int yCells = YCells;
            LevelData.LevelType levelType = LevelType;

            float minDistance = float.MaxValue;
            int targetX = 0;
            int targetY = 0;

            for (int y = 0; y < yCells; y++)
            {
                for (int x = 0; x < xCells; x++)
                {
                    Image gridCell = grid[y][x].imageField;
                    Vector2 cellMiddle = gridCell.transform.position;

                    float w = gridCell.style.width.value.value;
                    float h = gridCell.style.height.value.value;

                    cellMiddle.x += w / 2f;
                    cellMiddle.y += h / 2f;

                    // giữ nguyên logic cũ
                    if (levelType == LevelData.LevelType.Hexagon && RotateHexagon)
                        cellMiddle.x -= w;

                    float distance = Vector2.Distance(cellMiddle, mousePosition);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        targetX = x;
                        targetY = y;
                    }
                }
            }

            return grid[targetY][targetX];
        }

        private void SetHover(GridCell gridCell, bool isHovered)
		{
			if (isHovered)
			{
				Color hoverColor = GetShapeColor(selectedShapeIndex);
				
				if (selectedShapeIndex > 0)
				{
					hoverColor.a = 0.7f;
				}

				gridCell.imageField.tintColor = hoverColor;
			}
			else
			{
				gridCell.imageField.tintColor = GetShapeColor(gridCell.shapeIndex);
			}
		}

		private Color GetShapeColor(int shapeIndex)
		{
			Color color = Color.white;

			if (shapeIndex == BlankCellShapeIndex)
			{
				color.a	= 0.05f;
			}
			else
			{
				if(shapeColors.Count > 0)
				{
                    if (shapeIndex >= shapeColors.Count)
                    {
                        shapeIndex = shapeColors.Count - 1;
                    }

                    color = shapeColors[shapeIndex];
                }
			}

			return color;
		}

		/// <summary>
		/// Updates the number of color shapes used when creating manual levels
		/// </summary>
		private void ResizeShapeBlocks()
		{
			// Get the shpaes container and clear it
			VisualElement shapesContainer = ShapesContainerElement;

			shapesContainer.Clear();
			shapeBlocks.Clear();

			// Get the number of rows / cols we need to display all the shapes
			int numColors	= shapeColors.Count;
			int numCols		= Mathf.FloorToInt((shapesContainer.localBound.width + ShapeBlockSpacing) / (ShapeBlockSize + ShapeBlockSpacing));
			int numRows		= Mathf.CeilToInt((float)numColors / (float)numCols);

			int index = 0;

			for (int r = 0; r < numRows && index < numColors; r++)
			{
				// Add a row to the the shapes container
				VisualElement shapesRow = new VisualElement();

				shapesRow.AddToClassList("shapes-container-row");

				if (r > 0)
				{
					shapesRow.style.marginTop = ShapeBlockSpacing;
				}

				shapesContainer.Add(shapesRow);

				for (int c = 0; c < numCols && index < numColors; c++, index++)
				{
					VisualElement shapeBlock = new VisualElement();

					shapeBlock.style.width				= ShapeBlockSize;
					shapeBlock.style.height				= ShapeBlockSize;
					shapeBlock.style.backgroundColor	= GetShapeColor(index);

					// Give the block a border of size 2, we will set the border color to clear/white if the block is selected or not
					shapeBlock.style.borderBottomWidth	= 2f;
					shapeBlock.style.borderTopWidth		= 2f;
					shapeBlock.style.borderLeftWidth	= 2f;
					shapeBlock.style.borderRightWidth	= 2f;

					if (c > 0)
					{
						shapeBlock.style.marginLeft = ShapeBlockSpacing;
					}

					shapeBlock.RegisterCallback<MouseDownEvent>(OnShapeClicked);

					// Add the element to the row
					shapesRow.Add(shapeBlock);

					// Add the element to the list of active shape blocks
					shapeBlocks.Add(shapeBlock);
				}
			}

			if (shapeBlocks.Count > 0)
			{
				SetShapeBlockSelected(shapeBlocks[selectedShapeIndex], true);
			}
		}

		/// <summary>
		/// Creates a unique color for each shape
		/// </summary>
		private void GenerateShapeColors()
		{
			shapeColors.Clear();

			int		numShapes	= NumShapes;
			float	step		= 1f / numShapes;

			for (int i = 0; i < numShapes + EmptyCellShapeIndex + 1; i++)
			{
				if (i == BlankCellShapeIndex)
				{
					// Blank color
					shapeColors.Add(Color.grey);
				}
				else if (i == BlockCellShapeIndex)
				{
					// Block color
					shapeColors.Add(Color.black);
				}
				else if (i == EmptyCellShapeIndex)
				{
					// Empty cell color
					shapeColors.Add(Color.white);
				}
				else
				{
					shapeColors.Add(Color.HSVToRGB((i - (EmptyCellShapeIndex + 1)) * step, 1, 1));
				}
			}
		}

		/// <summary>
		/// Re-sets the tintColor on all GridCell imageFiles
		/// </summary>
		private void UpdateGridCellColors()
		{
			int xCells = XCells;
			int yCells = YCells;

			for (int y = 0; y < yCells; y++)
			{
				grid.Add(new List<GridCell>());

				for (int x = 0; x < xCells; x++)
				{
					GridCell	gridCell	= grid[y][x];
					Image		cellImage	= gridCell.imageField;

					if (gridCell.shapeIndex >= shapeColors.Count)
					{
						// Set it back to white
						gridCell.shapeIndex = EmptyCellShapeIndex;
					}

					cellImage.tintColor = GetShapeColor(gridCell.shapeIndex);
				}
			}
		}

		private void OnShapeClicked(MouseDownEvent evt)
		{
			VisualElement shapeBlock = evt.target as VisualElement;

			int selectedIndex = shapeBlocks.IndexOf(shapeBlock);

			if (selectedIndex != selectedShapeIndex)
			{
				SetShapeBlockSelected(shapeBlocks[selectedShapeIndex], false);
				SetShapeBlockSelected(shapeBlock, true);

				selectedShapeIndex = selectedIndex;
			}
		}

		private void SetShapeBlockSelected(VisualElement shapeBlock, bool isSelected)
		{
			shapeBlock.style.borderBottomColor	= isSelected ? Color.black : Color.clear;
			shapeBlock.style.borderTopColor		= isSelected ? Color.black : Color.clear;
			shapeBlock.style.borderLeftColor	= isSelected ? Color.black : Color.clear;
			shapeBlock.style.borderRightColor	= isSelected ? Color.black : Color.clear;
		}

		private void ExportClicked()
		{
			List<List<int>> gridCellValues = new List<List<int>>();

			// Convert the grid to the proper format for exporting
			for (int y = 0; y < YCells; y++)
			{
				gridCellValues.Add(new List<int>());

				for (int x = 0; x < XCells; x++)
				{
					GridCell gridCell = grid[y][x];

					gridCellValues[y].Add(gridCell.shapeIndex);
				}
			}

			Export(gridCellValues);

			AssetDatabase.Refresh();
		}

		/// <summary>
		/// Exports the level text file
		/// </summary>
		private void Export(List<List<int>> gridCellValues)
		{
			int yCells = gridCellValues.Count;
			int xCells = gridCellValues[0].Count;

			ulong timestamp = (ulong)Utilities.SystemTimeInMilliseconds;

			string contents = string.Format("{0},{1},{2},{3},{4}", timestamp, (int)LevelType, RotateHexagon, yCells, xCells);

			for (int y = 0; y < yCells; y++)
			{
				for (int x = 0; x < xCells; x++)
				{
					contents += "," + gridCellValues[y][x];
				}
			}

			string outputFolderAssetPath	= GetOutputFolderAssetPath();
			string outputFileAssetPath		= GetOutputFileAssetPath(outputFolderAssetPath);
			string outputFileFullPath		= Application.dataPath + outputFileAssetPath.Remove(0, "Assets".Length);

			System.IO.File.WriteAllText(outputFileFullPath, contents);
		}

		/// <summary>
		/// Gets the output folder asset path
		/// </summary>
		private string GetOutputFolderAssetPath()
		{
			if (OutputFolderField.value != null)
			{
				string	assetPath	= AssetDatabase.GetAssetPath(OutputFolderField.value);
				bool	isDir		= ((System.IO.File.GetAttributes(assetPath) & System.IO.FileAttributes.Directory) == System.IO.FileAttributes.Directory);

				if (isDir)
				{
					return assetPath;
				}
			}

			return "Assets";
		}

		/// <summary>
		/// Gets the output file asset path
		/// </summary>
		private string GetOutputFileAssetPath(string folderAssetPath)
		{
			string filename = string.IsNullOrEmpty(Filename) ? "level" : Filename;

			string path = folderAssetPath + "/" + filename + ".txt";

			return AssetDatabase.GenerateUniqueAssetPath(path);
		}

		private void AutoFill()
		{
			ClearShapes();

			List<List<int>> cellTypes = GetAllCellTypes();

			autoGenerationWorker = new AutoGenerationWorker(LevelType, RotateHexagon, XCells, YCells, cellTypes, NumShapes, MinShapeSize, MaxShapeSize);

			autoGenerationWorker.StartWorker();
		}

		private List<List<int>> GetAllCellTypes()
		{
			int xCells = XCells;
			int yCells = YCells;

			List<List<int>> cellTypes = new List<List<int>>();

			for (int y = 0; y < yCells; y++)
			{
				cellTypes.Add(new List<int>());
				
				for (int x = 0; x < xCells; x++)
				{
					GridCell gridCell = grid[y][x];

					if (gridCell.shapeIndex == BlankCellShapeIndex)
					{
						cellTypes[y].Add(0);
					}
					else if (gridCell.shapeIndex == BlockCellShapeIndex)
					{
						cellTypes[y].Add(1);
					}
					else
					{
						cellTypes[y].Add(-1);
					}
				}
			}

			return cellTypes;
		}

		private void GenerateBatch()
		{
			isBatchGenerating		= true;
			numLevelsLeftToGenerate	= NumberOfLevels;

			AutoFill();
		}

		private void AutoGenerationWorkerFinished()
		{
			List<List<int>> generatedGrid = autoGenerationWorker.GetGrid();

			if (isBatchGenerating)
			{
				numLevelsLeftToGenerate--;

				Export(generatedGrid);

				if (numLevelsLeftToGenerate > 0)
				{
					AutoFill();

					return;
				}
			}
			else
			{
				for (int y = 0; y < YCells; y++)
				{
					for (int x = 0; x < XCells; x++)
					{
						GridCell gridCell = grid[y][x];

						int cell = generatedGrid[y][x];

						if (cell > 1)
						{
							cell++;
						}

						gridCell.shapeIndex = cell;
					}
				}

				UpdateGridCellColors();
			}

			StopAutoGeneration();
		}

		private void StopAutoGeneration()
		{
			if (isBatchGenerating)
			{
				AssetDatabase.Refresh();
			}

			isBatchGenerating		= false;
			isGeneratingGrid		= false;
			autoGenerationWorker	= null;

			EditorUtility.ClearProgressBar();
		}
		
		#endregion // Private Methods
	}
}
