//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;
//using UnityEditor;

//[CreateAssetMenu(fileName = "HexGrid", menuName = "ScriptableObjects/HexGrid", order = 1)]
//public class HexGridSO : ScriptableObject
//{
//    [SerializeField] private int _width = 5;
//    [SerializeField] private int _height = 5;
//    [SerializeField] private Dictionary<AxialCoord, CellColor> _coloredCells = new Dictionary<AxialCoord, CellColor>();

//    public int Width { get => _width; set => _width = value; }
//    public int Height { get => _height; set => _height = value; }

//    public enum CellColor
//    {
//        None,
//        Green,
//        Orange,
//        Red,
//        Purple,
//        Yellow
//    }

//    [System.Serializable]
//    public struct AxialCoord
//    {
//        public int q;
//        public int r;

//        public AxialCoord(int q, int r)
//        {
//            this.q = q;
//            this.r = r;
//        }

//        public override bool Equals(object obj)
//        {
//            if (!(obj is AxialCoord)) return false;
//            AxialCoord other = (AxialCoord)obj;
//            return q == other.q && r == other.r;
//        }

//        public override int GetHashCode()
//        {
//            return q.GetHashCode() ^ r.GetHashCode();
//        }

//        public override string ToString()
//        {
//            return $"({q}, {r})";
//        }
//    }

//    private static readonly AxialCoord[] NeighborDirections = new AxialCoord[]
//    {
//        new AxialCoord(1, 0), new AxialCoord(1, -1), new AxialCoord(0, -1),
//        new AxialCoord(-1, 0), new AxialCoord(-1, 1), new AxialCoord(0, 1)
//    };

//    public void SetCellColor(AxialCoord coord, CellColor color)
//    {
//        if (IsValidCoord(coord))
//        {
//            if (color == CellColor.None)
//            {
//                _coloredCells.Remove(coord);
//            }
//            else
//            {
//                _coloredCells[coord] = color;
//            }
//        }
//    }

//    public CellColor GetCellColor(AxialCoord coord)
//    {
//        return _coloredCells.TryGetValue(coord, out var color) ? color : CellColor.None;
//    }

//    public bool IsValidCoord(AxialCoord coord)
//    {
//        int rowOffset = coord.r % 2 == 0 ? 0 : 1;
//        int effectiveWidth = (coord.r % 2 == 0) ? _width : _width - 1;
//        return coord.q >= 0 && coord.q < effectiveWidth && coord.r >= 0 && coord.r < _height;
//    }

//    public List<AxialCoord> GetColoredCoords()
//    {
//        return _coloredCells.Keys.ToList();
//    }

//    public Dictionary<CellColor, List<List<AxialCoord>>> GetColorGroups()
//    {
//        var groups = new Dictionary<CellColor, List<List<AxialCoord>>>();
//        var visited = new HashSet<AxialCoord>();

//        foreach (var color in System.Enum.GetValues(typeof(CellColor)).Cast<CellColor>())
//        {
//            if (color == CellColor.None) continue;
//            groups[color] = new List<List<AxialCoord>>();
//        }

//        foreach (var cell in _coloredCells)
//        {
//            if (visited.Contains(cell.Key)) continue;

//            var group = new List<AxialCoord>();
//            var stack = new Stack<AxialCoord>();
//            stack.Push(cell.Key);
//            visited.Add(cell.Key);
//            CellColor currentColor = cell.Value;

//            while (stack.Count > 0)
//            {
//                var current = stack.Pop();
//                group.Add(current);

//                foreach (var dir in NeighborDirections)
//                {
//                    var neighbor = new AxialCoord(current.q + dir.q, current.r + dir.r);
//                    if (IsValidCoord(neighbor) && !visited.Contains(neighbor) &&
//                        _coloredCells.TryGetValue(neighbor, out var neighColor) && neighColor == currentColor)
//                    {
//                        visited.Add(neighbor);
//                        stack.Push(neighbor);
//                    }
//                }
//            }

//            if (group.Count > 0)
//            {
//                groups[currentColor].Add(group);
//            }
//        }

//        return groups;
//    }

//    public void PrintData()
//    {
//        var coloredCoords = GetColoredCoords();
//        Debug.Log("Colored Coordinates:");
//        foreach (var coord in coloredCoords)
//        {
//            Debug.Log(coord.ToString());
//        }

//        var groups = GetColorGroups();
//        Debug.Log("Color Groups:");
//        foreach (var kvp in groups)
//        {
//            Debug.Log($"Color: {kvp.Key}");
//            for (int i = 0; i < kvp.Value.Count; i++)
//            {
//                Debug.Log($"Group {i + 1}: {string.Join(", ", kvp.Value[i])}");
//            }
//        }
//    }
//}

//[CustomEditor(typeof(HexGridSO))]
//public class HexGridSOEditor : Editor
//{
//    private HexGridSO _grid;
//    private HexGridSO.CellColor _selectedColor = HexGridSO.CellColor.Green;
//    private readonly float HexSize = 20f; // Giảm kích thước tam giác để gần nhau hơn
//    private float HexHeight; // Height of an equilateral triangle

//    private void OnEnable()
//    {
//        _grid = (HexGridSO)target;
//        HexHeight = HexSize * Mathf.Sqrt(3) / 2; // Calculate at runtime
//    }

//    public override void OnInspectorGUI()
//    {
//        base.OnInspectorGUI();

//        EditorGUILayout.Space();
//        _selectedColor = (HexGridSO.CellColor)EditorGUILayout.EnumPopup("Selected Color", _selectedColor);

//        EditorGUILayout.Space();
//        DrawHexGrid();

//        EditorGUILayout.Space();
//        if (GUILayout.Button("Print Data"))
//        {
//            _grid.PrintData();
//        }

//        if (GUI.changed)
//        {
//            EditorUtility.SetDirty(_grid);
//        }
//    }

//    private void DrawHexGrid()
//    {
//        GUILayout.Label("Hex Grid (Click to color cells - Text Representation):");

//        // Define a scroll view to ensure the grid is visible
//        Rect gridRect = EditorGUILayout.GetControlRect(false, _grid.Height * HexHeight + 20);
//        Handles.BeginGUI();
//        GUI.BeginGroup(gridRect);

//        for (int r = 0; r < _grid.Height; r++)
//        {
//            for (int q = 0; q < _grid.Width; q++)
//            {
//                var coord = new HexGridSO.AxialCoord(q, r);
//                if (!_grid.IsValidCoord(coord)) continue;

//                // Calculate position with offset for odd rows
//                float xOffset = (r % 2 == 1) ? HexSize / 2 : 0;
//                float x = q * HexSize * 0.9f; // Giảm khoảng cách giữa các ô bằng cách nhân với hệ số < 1
//                float y = r * HexHeight * 0.9f; // Giảm khoảng cách theo chiều dọc

//                // Determine if this is an upward or downward triangle
//                bool isUpTriangle = (q + r) % 2 == 0;

//                // Draw triangle
//                Vector3[] points = new Vector3[3];
//                if (isUpTriangle)
//                {
//                    points[0] = new Vector3(x, y, 0);
//                    points[1] = new Vector3(x + HexSize, y, 0);
//                    points[2] = new Vector3(x + HexSize / 2, y + HexHeight, 0);
//                }
//                else
//                {
//                    points[0] = new Vector3(x, y + HexHeight, 0);
//                    points[1] = new Vector3(x + HexSize, y + HexHeight, 0);
//                    points[2] = new Vector3(x + HexSize / 2, y, 0);
//                }

//                var color = _grid.GetCellColor(coord);
//                Handles.color = GetColorForCell(color);
//                Handles.DrawAAConvexPolygon(points);

//                // Check for click with adjusted position
//                Vector2 mousePos = Event.current.mousePosition - (Vector2)gridRect.position;
//                if (Event.current.type == EventType.MouseDown && IsPointInTriangle(mousePos, points))
//                {
//                    if (_selectedColor == HexGridSO.CellColor.None)
//                    {
//                        _grid.SetCellColor(coord, HexGridSO.CellColor.None);
//                    }
//                    else
//                    {
//                        _grid.SetCellColor(coord, _selectedColor);
//                    }
//                    Event.current.Use();
//                    Repaint();
//                }
//            }
//        }

//        GUI.EndGroup();
//        Handles.EndGUI();
//    }

//    private bool IsPointInTriangle(Vector2 point, Vector3[] triangle)
//    {
//        // Convert Vector3 to Vector2 for 2D check
//        Vector2 p1 = new Vector2(triangle[0].x, triangle[0].y);
//        Vector2 p2 = new Vector2(triangle[1].x, triangle[1].y);
//        Vector2 p3 = new Vector2(triangle[2].x, triangle[2].y);

//        float d1 = Sign(point, p1, p2);
//        float d2 = Sign(point, p2, p3);
//        float d3 = Sign(point, p3, p1);

//        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
//        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

//        return !(hasNeg && hasPos);
//    }

//    private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
//    {
//        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
//    }

//    private Color GetColorForCell(HexGridSO.CellColor color)
//    {
//        switch (color)
//        {
//            case HexGridSO.CellColor.Green: return Color.green;
//            case HexGridSO.CellColor.Orange: return new Color(1f, 0.5f, 0f);
//            case HexGridSO.CellColor.Red: return Color.red;
//            case HexGridSO.CellColor.Purple: return new Color(0.5f, 0f, 0.5f);
//            case HexGridSO.CellColor.Yellow: return Color.yellow;
//            default: return Color.white; // Thay xám nhạt bằng trắng cho ô trống
//        }
//    }
//}