using System.Collections.Generic;
using UnityEngine;
using static GridEditorWindow;

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
    public List<GroupData> groups = new();
    public List<GroupData> fakeDataGroups = new();
    public List<Vector2Int> coloredCells = new();
}
