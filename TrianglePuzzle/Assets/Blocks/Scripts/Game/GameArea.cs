using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BBG.Blocks
{
	public class GameArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
	{
		#region Classes

		[System.Serializable]
		private class TilePrefabs
		{
			public Tile			tilePrefab				= null;
			public GameObject	emptyGridTilePrefab		= null;
			public GameObject	blockGridTilePrefab		= null;
			public GameObject	borderGridTilePrefab	= null;

			public ObjectPool TilePool 				{ get; set; }
			public ObjectPool BorderGridTilePool	{ get; set; }
			public ObjectPool BlockGridTilePool		{ get; set; }
			public ObjectPool EmptyGridTilePool		{ get; set; }
		}

		private class GridTile
		{
			public CellPos			cellPos				= null;
			public bool				placeable			= false;
			public RectTransform	tile				= null;
			public RectTransform	tileBorder			= null;
			public ShapeObject		placedShape			= null;
			public CellPos			placedShapeTilePos	= null;
			public ShapeObject		placedHintShape		= null;

			public bool CanPlaceShapeTile { get { return placeable && placedShape == null; } }
		}

		private class ShapeObject
		{
			public LevelData.Shape		shape					= null;
			public RectTransform		tileContainer			= null;
			public RectTransform		shapeContainerMarker	= null;
			public List<Tile>			tiles					= null;
			public bool					isOnGrid				= false;
			public bool					isActiveShape			= false;
		}

		#endregion

		#region Inspector Variables

		[SerializeField] private TilePrefabs 		squareTilePrefabs				= null;
		[SerializeField] private TilePrefabs 		triangleTilePrefabs				= null;
		[SerializeField] private TilePrefabs 		triangleFlippedTilePrefabs		= null;
		[SerializeField] private TilePrefabs 		hexagonVerticalTilePrefabs		= null;
		[SerializeField] private TilePrefabs 		hexagonHorizontalTilePrefabs	= null;
		[Space]
		[SerializeField] private RectTransform		gridContainer					= null;
		[SerializeField] private GridLayoutGroup	shapesContainer					= null;
		[Space]
		[SerializeField] private int				maxCellSize 					= 0;
		[SerializeField] private int				shapesInRow 					= 4;
		[SerializeField] private float				shapePlacementAlpha 			= 0.5f;
		[SerializeField] private List<Color> 		shapeColors 					= null;
		[Space]
		[SerializeField] private float				hintMinAlpha					= 0.5f;
		[SerializeField] private float				hintMaxAlpha					= 0.5f;
		[SerializeField] private AnimationCurve		hintAnimCurve					= null;
		[SerializeField] private float				hintAnimDuration				= 1f;	

		#endregion

		#region Member Variables

		private RectTransform 			gridPlacedShapesContainer;
		private RectTransform 			gridPlacedHintsContainer;
		private RectTransform 			gridEmptyTileContainer;
		private RectTransform 			gridBorderTileContainer;

		private LevelData				activeLevelData;
		private List<List<GridTile>>	gridTiles;
		private List<ShapeObject>		shapeObjects;
		private List<ShapeObject>		hintShapeObjects;
		private float					shapeScale;

		private bool					isPointerActive;
		private int						activePointerId;
		private ShapeObject				activeShapeObject;
		private ShapeObject				activeShapeObjectPlacement;

		#endregion

		#region Properties

		private RectTransform RectT { get { return transform as RectTransform; } }

		#endregion

		#region Public Variables

		public void Initialize()
		{
			shapeObjects		= new List<ShapeObject>();
			hintShapeObjects	= new List<ShapeObject>();
			gridTiles			= new List<List<GridTile>>();

			InitializeObjectPools();
			InitalizeGridContainers();

			GameEventManager.Instance.RegisterEventHandler(GameEventManager.ActiveLevelCompletedEventId, OnActiveLevelCompleted);
		}

		public void SetupLevel(LevelData levelData, LevelSaveData levelSaveData)
		{
			activeLevelData = levelData;

			Clear();

			CreateGrid(levelData);

			SetupShapesContainer(levelData.Shapes.Count);

			CreateShapes(levelData);

			// Move all the shapes that are placed in the save data to the grid
			for (int i = 0; i < levelSaveData.placedCellPositions.Count; i++)
			{
				CellPos cellPos = levelSaveData.placedCellPositions[i];

				if (cellPos != null)
				{
					MoveShapeToGrid(shapeObjects[i], cellPos);
				}
			}

			// Display any hints that where used
			for (int i = 0; i < levelSaveData.hintsPlaced.Count; i++)
			{
				DisplayHint(levelSaveData.hintsPlaced[i]);
			}
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			// If there is already an active pointer or the active level data is now null then ignore this event
			if (isPointerActive || activeLevelData == null)
			{
				return;
			}

			isPointerActive = true;
			activePointerId = eventData.pointerId;

			Vector2 mouseScreenPosition = eventData.position;

			// Check if the mouse is in the grid container, if so we need to check if the player clicked on a shape in the grid and start dragging it
			if (RectTransformUtility.RectangleContainsScreenPoint(gridContainer, mouseScreenPosition))
			{
				TryStartDraggingShapeOnGrid(mouseScreenPosition);
			}
			// Check if the mouse is in the shapes container, if so we need to check if the player clicked on a shape and start dragging it
			else if (RectTransformUtility.RectangleContainsScreenPoint(shapesContainer.transform as RectTransform, mouseScreenPosition))
			{
				TryStartDraggingShapeInContainer(mouseScreenPosition);
			}

			// If activeShapeObject is not null then a shape was selected
			if (activeShapeObject != null)
			{
				// Create a ShapeObject that will be displayed on the grid as a preview/plcement shape when dragging
				activeShapeObjectPlacement = CreateShape(activeLevelData, activeShapeObject.shape, false);
				activeShapeObjectPlacement.tileContainer.gameObject.AddComponent<CanvasGroup>().alpha = shapePlacementAlpha;
				activeShapeObjectPlacement.tileContainer.gameObject.SetActive(false);

				SoundManager.Instance.Play("shape-selected");
			}
		}

		public void OnDrag(PointerEventData eventData)
		{
			// If the event is not for the active down pointer then ignore this event
			if (!isPointerActive || eventData.pointerId != activePointerId || activeShapeObject == null)
			{
				return;
			}

			UpdateActiveShapeObjectPosition(eventData.position);

			// Try and get a valid grid cell position to place the active shape
			CellPos cellPos = TryGetValidGridCellPos();

			activeShapeObjectPlacement.tileContainer.gameObject.SetActive(cellPos != null);

			if (cellPos != null)
			{
				// If there is a valid position then place active shape placement object at that location
				MoveShapeToGrid(activeShapeObjectPlacement, cellPos);
			}
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			// If the event is not for the active down pointer then ignore this event
			if (!isPointerActive || eventData.pointerId != activePointerId)
			{
				return;
			}

			if (activeShapeObject != null)
			{
				CellPos placedCellPos;

				// Try and place the active shape on the grid at it's current location over the grid
				if (TryPlaceActiveShapeOnGrid(out placedCellPos))
				{
					GameManager.Instance.SetShapePlaced(activeShapeObject.shape, placedCellPos);
				}
				else
				{
					// Shape cannot be placed on the grid at it's current location, move it back to the shapes container
					MoveToShapeMarker(activeShapeObject);

					GameManager.Instance.SetShapePlaced(activeShapeObject.shape, null);
				}

				// Destroy the shape placement object
				for (int i = 0; i < activeShapeObjectPlacement.tiles.Count; i++)
				{
					ObjectPool.ReturnObjectToPool(activeShapeObjectPlacement.tiles[i].gameObject);
				}

				Destroy(activeShapeObjectPlacement.tileContainer.gameObject);

				SoundManager.Instance.Play("shape-placed");
			}

			isPointerActive		= false;
			activeShapeObject	= null;
		}

		/// <summary>
		/// Displays the shape as a hint on the grid
		/// </summary>
		public void DisplayHint(int shapeIndex)
		{
			LevelData.Shape	shape			= activeLevelData.Shapes[shapeIndex];
			ShapeObject		hintShapeObject	= CreateShape(activeLevelData, shape, false);

			hintShapeObjects.Add(hintShapeObject);

			// Add a CanvasGroup to the tileContainer and set the alpha
			CanvasGroup canvasGroup = hintShapeObject.tileContainer.gameObject.AddComponent<CanvasGroup>();

			// Start the animation that will fade in/out the hint
			UIAnimation animation = UIAnimation.Alpha(canvasGroup, hintMinAlpha, hintMaxAlpha, hintAnimDuration);

			animation.style				= UIAnimation.Style.Custom;
			animation.animationCurve	= hintAnimCurve;
			animation.loopType			= UIAnimation.LoopType.Reverse;
			animation.startOnFirstFrame	= true;

			animation.Play();

			// Move the hint shape to the grid container
			MoveShapeToGrid(hintShapeObject, shape.Anchor);
		}

		#endregion

		#region Private Variables

		/// <summary>
		/// Creates the ObjectPools for all the TilePrefabs
		/// </summary>
		private void InitializeObjectPools()
		{
			Transform poolContainer = ObjectPool.CreatePoolContainer(transform);

			InitializeObjectPools(squareTilePrefabs, poolContainer);
			InitializeObjectPools(triangleTilePrefabs, poolContainer);
			InitializeObjectPools(triangleFlippedTilePrefabs, poolContainer);
			InitializeObjectPools(hexagonVerticalTilePrefabs, poolContainer);
			InitializeObjectPools(hexagonHorizontalTilePrefabs, poolContainer);
		}		

		/// <summary>
		/// Creates the ObjectPools for the givne TilePrefabs
		/// </summary>
		private void InitializeObjectPools(TilePrefabs tilePrefabs, Transform poolContainer)
		{
			tilePrefabs.TilePool			= CreateObjectPool(tilePrefabs.tilePrefab.gameObject, poolContainer);
			tilePrefabs.EmptyGridTilePool	= CreateObjectPool(tilePrefabs.emptyGridTilePrefab, poolContainer);
			tilePrefabs.BlockGridTilePool	= CreateObjectPool(tilePrefabs.blockGridTilePrefab, poolContainer);
			tilePrefabs.BorderGridTilePool	= CreateObjectPool(tilePrefabs.borderGridTilePrefab, poolContainer);
		}

		/// <summary>
		/// Creates an ObjectPool if the given prefab is not null
		/// </summary>
		private ObjectPool CreateObjectPool(GameObject prefab, Transform poolContainer)
		{
			return (prefab != null) ? new ObjectPool(prefab, 1, poolContainer, ObjectPool.PoolBehaviour.CanvasGroup) : null;
		}

		/// <summary>
		/// Creates the GameObject containers that will hold the various grid tile objects
		/// </summary>
		private void InitalizeGridContainers()
		{
			gridBorderTileContainer		= CreateGridTileContainer("border_tile_container");
			gridEmptyTileContainer		= CreateGridTileContainer("empty_tile_container");
			gridPlacedShapesContainer	= CreateGridTileContainer("placed_shapes_container");
			gridPlacedHintsContainer	= CreateGridTileContainer("placed_hints_container");
		}

		/// <summary>
		/// Creates a GameObject container
		/// </summary>
		private RectTransform CreateGridTileContainer(string name)
		{
			GameObject		container		= new GameObject(name);
			RectTransform	containerRectT	= container.AddComponent<RectTransform>();

			containerRectT.SetParent(gridContainer, false);

			// Set anchors to expand to fill
			containerRectT.anchorMin = Vector2.zero;
			containerRectT.anchorMax = Vector2.one;
			containerRectT.offsetMin = Vector2.zero;
			containerRectT.offsetMax = Vector2.zero;

			return containerRectT;
		}

		/// <summary>
		/// Invoked when the GameManager determines the active level has been completed
		/// </summary>
		private void OnActiveLevelCompleted(string eventId, object[] data)
		{
			// Set the active level data to null so mouse events will be ignored until the next level starts
			activeLevelData = null;
		}

		/// <summary>
		/// Removes all tiles and shapes from the game area
		/// </summary>
		private void Clear()
		{
			// Return all the tiles to their pool
			ReturnAllTilesToPool(hexagonHorizontalTilePrefabs);
			ReturnAllTilesToPool(hexagonVerticalTilePrefabs);
			ReturnAllTilesToPool(triangleTilePrefabs);
			ReturnAllTilesToPool(triangleFlippedTilePrefabs);
			ReturnAllTilesToPool(squareTilePrefabs);

			// Destroy all the shape objects
			for (int i = 0; i < shapeObjects.Count; i++)
			{
				ShapeObject shapeObject = shapeObjects[i];

				Destroy(shapeObject.tileContainer.gameObject);
				Destroy(shapeObject.shapeContainerMarker.gameObject);
			}

			for (int i = 0; i < hintShapeObjects.Count; i++)
			{
				Destroy(hintShapeObjects[i].tileContainer.gameObject);
			}

			shapeObjects.Clear();
			hintShapeObjects.Clear();
			gridTiles.Clear();
		}

		/// <summary>
		/// Calls ReturnAllObjectsToPool on the 4 pools in the TilePrefabs object
		/// </summary>
		private void ReturnAllTilesToPool(TilePrefabs tilePrefabs)
		{
			if (tilePrefabs.BlockGridTilePool != null)
			{
				tilePrefabs.BlockGridTilePool.ReturnAllObjectsToPool();
			}

			if (tilePrefabs.BorderGridTilePool != null)
			{
				tilePrefabs.BorderGridTilePool.ReturnAllObjectsToPool();
			}

			if (tilePrefabs.EmptyGridTilePool != null)
			{
				tilePrefabs.EmptyGridTilePool.ReturnAllObjectsToPool();
			}

			if (tilePrefabs.TilePool != null)
			{
				tilePrefabs.TilePool.ReturnAllObjectsToPool();
			}
		}

		/// <summary>
		/// Creates the grid tiles for the level
		/// </summary>
		private void CreateGrid(LevelData levelData)
		{
			// Get the size of a single tile on the board based on the type of level
			Vector2 tileSize = GetTileSize(levelData);

			Rect gridBounds = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

			// Start creating all tiles
			for (int y = 0; y < levelData.YCells; y++)
			{
				gridTiles.Add(new List<GridTile>());

				for (int x = 0; x < levelData.XCells; x++)
				{
					LevelData.CellType cellType = levelData.GridCellTypes[y][x];

					// We won't place any tiles for blank cells
					if (cellType == LevelData.CellType.Blank)
					{
						gridTiles[y].Add(null);
						continue;
					}

					// Create the tile and get its bounds
					GridTile gridTile = CreateGriTile(levelData, x, y, tileSize, cellType);

					gridTiles[y].Add(gridTile);


					// Get the min/max values, when we are done adding all the tiles we will have the bounds of the grid
					gridBounds.xMin = Mathf.Min(gridBounds.xMin, gridTile.tile.anchoredPosition.x - tileSize.x / 2f);
					gridBounds.yMin = Mathf.Min(gridBounds.yMin, gridTile.tile.anchoredPosition.y - tileSize.y / 2f);
					gridBounds.xMax = Mathf.Max(gridBounds.xMax, gridTile.tile.anchoredPosition.x + tileSize.x / 2f);
					gridBounds.yMax = Mathf.Max(gridBounds.yMax, gridTile.tile.anchoredPosition.y + tileSize.y / 2f);
				}
			}

			Vector2 offset = new Vector2(gridBounds.xMin + gridBounds.size.x / 2f, gridBounds.yMax - gridBounds.size.y / 2f);

			// Adjust all the tile positions so the grid is centered in the gridContainer
			for (int y = 0; y < levelData.YCells; y++)
			{
				for (int x = 0; x < levelData.XCells; x++)
				{
					GridTile gridTile = gridTiles[y][x];

					if (gridTile != null)
					{
						// Re-position the tile
						gridTile.tile.anchoredPosition = gridTile.tile.anchoredPosition - offset;

						// Re-position the border
						gridTile.tileBorder.anchoredPosition = gridTile.tileBorder.anchoredPosition - offset;
					}
				}
			}
		}

		private Vector2 GetTileSize(LevelData levelData)
		{
			switch (levelData.Type)
			{
				case LevelData.LevelType.Square:
					return GetSquareTileSize(levelData);
				case LevelData.LevelType.Triangle:
					return GetTriangleTileSize(levelData);
				case LevelData.LevelType.Hexagon:
					return GetHexagonTileSize(levelData);
			}

			return Vector2.zero;
		}

		private Vector2 GetSquareTileSize(LevelData levelData)
		{
			// Get the square tile size on the grid
			float	maxTileWidth	= gridContainer.rect.width / levelData.XCells;
			float	maxTileHeight	= gridContainer.rect.height / levelData.YCells;
			float	tileSize		= Mathf.Min(maxCellSize, maxTileWidth, maxTileHeight);

			return new Vector2(tileSize, tileSize);
		}

		private Vector2 GetTriangleTileSize(LevelData levelData)
		{
			// Get max width and max height a triangle can be
			float maxTileWidth	= Mathf.Min(maxCellSize, gridContainer.rect.width / ((levelData.XCells + 1) / 2f));
			float maxTileHeight	= Mathf.Min(maxCellSize, gridContainer.rect.height / levelData.YCells);

			// Get the width/height of a triangle so all triangles can fit on the grid
			float tileWidth, tileHeight;

			// First try using the max width and check if the height fits on the grid
			tileWidth	= maxTileWidth;
			tileHeight	= maxTileWidth * (Mathf.Sqrt(3f) / 2f);

			if (tileHeight * levelData.YCells > gridContainer.rect.height)
			{
				// Using maxTileWidth will make the grid overflow on the height so we need to use the maxTileHeight instead and calculate the width
				tileWidth	= maxTileHeight * (2f / Mathf.Sqrt(3f));
				tileHeight	= maxTileHeight;
			}

			return new Vector2(tileWidth, tileHeight);
		}

		private Vector2 GetHexagonTileSize(LevelData levelData)
		{
			float maxTileWidth	= 0;
			float maxTileHeight	= 0;

			float tileWidth, tileHeight;

			if (levelData.IsVertHexagons)
			{
				maxTileWidth	= Mathf.Min(maxCellSize, gridContainer.rect.width / (levelData.XCells + 0.5f));
				maxTileHeight	= Mathf.Min(maxCellSize, (4f/3f) * (gridContainer.rect.height / (levelData.YCells + 0.25f)));

				tileWidth 	= maxTileWidth;
				tileHeight	= maxTileWidth * (2f / Mathf.Sqrt(3f));

				float totalHeight = (3f / 4f) * tileHeight * levelData.YCells + (1f / 4f) * tileHeight;

				if (totalHeight > gridContainer.rect.height)
				{
					tileWidth	= maxTileHeight * (Mathf.Sqrt(3f) / 2f);
					tileHeight	= maxTileHeight;
				}
			}
			else
			{
				maxTileHeight	= Mathf.Min(maxCellSize, gridContainer.rect.height / (levelData.YCells + 0.5f));
				maxTileWidth	= Mathf.Min(maxCellSize, (4f/3f) * (gridContainer.rect.width / (levelData.XCells + 0.25f)));

				tileWidth 	= maxTileWidth;
				tileHeight	= maxTileWidth * (Mathf.Sqrt(3f) / 2f);

				float totalHeight = tileHeight * (levelData.YCells + 0.5f);

				if (totalHeight > gridContainer.rect.height)
				{
					tileWidth	= maxTileHeight * (2f / Mathf.Sqrt(3f));
					tileHeight	= maxTileHeight;
				}
			}

			return new Vector2(tileWidth, tileHeight);
		}

		private GridTile CreateGriTile(LevelData levelData, int x, int y, Vector2 tileSize, LevelData.CellType cellType)
		{
			RectTransform tile			= GetGridTile(levelData, x, y, cellType);
			RectTransform borderTile	= GetGridBorderTile(levelData, x, y, cellType);

			// Scale the tile so it matchs the proper size
			float xScale = tileSize.x / tile.rect.width;
			float yScale = tileSize.y / tile.rect.height;

			tile.localScale			= new Vector3(xScale, yScale, 1f);
			borderTile.localScale	= new Vector3(xScale, yScale, 1f);

			// Position the tile
			Vector2 tilePosition = GetTilePosition(levelData, tile, x, y, tileSize);

			tile.anchoredPosition		= tilePosition;
			borderTile.anchoredPosition	= tilePosition;

			GridTile gridTile = new GridTile();

			gridTile.cellPos	= new CellPos(x, y);
			gridTile.tile		= tile;
			gridTile.tileBorder	= borderTile;
			gridTile.placeable	= cellType == LevelData.CellType.Normal;

			SetDebugText(gridTile, "");

			return gridTile;
		}

		private RectTransform GetGridTile(LevelData levelData, int x, int y, LevelData.CellType cellType)
		{
			TilePrefabs tilePrefabs = GetTilePrefabs(levelData, x, y);

			// Get the tile to display
			switch (cellType)
			{
				case LevelData.CellType.Block:
					return tilePrefabs.BlockGridTilePool.GetObject<RectTransform>(gridPlacedShapesContainer);
				case LevelData.CellType.Normal:
					return tilePrefabs.EmptyGridTilePool.GetObject<RectTransform>(gridEmptyTileContainer);
			}

			return null;
		}

		private RectTransform GetGridBorderTile(LevelData levelData, int x, int y, LevelData.CellType cellType)
		{
			TilePrefabs tilePrefabs = GetTilePrefabs(levelData, x, y);

			return tilePrefabs.BorderGridTilePool.GetObject<RectTransform>(gridBorderTileContainer);
		}

		private TilePrefabs GetTilePrefabs(LevelData levelData, int x, int y)
		{
			switch (levelData.Type)
			{
				case LevelData.LevelType.Square:
					return squareTilePrefabs;
				case LevelData.LevelType.Triangle:
					return (x + y) % 2 == 0 ? triangleTilePrefabs : triangleFlippedTilePrefabs;
				case LevelData.LevelType.Hexagon:
					return (levelData.IsVertHexagons ? hexagonVerticalTilePrefabs : hexagonHorizontalTilePrefabs);
			}

			return null;
		}

		private Vector2 GetTilePosition(LevelData levelData, RectTransform tile, int x, int y, Vector2 tileSize)
		{
			switch (levelData.Type)
			{
				case LevelData.LevelType.Square:
					return GetSquareTilePosition(levelData, tile, x, y, tileSize);
				case LevelData.LevelType.Triangle:
					return GetTriangleTilePosition(levelData, tile, x, y, tileSize);
				case LevelData.LevelType.Hexagon:
					return GetHexagonTilePosition(levelData, tile, x, y, tileSize);
			}

			return Vector2.zero;
		}

		private Vector2 GetSquareTilePosition(LevelData levelData, RectTransform tile, int x, int y, Vector2 tileSize)
		{
			float xPos = x * tileSize.x;
			float yPos = -y * tileSize.y;

			return new Vector2(xPos, yPos);
		}

		private Vector2 GetTriangleTilePosition(LevelData levelData, RectTransform tile, int x, int y, Vector2 tileSize)
		{
			bool	upsideDown	= (x + y) % 2 != 0;
			float	xPos		= x * (tileSize.x / 2f);
			float	yPos		= -y * tileSize.y;

			return new Vector2(xPos, yPos);
		}

		private Vector2 GetHexagonTilePosition(LevelData levelData, RectTransform tile, int x, int y, Vector2 tileSize)
		{
			float xPos = x * tileSize.x;
			float yPos = -y * tileSize.y * (3f / 4f);

			if (levelData.IsVertHexagons)
			{
				xPos = x * tileSize.x;
				yPos = -y * tileSize.y * (3f / 4f);

				if (y % 2 == 1)
				{
					xPos += tileSize.x / 2f;
				}
			}
			else
			{
				xPos = x * tileSize.x * (3f / 4f);
				yPos = -y * tileSize.y;

				if (x % 2 == 1)
				{
					yPos -= tileSize.y / 2f;
				}
			}

			return new Vector2(xPos, yPos);
		}

		/// <summary>
		/// Sets the number or columns/rows in the shapesContainer grid
		/// </summary>
		private void SetupShapesContainer(int numShapes)
		{
			int		cols		= shapesInRow;
			int		rows		= 1;
			bool	growRowNext = true;

			// Increase the number of rows/cols in the grid until we have enough spots to place each shape
			while (numShapes > rows * cols)
			{
				if (growRowNext)
				{
					rows++;
				}
				else
				{
					cols++;
				}

				growRowNext = !growRowNext;
			}

			// Set the column constraint so when we add items to the GridLayoutGroup they will auto position
			shapesContainer.constraint		= GridLayoutGroup.Constraint.FixedColumnCount;
			shapesContainer.constraintCount	= cols;

			Vector2 containerSize = (shapesContainer.transform as RectTransform).rect.size;

			// Set the size of a cell in the grid to the max size it can be given the number of columns/rows
			float cellWidth		= (containerSize.x - shapesContainer.spacing.x * (cols - 1) - shapesContainer.padding.left - shapesContainer.padding.right) / cols;
			float cellHeight	= (containerSize.y - shapesContainer.spacing.y * (rows - 1) - shapesContainer.padding.top - shapesContainer.padding.bottom) / rows;

			shapesContainer.cellSize = new Vector2(cellWidth, cellHeight);
		}

		/// <summary>
		/// Creates the tiles for each of the shapes
		/// </summary>
		private void CreateShapes(LevelData levelData)
		{
			shapeScale = float.MaxValue;

			// Create all the shape objects
			for (int i = 0; i < levelData.Shapes.Count; i++)
			{
				LevelData.Shape	shape		= levelData.Shapes[i];
				ShapeObject		shapeObject	= CreateShape(levelData, shape, true);

				shapeObjects.Add(shapeObject);

				// Create a placement marker object for the shape in the shapesContainer
				shapeObject.shapeContainerMarker = new GameObject("shape_marker").AddComponent<RectTransform>();
				shapeObject.shapeContainerMarker.SetParent(shapesContainer.transform, false);
			}

			// Now move all shapes to their shapeContainer marker
			for (int i = 0; i < shapeObjects.Count; i++)
			{
				MoveToShapeMarker(shapeObjects[i]);
			}
		}

		private ShapeObject CreateShape(LevelData levelData, LevelData.Shape shape, bool isActiveShape)
		{
			ShapeObject	shapeObject	= new ShapeObject();

			// Set the shape the shape object is for
			shapeObject.shape			= shape;
			shapeObject.tiles			= new List<Tile>();
			shapeObject.isActiveShape	= isActiveShape;

			// Create a container that will hold all the shapes tiles
			shapeObject.tileContainer = new GameObject("tile_container").AddComponent<RectTransform>();
			shapeObject.tileContainer.sizeDelta = Vector2.zero;

			// Create all the tiles for the shape
			switch (levelData.Type)
			{
				case LevelData.LevelType.Square:
					CreateSquareShape(shape, shapeObject);
					break;
				case LevelData.LevelType.Triangle:
					CreateTriangleShape(shape, shapeObject);
					break;
				case LevelData.LevelType.Hexagon:
					CreateHexagonShape(shape, shapeObject, levelData.IsVertHexagons);
					break;
			}

			// Now that tileContainers size has been set, find the min scale needed to fit all shapes in their respective marker
			float shapeWidth	= shapeObject.tileContainer.rect.width;
			float shapeHeight	= shapeObject.tileContainer.rect.height;
			float cellWidth		= shapesContainer.cellSize.x;
			float cellHeight	= shapesContainer.cellSize.y;

			float thisShapeScale;

			if (shapeWidth - cellWidth > shapeHeight - cellHeight)
			{
				thisShapeScale = cellWidth / shapeWidth;
			}
			else
			{
				thisShapeScale = cellHeight / shapeHeight;
			}

			shapeScale = Mathf.Min(shapeScale, thisShapeScale);

			// Set the color of the tiles
			SetShapeTileColor(shapeObject);

			return shapeObject;
		}

		private void CreateSquareShape(LevelData.Shape shape, ShapeObject shapeObject)
		{
			float	tileWidth	= (squareTilePrefabs.tilePrefab.transform as RectTransform).rect.width;
			float	tileHeight	= (squareTilePrefabs.tilePrefab.transform as RectTransform).rect.height;
			Vector2	tileSize	= new Vector2(tileWidth, tileHeight);

			float 	shapeWidth	= shape.bounds.width * tileWidth;
			float	shapeHeight	= shape.bounds.height * tileHeight;
			Vector2	shapeSize	= new Vector2(shapeWidth, shapeHeight);

			// Set the size of the tileContainer to the size of the shape
			shapeObject.tileContainer.sizeDelta = shapeSize;

			for (int i = 0; i < shape.cellPositions.Count; i++)
			{
				// Get the x/y cell on the grid for this tile
				CellPos cellPosition = shape.cellPositions[i];
				CellPos shapeCellPos = new CellPos(cellPosition.x - shape.bounds.xMin, cellPosition.y - shape.bounds.yMin);

				// Create the tile object
				Tile tile = squareTilePrefabs.TilePool.GetObject<Tile>(shapeObject.tileContainer);

				shapeObject.tiles.Add(tile);

				// Position the square tile
				PositionSquareTile(tile.transform as RectTransform, shapeCellPos, tileSize, shapeSize);
			}
		}

		private void CreateTriangleShape(LevelData.Shape shape, ShapeObject shapeObject)
		{
			float	tileWidth	= (triangleTilePrefabs.tilePrefab.transform as RectTransform).rect.width;
			float	tileHeight	= (triangleTilePrefabs.tilePrefab.transform as RectTransform).rect.height;
			Vector2	tileSize	= new Vector2(tileWidth, tileHeight);

			float 	shapeWidth	= ((shape.bounds.width + 1) / 2f) * tileWidth;
			float	shapeHeight	= shape.bounds.height * tileHeight;
			Vector2	shapeSize	= new Vector2(shapeWidth, shapeHeight);

			// Set the size of the tileContainer to the size of the shape
			shapeObject.tileContainer.sizeDelta = shapeSize;

			for (int i = 0; i < shape.cellPositions.Count; i++)
			{
				// Get the x/y cell on the grid for this tile
				CellPos cellPosition = shape.cellPositions[i];
				CellPos shapeCellPos = new CellPos(cellPosition.x - shape.bounds.xMin, cellPosition.y - shape.bounds.yMin);

				// Create the tile object
				Tile tile = null;
				
				if ((cellPosition.x + cellPosition.y) % 2 == 0)
				{
					tile = triangleTilePrefabs.TilePool.GetObject<Tile>(shapeObject.tileContainer);
				}
				else
				{
					tile = triangleFlippedTilePrefabs.TilePool.GetObject<Tile>(shapeObject.tileContainer);
				}

				shapeObject.tiles.Add(tile);

				// Position the square tile
				PositionTriangleTile(tile.RectT, shapeCellPos, tileSize, shapeSize);
			}
		}

		private void CreateHexagonShape(LevelData.Shape shape, ShapeObject shapeObject, bool isVertHexagons)
		{
			float tileWidth, tileHeight;

			if (isVertHexagons)
			{
				tileWidth	= (hexagonVerticalTilePrefabs.tilePrefab.transform as RectTransform).rect.width;
				tileHeight	= (hexagonVerticalTilePrefabs.tilePrefab.transform as RectTransform).rect.height;
			}
			else
			{
				tileWidth	= (hexagonHorizontalTilePrefabs.tilePrefab.transform as RectTransform).rect.width;
				tileHeight	= (hexagonHorizontalTilePrefabs.tilePrefab.transform as RectTransform).rect.height;
			}

			Vector2	tileSize = new Vector2(tileWidth, tileHeight);

			for (int i = 0; i < shape.cellPositions.Count; i++)
			{
				// Get the x/y cell on the grid for this tile
				CellPos cellPosition = shape.cellPositions[i];

				float xCellPos = cellPosition.x - shape.bounds.xMin;
				float yCellPos = cellPosition.y - shape.bounds.yMin;

				// Create the tile object
				Tile tile = null;
				
				if (isVertHexagons)
				{
					tile = hexagonVerticalTilePrefabs.TilePool.GetObject<Tile>(shapeObject.tileContainer);

					if (cellPosition.y % 2 == 1)
					{
						xCellPos += 0.5f;
					}
				}
				else
				{
					tile = hexagonHorizontalTilePrefabs.TilePool.GetObject<Tile>(shapeObject.tileContainer);

					if (cellPosition.x % 2 == 1)
					{
						yCellPos += 0.5f;
					}
				}

				shapeObject.tiles.Add(tile);

				// Position the square tile
				PositionHexagonTile(tile.RectT, xCellPos, yCellPos, tileSize, isVertHexagons);
			}

			Rect shapeBounds = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

			// Set the size of the shape tileContainer, this way is easier than doing math
			for (int i = 0; i < shapeObject.tiles.Count; i++)
			{
				RectTransform shapeTile = shapeObject.tiles[i].RectT;

				shapeBounds.xMin = Mathf.Min(shapeBounds.xMin, shapeTile.anchoredPosition.x - tileSize.x / 2f);
				shapeBounds.yMin = Mathf.Min(shapeBounds.yMin, shapeTile.anchoredPosition.y - tileSize.y / 2f);
				shapeBounds.xMax = Mathf.Max(shapeBounds.xMax, shapeTile.anchoredPosition.x + tileSize.x / 2f);
				shapeBounds.yMax = Mathf.Max(shapeBounds.yMax, shapeTile.anchoredPosition.y + tileSize.y / 2f);
			}

			shapeObject.tileContainer.sizeDelta = shapeBounds.size;

			Vector2 offset = new Vector2(shapeBounds.xMin + shapeBounds.size.x / 2f, shapeBounds.yMax - shapeBounds.size.y / 2f);

			for (int i = 0; i < shapeObject.tiles.Count; i++)
			{
				RectTransform shapeTile = shapeObject.tiles[i].RectT;

				shapeTile.anchoredPosition = shapeTile.anchoredPosition - offset;
			}
		}

		private void PositionSquareTile(RectTransform tileRect, CellPos cellPosition, Vector2 tileSize, Vector2 shapeSize)
		{
			Vector2 tilePosition = new Vector2();

			// Position the tile in the top/left corner
			tilePosition.x -= shapeSize.x / 2f;
			tilePosition.y += shapeSize.y / 2f;

			tilePosition.x += tileSize.x / 2f;
			tilePosition.y -= tileSize.y / 2f;

			// Move the tile to it's proper cell position
			tilePosition.x += cellPosition.x * tileSize.x;
			tilePosition.y -= cellPosition.y * tileSize.y;

			tileRect.anchoredPosition = tilePosition;
		}

		private void PositionTriangleTile(RectTransform tileRect, CellPos cellPosition, Vector2 tileSize, Vector2 shapeSize)
		{
			Vector2 tilePosition = new Vector2();

			// Position the tile in the top/left corner
			tilePosition.x -= shapeSize.x / 2f;
			tilePosition.y += shapeSize.y / 2f;

			tilePosition.x += tileSize.x / 2f;
			tilePosition.y -= tileSize.y / 2f;

			// Move the tile to it's proper cell position
			tilePosition.x += cellPosition.x * (tileSize.x / 2f);
			tilePosition.y -= cellPosition.y * tileSize.y;

			tileRect.anchoredPosition = tilePosition;
		}

		private void PositionHexagonTile(RectTransform tileRect, float xCellPos, float yCellPos, Vector2 tileSize, bool isVertHexagons)
		{
			Vector2 tilePosition = new Vector2();

			tilePosition.x += tileSize.x / 2f;
			tilePosition.y -= tileSize.y / 2f;

			// Move the tile to it's proper cell position
			if (isVertHexagons)
			{
				tilePosition.x += xCellPos * tileSize.x;
				tilePosition.y -= yCellPos * (tileSize.y * (3f / 4f));
			}
			else
			{
				tilePosition.x += xCellPos * (tileSize.x * (3f / 4f));
				tilePosition.y -= yCellPos * tileSize.y;
			}

			tileRect.anchoredPosition = tilePosition;
		}

		/// <summary>
		/// Sets the color of all the ShapeObject tiles based on the shape index
		/// </summary>
		private void SetShapeTileColor(ShapeObject shapeObject)
		{
			// Set all the tiles colors
			Color shapeColor = shapeColors[shapeObject.shape.index % shapeColors.Count];

			for (int j = 0; j < shapeObject.tiles.Count; j++)
			{
				shapeObject.tiles[j].tileBkg.color = shapeColor;
			}
		}

		private void MoveToShapeMarker(ShapeObject shapeObject, bool animate = false)
		{
			// Set the shape tile containers parent to the marker
			shapeObject.tileContainer.SetParent(shapeObject.shapeContainerMarker, false);

			// Set the tile container position to zero so its centered on the marker
			shapeObject.tileContainer.anchoredPosition = Vector2.zero;

			// Set the tileContainers scale so it fits in the cell
			shapeObject.tileContainer.localScale = new Vector3(shapeScale, shapeScale, 1f);

			shapeObject.isOnGrid = false;
		}

		private void MoveShapeToGrid(ShapeObject shapeObject, CellPos gridCellPos)
		{
			RectTransform	gridTile	= gridTiles[gridCellPos.y][gridCellPos.x].tile;
			RectTransform	shapeTile	= shapeObject.tiles[0].RectT;

			// Get the container to place the shape in
			RectTransform container = shapeObject.isActiveShape ? gridPlacedShapesContainer : gridPlacedHintsContainer;

			// Move the shape to the GameArea
			shapeObject.tileContainer.SetParent(container);

			// Scale the shape so it fits on the grid
			shapeObject.tileContainer.localScale = gridTile.localScale;

			// Get the positions of the grid tile and the anchor shape tile in the GameArea
			Vector2 gridTilePosition	= Utilities.SwitchToRectTransform(gridTile, container);
			Vector2 shapeTilePosition	= Utilities.SwitchToRectTransform(shapeTile, container);

			// Move the shape the difference so it lines up on the grid properly
			Vector2 diff			= gridTilePosition - shapeTilePosition;
			Vector2 shapePosition	= shapeObject.tileContainer.anchoredPosition;

			shapeObject.tileContainer.anchoredPosition = shapePosition + diff;

			// Set the GridTile placedShape references
			if (shapeObject.isActiveShape)
			{
				ShapePlacedOnGrid(shapeObject, gridCellPos);
			}
		}

		private bool TryStartDraggingShapeInContainer(Vector2 screenPosition)
		{
			// Get the screenPosition relative to the gridContainer
			Vector2 shapeContainerPosition;

			RectTransformUtility.ScreenPointToLocalPointInRectangle(shapesContainer.transform as RectTransform, screenPosition, null, out shapeContainerPosition);

			// Set the position relative to the top/left corner of the shapesContainer
			shapeContainerPosition.x += (shapesContainer.transform as RectTransform).rect.width / 2f;
			shapeContainerPosition.y -= (shapesContainer.transform as RectTransform).rect.height / 2f;

			float		minDistance			= float.MaxValue;
			ShapeObject	closestShapeObject	= null;

			// Get the closest shape marker to shapeContainerPosition
			for (int i = 0; i < shapeObjects.Count; i++)
			{
				ShapeObject shapeObject = shapeObjects[i];

				float distance = Vector2.Distance(shapeObject.shapeContainerMarker.anchoredPosition, shapeContainerPosition);

				if (distance < minDistance)
				{
					minDistance			= distance;
					closestShapeObject	= shapeObject;
				}
			}

			// Check if the closest shape object is not already on the grid
			if (closestShapeObject != null && !closestShapeObject.isOnGrid)
			{
				SetShapeObjectAsActive(closestShapeObject, screenPosition);

				return true;
			}

			return false;
		}

		/// <summary>
		/// Sets the given shape object as the active object being dragged by the mouse
		/// </summary>
		private void SetShapeObjectAsActive(ShapeObject shapeObject, Vector2 initialScreenPosition)
		{
			activeShapeObject = shapeObject;

			// Move the active ShapeObject to the GameAreas transform so it appears ontop of everything in the GameArea
			activeShapeObject.tileContainer.SetParent(transform);

			// Set the scale of the shape object so it's tiles match the size of the grid tiles
			CellPos		anchorCellPosition	= activeShapeObject.shape.Anchor;
			GridTile	anchorGridTile		= gridTiles[anchorCellPosition.y][anchorCellPosition.x];

			activeShapeObject.tileContainer.localScale = anchorGridTile.tile.localScale;

			// Update the position of the shape so it appears at the mouse position right away
			UpdateActiveShapeObjectPosition(initialScreenPosition);
		}

		private bool TryStartDraggingShapeOnGrid(Vector2 screenPosition)
		{
			// Get the screenPosition relative to the gridContainer
			Vector2 gridPosition;

			RectTransformUtility.ScreenPointToLocalPointInRectangle(gridContainer, screenPosition, null, out gridPosition);

			// Get the closest GridTile to the position
			GridTile gridTile = GetClosestGridTile(gridPosition);

			// Check if there is a shape placed on the GridTile
			if (gridTile.placedShape != null)
			{
				ShapeObject	shapeObject	= gridTile.placedShape;

				// Remove the shape from the grid
				RemoveShapeFromGrid(shapeObject, gridTile);

				// Set the shape has the active dragging shape
				SetShapeObjectAsActive(shapeObject, screenPosition);

				return true;
			}

			return false;
		}

		/// <summary>
		/// Gets the GridTile in gridTiles that is closest to the given position relative to gridContainer
		/// </summary>
		private GridTile GetClosestGridTile(Vector2 position, bool doTriangleOrientationCheck = false, bool onlyFlipped = false)
		{
			float		minDistance		= float.MaxValue;
			GridTile	closestGridTile	= null;

			for (int i = 0; i < gridTiles.Count; i++)
			{
				List<GridTile> gridTilesRow = gridTiles[i];

				for (int j = 0; j < gridTilesRow.Count; j++)
				{
					GridTile gridTile = gridTilesRow[j];

					if (gridTile == null)
					{
						continue;
					}

					if (doTriangleOrientationCheck)
					{
						if ((onlyFlipped && (gridTile.cellPos.x + gridTile.cellPos.y) % 2 == 0) ||
							(!onlyFlipped && (gridTile.cellPos.x + gridTile.cellPos.y) % 2 == 1))
						{
							continue;
						}
					}

					if (gridTile != null)
					{
						float distance = Vector2.Distance(gridTile.tile.anchoredPosition, position);

						if (distance < minDistance)
						{
							closestGridTile	= gridTile;
							minDistance		= distance;
						}
					}
				}
			}

			return closestGridTile;
		}

		/// <summary>
		/// Sets the activeShapeObjects position to the given screen position inside GameAreas RectTransform
		/// </summary>
		private void UpdateActiveShapeObjectPosition(Vector2 screenPosition)
		{
			Vector2 gameAreaPosition;

			// Get the position inside the GameArea
			RectTransformUtility.ScreenPointToLocalPointInRectangle(RectT, screenPosition, null, out gameAreaPosition);

			// Position the shape so it's bottom edge is at the gameAreaPosition
			Vector2 shapePosition = gameAreaPosition;

			shapePosition.y += (activeShapeObject.tileContainer.rect.height * activeShapeObject.tileContainer.localScale.y) / 2f;

			activeShapeObject.tileContainer.anchoredPosition = shapePosition;
		}

		/// <summary>
		/// Tries to place the active shape on the grid
		/// </summary>
		private bool TryPlaceActiveShapeOnGrid(out CellPos cellPos)
		{
			cellPos = TryGetValidGridCellPos();

			if (cellPos != null)
			{
				MoveShapeToGrid(activeShapeObject, cellPos);

				return true;
			}

			return false;
		}

		/// <summary>
		/// Checks if the active shape object can be placed on the grid at it's current location and returns the valid cell pos if it can
		/// </summary>
		private CellPos TryGetValidGridCellPos()
		{
			// Get the position of the active shapes anchor tile in the gridContainer
			RectTransform	anchorTile		= activeShapeObject.tiles[0].RectT;
			Vector2			gridPosition	= Utilities.SwitchToRectTransform(anchorTile, gridContainer);

			// Check that the anchors grid position is contained within the gridContainer bounds
			if (gridContainer.rect.Contains(gridPosition))
			{
				// For triangle levels, we need to make sure we place the shape only on cells that match the orientation of the anchor triangle
				bool doTriangleOrientationCheck = activeLevelData.Type == LevelData.LevelType.Triangle;
				bool anchorTriangleFlipped		= activeShapeObject.shape.IsAnchorFlipped;

				// Get the closest tile to the anchors
				GridTile gridTile = GetClosestGridTile(gridPosition, doTriangleOrientationCheck, anchorTriangleFlipped);

				if (CanPlaceActiveShapeOnGridAt(gridTile.cellPos))
				{
					return gridTile.cellPos;
				}
			}

			return null;
		}

		/// <summary>
		/// Checks if we can place the active shape object at the given grid x/y location
		/// </summary>
		private bool CanPlaceActiveShapeOnGridAt(CellPos gridCellPos)
		{
			List<GridTile> shapeGridTiles = GetShapeGridTiles(activeShapeObject, activeShapeObject.shape.Anchor, gridCellPos);

			if (shapeGridTiles == null)
			{
				return false;
			}

			for (int i = 0; i < shapeGridTiles.Count; i++)
			{
				GridTile cellGridTile = shapeGridTiles[i];

				if (cellGridTile == null || !cellGridTile.CanPlaceShapeTile)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Sets the values on the GridTiles for the shape that was just placed on the grid
		/// </summary>
		private void ShapePlacedOnGrid(ShapeObject shapeObject, CellPos gridCellPos)
		{
			List<GridTile> shapeGridTiles = GetShapeGridTiles(shapeObject, shapeObject.shape.Anchor, gridCellPos);

			for (int i = 0; i < shapeGridTiles.Count; i++)
			{
				GridTile cellGridTile = shapeGridTiles[i];

				// Set the placed shape on this GridTile
				cellGridTile.placedShape		= shapeObject;
				cellGridTile.placedShapeTilePos	= shapeObject.shape.cellPositions[i];
			}

			shapeObject.isOnGrid = true;
		}

		/// <summary>
		/// Sets the values on the GridTiles for the shape that was just removed from the grid
		/// </summary>
		private bool RemoveShapeFromGrid(ShapeObject shapeObject, GridTile targetGridTile)
		{
			List<GridTile> shapeGridTiles = GetShapeGridTiles(shapeObject, targetGridTile.placedShapeTilePos, targetGridTile.cellPos);

			for (int i = 0; i < shapeGridTiles.Count; i++)
			{
				// Set the placed shape on this GridTile to null to indicate there is no shape placed here
				shapeGridTiles[i].placedShape = null;

				SetDebugText(shapeGridTiles[i], "");
			}

			return true;
		}

		private List<GridTile> GetShapeGridTiles(ShapeObject shapeObject, CellPos anchorPos, CellPos gridCellPos)
		{
			List<GridTile> shapeGridTiles = new List<GridTile>();

			bool adjustForHexagons = false;

			if (activeLevelData.Type == LevelData.LevelType.Hexagon)
			{
				if (activeLevelData.IsVertHexagons && (anchorPos.y % 2) != (gridCellPos.y % 2))
				{
					adjustForHexagons = true;
				}
				else if (!activeLevelData.IsVertHexagons && (anchorPos.x % 2) != (gridCellPos.x % 2))
				{
					adjustForHexagons = true;
				}
			}

			for (int i = 0; i < shapeObject.shape.cellPositions.Count; i++)
			{
				CellPos cellPos = shapeObject.shape.cellPositions[i];

				int xDiff = cellPos.x - anchorPos.x;
				int yDiff = cellPos.y - anchorPos.y;

				int x = gridCellPos.x + xDiff;
				int y = gridCellPos.y + yDiff;

				if (adjustForHexagons)
				{
					if (activeLevelData.IsVertHexagons && Mathf.Abs(yDiff) % 2 == 1)
					{
						x += (anchorPos.y % 2 == 0) ? 1 : -1;
					}
					else if (!activeLevelData.IsVertHexagons && Mathf.Abs(xDiff) % 2 == 1)
					{
						y += (anchorPos.x % 2 == 0) ? 1 : -1;
					}
				}

				if (x < 0 || y < 0 || x >= activeLevelData.XCells || y >= activeLevelData.YCells)
				{
					return null;
				}

				shapeGridTiles.Add(gridTiles[y][x]);
			}

			return shapeGridTiles;
		}

		#endregion

		#region DebugMethods

		private void SetDebugText(GridTile tile, string message)
		{
			Text debugText = tile.tile.GetComponentInChildren<Text>();

			if (debugText != null) 
				debugText.GetComponentInChildren<Text>().text = string.Format("{0}\n{1}", tile.cellPos, message);
		}

		#endregion
	}
}
