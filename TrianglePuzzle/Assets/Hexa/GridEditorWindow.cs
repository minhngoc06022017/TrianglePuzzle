using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GridEditorWindow : EditorWindow
{
    [Header("Grid Settings")]
    public int width = 5;
    public int height = 5;
    public float spacingWidth = 1.05f;
    public float spacingHeight = 1.1f;

    public enum CellColor { White, Red, Orange, Yellow, Purple , Green, Blue, Gray }
    public CellColor selectedColor = CellColor.White;

    private CellColor[,] grid;
    private Vector2 scrollPos;

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

        InitGrid();
        RefreshMapList();
    }

    void InitGrid()
    {
        grid = new CellColor[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = CellColor.White;
    }

    private void OnGUI()
    {
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

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Map Save/Load", EditorStyles.boldLabel);
        mapSaveSO = (MapSaveSO)EditorGUILayout.ObjectField("Map Save SO", mapSaveSO, typeof(MapSaveSO), false);
        currentMapID = EditorGUILayout.TextField("Current Map ID", currentMapID);

        // Dropdown chọn map có sẵn
        if (mapSaveSO && availableMapIDs != null && availableMapIDs.Length > 0)
        {
            selectedMapIndex = Mathf.Clamp(selectedMapIndex, 0, availableMapIDs.Length - 1);
            selectedMapIndex = EditorGUILayout.Popup("Load From", selectedMapIndex, availableMapIDs);

            if (GUILayout.Button("Load Selected Map", GUILayout.Height(25)))
            {
                currentMapID = availableMapIDs[selectedMapIndex];
                LoadMap();
            }
        }
        else if (mapSaveSO)
        {
            EditorGUILayout.HelpBox("No maps saved yet.", MessageType.Info);
        }

        //EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Map", GUILayout.Height(25))) SaveMap();
        //EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        GUILayout.Label("Grid Preview", EditorStyles.boldLabel);
        Rect gridRect = GUILayoutUtility.GetRect(position.width, position.height - 300);
        GUI.Box(gridRect, GUIContent.none);
        GUILayout.Space(10);

        if (grid != null)
            DrawGrid(gridRect);

        GUILayout.Space(20);
        if (GUILayout.Button("Print Data", GUILayout.Height(25)))
            PrintData();
    }

    void DrawGrid(Rect drawArea)
    {
        Handles.BeginGUI();
        float triSize = 30f;
        Vector2 startPos = new Vector2(drawArea.x + 60, drawArea.y + 40);
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
                Handles.color = GetColor(grid[x, y]);
                Handles.DrawAAConvexPolygon(p3);
                Handles.color = Color.black;
                Handles.DrawPolyLine(p3[0], p3[1], p3[2], p3[0]);

                Rect clickRect = new Rect(p1.x - triSize * 0.5f, p1.y - triSize * 0.5f, triSize, triSize);
                if (e.type == EventType.MouseDown && clickRect.Contains(e.mousePosition))
                {
                    // Nếu click lại cùng màu thì reset về White
                    if (grid[x, y] == selectedColor)
                        grid[x, y] = CellColor.White;
                    else
                        grid[x, y] = selectedColor;

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
        List<GroupData> groups = GetGroups();
        Debug.Log($"Total Groups: {groups.Count}");
        foreach (var g in groups)
            Debug.Log($"[{g.color}] => " + string.Join(", ", g.cells));
    }

    // 8 hướng + logic Hex offset
    List<GroupData> GetGroups()
    {
        List<GroupData> result = new();
        bool[,] visited = new bool[width, height];

        Vector2Int[] dirsEven =
        {
            new(1,0), new(-1,0), new(0,1), new(0,-1),
            new(1,1), new(1,-1), new(-1,1), new(-1,-1)
        };

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y]) continue;

                CellColor color = grid[x, y];
                if (color == CellColor.White) continue;

                List<Vector2Int> cells = new();
                Queue<Vector2Int> q = new();
                q.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;

                while (q.Count > 0)
                {
                    var cur = q.Dequeue();
                    cells.Add(cur);

                    foreach (var d in dirsEven)
                    {
                        int nx = cur.x + d.x;
                        int ny = cur.y + d.y;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;

                        if (visited[nx, ny]) continue;
                        if (!CanConnect(grid[cur.x, cur.y], grid[nx, ny])) continue;

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

        var groups = GetGroups();
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
        }
        else
        {
            mapSaveSO.maps.Add(new MapGameData
            {
                mapID = currentMapID,
                width = width,
                height = height,
                groups = groups,
                coloredCells = coloredCells
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
