using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GridEditorWindow : EditorWindow
{
    [Header("Grid Settings")]
    public int width = 10;
    public int height = 10;
    public float spacingWidth = 1.05f;
    public float spacingHeight = 1.1f;

    public enum CellColor { White, Red, Orange, Yellow, Purple , Green, Blue, Gray }
    public CellColor selectedColor = CellColor.White;

    private CellColor[,] grid;
    private CellColor[,] fakeGrid;
    // Giá trị tạm cho input
    private int tempWidth;
    private int tempHeight;
    private float tempSpacingWidth;
    private float tempSpacingHeight;

    [Header("Map Save/Load")]
    public string currentMapID = "Map_1";
    public MapSaveSO mapSaveSO;

    private string[] availableMapIDs;
    private int selectedMapIndex = -1;

    [MenuItem("Tools/Hex Tri Grid Editor")]
    public static void ShowWindow()
    {
        GetWindow<GridEditorWindow>("Hex Tri Grid Editor");
    }

    private void OnEnable()
    {
        // copy giá trị hiện tại sang temp
        tempWidth = width;
        tempHeight = height;
        tempSpacingWidth = spacingWidth;
        tempSpacingHeight = spacingHeight;

        // 🔹 Auto assign MapSaveSO
        if (mapSaveSO == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:MapSaveSO");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                mapSaveSO = AssetDatabase.LoadAssetAtPath<MapSaveSO>(path);
                Debug.Log($"Auto-loaded MapSaveSO: {mapSaveSO.name}");
            }
        }

        InitGrid();
        RefreshMapList();
    }

    void InitGrid()
    {
        fakeGrid = new CellColor[width, height];
        grid = new CellColor[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = CellColor.White;
                fakeGrid[x, y] = CellColor.White;
            }  
    }

    private Vector2 _scrollPos;
    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // --- GRID SETTINGS ---
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
        selectedColor = (CellColor)EditorGUILayout.EnumPopup("Selected Color", selectedColor);
        tempWidth = EditorGUILayout.IntField("Grid Width", tempWidth);
        tempHeight = EditorGUILayout.IntField("Grid Height", tempHeight);
        tempSpacingWidth = EditorGUILayout.FloatField("Spacing Width", tempSpacingWidth);
        tempSpacingHeight = EditorGUILayout.FloatField("Spacing Height", tempSpacingHeight);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Resize Grid", GUILayout.Height(25)))
        {
            width = Mathf.Max(1, tempWidth);
            height = Mathf.Max(1, tempHeight);
            spacingWidth = tempSpacingWidth;
            spacingHeight = tempSpacingHeight;
            InitGrid();
        }

        // --- MAP SAVE / LOAD ---
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Map Save/Load", EditorStyles.boldLabel);
        mapSaveSO = (MapSaveSO)EditorGUILayout.ObjectField("Map Save SO", mapSaveSO, typeof(MapSaveSO), false);
        currentMapID = EditorGUILayout.TextField("Current Map ID", currentMapID);

        // Dropdown chọn map có sẵn
        if (mapSaveSO)
        {
            if (availableMapIDs == null)
            {
                RefreshMapList();
            }

            if (availableMapIDs.Length > 0)
            {
                selectedMapIndex = Mathf.Clamp(selectedMapIndex, 0, availableMapIDs.Length - 1);
                selectedMapIndex = EditorGUILayout.Popup("Load From", selectedMapIndex, availableMapIDs);

                if (GUILayout.Button("Load Selected Map", GUILayout.Height(25)))
                {
                    currentMapID = availableMapIDs[selectedMapIndex];
                    LoadMap();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No maps saved yet.", MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No maps saved yet.", MessageType.Info);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Map", GUILayout.Height(25))) SaveMap();
        if (GUILayout.Button("Clear Current Map", GUILayout.Height(25))) ClearCurrentMap();
        if (GUILayout.Button("Clear All Maps", GUILayout.Height(25))) ClearAllMaps();
        EditorGUILayout.EndHorizontal();

        // --- REAL BOARD ---
        EditorGUILayout.Space(10);
        GUILayout.Label("Real Board", EditorStyles.boldLabel);
        Rect realRect = GUILayoutUtility.GetRect(position.width - 40, height * 30f);
        GUI.Box(realRect, GUIContent.none);
        if (grid != null)
            DrawGrid(realRect, grid);

        // --- FAKE BOARD ---
        EditorGUILayout.Space(20);
        GUILayout.Label("Fake Board", EditorStyles.boldLabel);
        Rect fakeRect = GUILayoutUtility.GetRect(position.width - 40, height * 30f);
        GUI.Box(fakeRect, GUIContent.none);
        if (fakeGrid != null)
            DrawGrid(fakeRect, fakeGrid);

        EditorGUILayout.Space(20);
        if (GUILayout.Button("Print Data", GUILayout.Height(25)))
            PrintData();

        EditorGUILayout.EndScrollView();
    }

    void DrawGrid(Rect drawArea, CellColor[,] targetGrid)
    {
        Handles.BeginGUI();
        float triSize = 25f; // có thể nhỏ hơn cho dễ nhìn

        float totalWidth = (width - 1) * triSize * spacingWidth + triSize * 2f;
        float totalHeight = (height - 1) * triSize * 0.85f * spacingHeight + triSize * 2f;

        Vector2 startPos = new Vector2(
            drawArea.x + (drawArea.width - totalWidth) / 2f,
            drawArea.y + (drawArea.height - totalHeight) / 2f
        );

        Event e = Event.current;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isUp = (x + y) % 2 == 0;
                float offsetX = x * triSize * spacingWidth;
                float offsetY = y * triSize * 0.85f * spacingHeight;
                Vector2 p1 = startPos + new Vector2(offsetX, offsetY);

                Vector2[] points = isUp
                    ? new Vector2[] { p1, p1 + new Vector2(triSize, triSize), p1 + new Vector2(-triSize, triSize) }
                    : new Vector2[] { p1 + new Vector2(0, triSize), p1 + new Vector2(triSize, 0), p1 + new Vector2(-triSize, 0) };

                Vector3[] p3 = System.Array.ConvertAll(points, p => (Vector3)p);
                Handles.color = GetColor(targetGrid[x, y]);
                Handles.DrawAAConvexPolygon(p3);
                Handles.color = Color.black;
                Handles.DrawPolyLine(p3[0], p3[1], p3[2], p3[0]);

                Rect clickRect = new Rect(p1.x - triSize * 0.5f, p1.y - triSize * 0.5f, triSize, triSize);
                if (e.type == EventType.MouseDown && clickRect.Contains(e.mousePosition))
                {
                    if (targetGrid[x, y] == selectedColor)
                        targetGrid[x, y] = CellColor.White;
                    else
                        targetGrid[x, y] = selectedColor;

                    Repaint();
                    e.Use();
                    return;
                }
            }
        }

        Handles.EndGUI();
    }

    Color GetColor(CellColor color)
    {
        return color switch
        {
            CellColor.Red => Color.red,
            CellColor.Orange => new Color(1f, 0.5f, 0f),
            CellColor.Yellow => Color.yellow,
            CellColor.Purple => new Color(0.6f, 0f, 0.8f),
            CellColor.Green => Color.green,
            CellColor.Blue => Color.blue,
            CellColor.Gray => Color.gray,
            _ => Color.white,
        };
    }

    void PrintData()
    {
        List<GroupData> groups = GetGroups(grid);
        List<GroupData> fakeGroups = GetGroups(fakeGrid);
        Debug.Log($"Total Groups: {groups.Count}");
        foreach (var g in groups)
            Debug.Log($"Real [{g.color}] => " + string.Join(", ", g.cells));
        foreach (var g in fakeGroups)
            Debug.Log($"Fake [{g.color}] => " + string.Join(", ", g.cells));
    }

    // 8 hướng + logic Hex offset
    List<GroupData> GetGroups(CellColor[,] targetGrid)
    {
        List<GroupData> result = new();
        bool[,] visited = new bool[width, height];

        Vector2Int[] dirs =
        {
        new(1,0), new(-1,0), new(0,1), new(0,-1),
        new(1,1), new(1,-1), new(-1,1), new(-1,-1)
    };

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y]) continue;
                var color = targetGrid[x, y];
                if (color == CellColor.White) continue;

                List<Vector2Int> cells = new();
                Queue<Vector2Int> q = new();
                q.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;

                while (q.Count > 0)
                {
                    var cur = q.Dequeue();
                    cells.Add(cur);

                    foreach (var d in dirs)
                    {
                        int nx = cur.x + d.x, ny = cur.y + d.y;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        if (visited[nx, ny]) continue;
                        if (!CanConnect(targetGrid[cur.x, cur.y], targetGrid[nx, ny])) continue;

                        visited[nx, ny] = true;
                        q.Enqueue(new Vector2Int(nx, ny));
                    }
                }

                result.Add(new GroupData { color = color, cells = cells });
            }
        }

        return result;
    }

    bool CanConnect(CellColor from, CellColor to)
    {
        // không nối qua ô trắng hoặc block khác màu
        if (to == CellColor.White) return false;
        if (from == CellColor.Gray && to != CellColor.Gray) return false;
        if (to == CellColor.Gray && from != CellColor.Gray) return false;
        return from == to;
    }

    void SaveMap()
    {
        if (!mapSaveSO)
        {
            Debug.LogError("❌ Chưa gán MapSaveSO!");
            return;
        }

        var groups = GetGroups(grid);
        var fakeGroups = GetGroups(fakeGrid);
        var coloredCells = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y] != CellColor.White)
                    coloredCells.Add(new Vector2Int(x, y));

        MapGameData existing = mapSaveSO.maps.FirstOrDefault(m => m.mapID == currentMapID);
        if (existing != null)
        {
            existing.width = width;
            existing.height = height;
            existing.groups = groups;
            existing.coloredCells = coloredCells;
            existing.fakeDataGroups = fakeGroups;
        }
        else
        {
            mapSaveSO.maps.Add(new MapGameData
            {
                mapID = currentMapID,
                width = width,
                height = height,
                groups = groups,
                coloredCells = coloredCells,
                fakeDataGroups = fakeGroups
            });
        }

        EditorUtility.SetDirty(mapSaveSO);
        AssetDatabase.SaveAssets();
        RefreshMapList();
        Debug.Log($"✅ Saved Map: {currentMapID}");
    }

    void LoadMap()
    {
        if (!mapSaveSO)
        {
            Debug.LogError("❌ Chưa gán MapSaveSO!");
            return;
        }

        MapGameData existing = mapSaveSO.maps.FirstOrDefault(m => m.mapID == currentMapID);
        if (existing == null)
        {
            Debug.LogWarning($"⚠ Không tìm thấy mapID {currentMapID}");
            return;
        }

        width = existing.width;
        height = existing.height;
        InitGrid();

        foreach (var g in existing.groups)
            foreach (var c in g.cells)
                if (c.x < width && c.y < height)
                    grid[c.x, c.y] = g.color;

        Debug.Log($"✅ Loaded Map: {currentMapID}");
        Repaint();
    }

    void ClearCurrentMap()
    {
        if (!mapSaveSO)
        {
            Debug.LogError("❌ Chưa gán MapSaveSO!");
            return;
        }

        int index = mapSaveSO.maps.FindIndex(m => m.mapID == currentMapID);
        if (index >= 0)
        {
            mapSaveSO.maps.RemoveAt(index);
            EditorUtility.SetDirty(mapSaveSO);
            AssetDatabase.SaveAssets();
            RefreshMapList();
            Debug.Log($"🗑 Cleared current map: {currentMapID}");
        }
        else
        {
            Debug.LogWarning($"⚠ Không tìm thấy mapID {currentMapID}");
        }
    }

    void ClearAllMaps()
    {
        if (!mapSaveSO)
        {
            Debug.LogError("❌ Chưa gán MapSaveSO!");
            return;
        }

        if (EditorUtility.DisplayDialog("Xóa tất cả map?",
            "Bạn có chắc muốn xóa toàn bộ dữ liệu map trong SO này?", "Xóa hết", "Hủy"))
        {
            mapSaveSO.maps.Clear();
            EditorUtility.SetDirty(mapSaveSO);
            AssetDatabase.SaveAssets();
            RefreshMapList();
            Debug.Log("🧹 Đã xóa toàn bộ maps trong MapSaveSO!");
        }
    }

    void RefreshMapList()
    {
        if (!mapSaveSO)
        {
            availableMapIDs = null;
            return;
        }
        availableMapIDs = mapSaveSO.maps.Select(m => m.mapID).ToArray();
        selectedMapIndex = availableMapIDs.Length > 0 ? 0 : -1;
    }

    [System.Serializable]
    public class GroupData
    {
        public CellColor color;
        public List<Vector2Int> cells;
    }
}
