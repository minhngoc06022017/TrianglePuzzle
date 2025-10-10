using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MapSaveSO", menuName = "Map Editor/Map Save")]
public class MapSaveSO : ScriptableObject
{
    public List<MapGameData> maps = new();
}

[System.Serializable]
public class MapGameData
{
    public string mapID;
    public int width;
    public int height;
    public List<GridEditorWindow.GroupData> groups = new();
    public List<Vector2Int> coloredCells = new();
}
