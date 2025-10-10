using System.Collections.Generic;
using UnityEngine;

public enum CellColor
{
    None,
    Red,
    Orange,
    Yellow,
    Purple
}

[System.Serializable]
public class HexCell
{
    public CellColor color = CellColor.None;
}

[CreateAssetMenu(fileName = "HexGridData", menuName = "Tools/HexGridData")]
public class HexGridData : ScriptableObject
{
    public int width = 5;
    public int height = 5;
    public List<HexCell> cells = new List<HexCell>();

    public HexCell GetCell(int x, int y)
    {
        int index = y * width + x;
        if (index >= 0 && index < cells.Count)
            return cells[index];
        return null;
    }

    public void Resize()
    {
        int newSize = width * height;
        while (cells.Count < newSize)
            cells.Add(new HexCell());
        while (cells.Count > newSize)
            cells.RemoveAt(cells.Count - 1);
    }
}

