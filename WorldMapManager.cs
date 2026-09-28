using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class WorldMapManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject tilePrefab;
    public GameObject playerMarkerPrefab;
    public GameObject foamPrefab;

    [Header("Grid & Island Settings")]
    public int islandRadius = 3;
    public float tileWidth = 0.3f;
    public float tileHeight = 0.15f;

    [Header("Visual Variety")]
    
    public Sprite[] groundSprites;

    [Header("Refs")]
    public WorldMapUI ui;

    [Header("Config")]
    public string apiBaseUrl = "http://localhost:5092/api";

    [Header("Runtime Assets")]
    public Transform gridRoot;
    private readonly List<GameObject> spawnedMarkers = new List<GameObject>();
    private readonly List<GameObject> spawnedTiles = new List<GameObject>();
    private readonly HashSet<Vector2> occupiedTileCoords = new HashSet<Vector2>();

    [Header("Marker Settings")]
    public Vector3 markerLocalScale = new Vector3(0.25f, 0.25f, 1f);
    public float markerZStep = 0.001f;

    [Header("Auto Refresh")]
    public bool refreshPlayers = false;
    public float refreshInterval = 3f;

    void Start()
    {
        EnsureGridRoot();
        StartCoroutine(LoadPlayers());

        if (refreshPlayers)
            StartCoroutine(RefreshLoop());
    }

    void EnsureGridRoot()
    {
        if (gridRoot == null)
        {
            var rootGo = new GameObject("GridRoot");
            gridRoot = rootGo.transform;
            gridRoot.SetParent(transform, false);
        }
        gridRoot.localPosition = Vector3.zero;
        gridRoot.localRotation = Quaternion.identity;
        gridRoot.localScale = Vector3.one;
    }

    IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshInterval);
            yield return LoadPlayers();
        }
    }

    IEnumerator LoadPlayers()
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{apiBaseUrl}/world/players"))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to load players: " + www.error);
                yield break;
            }

            string json = www.downloadHandler.text;
            PlayerList list = JsonUtility.FromJson<PlayerList>("{\"players\":" + json + "}");

            ClearMapWhole();

            if (list == null || list.players == null || list.players.Length == 0) yield break;

            
            foreach (var p in list.players)
            {
                GenerateIslandForPlayer(Mathf.RoundToInt(p.positionX), Mathf.RoundToInt(p.positionY));
            }

            
            foreach (var p in list.players)
            {
                SpawnMarkerSimple(p);
            }

            
            if (spawnedMarkers.Count > 0)
            {
                Vector3 firstPlayerPos = spawnedMarkers[0].transform.position;
                Camera.main.transform.position = new Vector3(firstPlayerPos.x, firstPlayerPos.y, Camera.main.transform.position.z);
            }
        }
    }

    void GenerateIslandForPlayer(int centerX, int centerY)
    {
        if (tilePrefab == null) return;

        
        int islandSeed = Mathf.Abs((centerX * 7) + (centerY * 13));
        Random.InitState(islandSeed);

        
        Color islandColor;
        if (islandSeed % 3 == 0)
        {
            islandColor = new Color(2.5f, 2.0f, 1.0f, 1f);
        }
        else
        {
            islandColor = new Color(0.2f, 2.5f, 0.2f, 1f);
        }

        
        List<Vector2> currentIslandTiles = new List<Vector2>();

        for (int x = centerX - islandRadius; x <= centerX + islandRadius; x++)
        {
            for (int y = centerY - islandRadius; y <= centerY + islandRadius; y++)
            {
                Vector2 coordKey = new Vector2(x, y);

                int distX = Mathf.Abs(x - centerX);
                int distY = Mathf.Abs(y - centerY);

                
                if (distX == islandRadius || distY == islandRadius)
                {
                    if (Random.value > 0.4f) continue;
                }

                
                if (occupiedTileCoords.Contains(coordKey))
                {
                    foreach (var spawnedTile in spawnedTiles)
                    {
                        if (spawnedTile != null && (Vector2)IsoToWorldLocal(x, y) == (Vector2)spawnedTile.transform.localPosition)
                        {
                            ApplyColorToTile(spawnedTile, islandColor);
                            break;
                        }
                    }
                    
                    currentIslandTiles.Add(coordKey);
                    continue;
                }

                Vector3 localPos = IsoToWorldLocal(x, y);
                localPos.z = -(x + y) * markerZStep;

                var tile = Instantiate(tilePrefab, gridRoot);
                tile.transform.localPosition = localPos;

                ApplyColorToTile(tile, islandColor);

                spawnedTiles.Add(tile);
                occupiedTileCoords.Add(coordKey);
                currentIslandTiles.Add(coordKey);
            }
        }

        
        
        }
    

    void ApplyColorToTile(GameObject tile, Color col)
    {
        SpriteRenderer rootSr = tile.GetComponent<SpriteRenderer>();
        if (rootSr != null) rootSr.color = col;

        Transform fillTransform = tile.transform.Find("Fill");
        if (fillTransform != null)
        {
            SpriteRenderer fillSr = fillTransform.GetComponent<SpriteRenderer>();
            if (fillSr != null) fillSr.color = col;
        }
    }

    void SpawnMarkerSimple(PlayerInfo p)
    {
        if (playerMarkerPrefab == null) return;

        Vector3 localPos = IsoToWorldLocal(p.positionX, p.positionY);
        localPos.z = (-(p.positionX + p.positionY) * markerZStep) - 0.01f;

        GameObject go = Instantiate(playerMarkerPrefab, gridRoot);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = markerLocalScale;

        spawnedMarkers.Add(go);

        var marker = go.GetComponent<PlayerMarker>();
        if (marker != null) marker.Init(p.id, p.username, ui);
    }

    Vector3 IsoToWorldLocal(float x, float y)
    {
        float worldX = (x - y) * tileWidth * 0.5f;
        float worldY = (x + y) * tileHeight * 0.5f;
        return new Vector3(worldX, worldY, 0f);
    }

    void ClearMapWhole()
    {
        for (int i = 0; i < spawnedMarkers.Count; i++) if (spawnedMarkers[i] != null) Destroy(spawnedMarkers[i]);
        boxMarkersClear();

        for (int i = 0; i < spawnedTiles.Count; i++) if (spawnedTiles[i] != null) Destroy(spawnedTiles[i]);
        spawnedTiles.Clear();

        occupiedTileCoords.Clear();
    }

    private void boxMarkersClear() => spawnedMarkers.Clear();

    [System.Serializable]
    public class PlayerInfo
    {
        public int id;
        public string username;
        public float positionX;
        public float positionY;
    }

    [System.Serializable]
    public class PlayerList { public PlayerInfo[] players; }
}