using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Text;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Newtonsoft.Json;

public class BuildManager : MonoBehaviour
{
    [Header("References")]
    public GameObject plantationPrefab;
    public ResourceManager resourceManager;

    [Header("Building Settings")]
    public int buildCost = 50;

    [Header("API")]
    public string apiBaseUrl = "http://localhost:5092/api";

    [Header("Spawn / Sorting")]
    public string buildingsSortingLayer = "Buildings";
    public int baseSortingOrder = 10;                 
    public float zDepth = -0.1f;                       
    public int ySortMultiplier = 100;                  

    [Header("Runtime (optional)")]
    public Transform buildingsRoot;                   
    public string plantationTag = "Plantation";       

    private bool isBuildingInProgress = false; 

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "WorldScene")
        {
            enabled = false;
            return;
        }

        
        StartCoroutine(LoadBuildings());
    }

    public void TryBuildPlantation(Vector3 position)
    {
        if (resourceManager == null)
        {
            Debug.LogError("ResourceManager not assigned!");
            return;
        }

        
        if (isBuildingInProgress)
        {
            Debug.Log("Waiting for server response...");
            return;
        }

        if (!CanAfford(buildCost))
        {
            Debug.Log("Not enough bananas to build!");
            return;
        }

       
        isBuildingInProgress = true;

        StartCoroutine(SaveBuilding(position));
    }

    bool CanAfford(int cost)
    {
        return resourceManager != null && resourceManager.GetCurrentBananas() >= cost;
    }

    
    IEnumerator SaveBuilding(Vector3 position)
    {
        if (!PlayerPrefs.HasKey("UserId"))
        {
            Debug.LogError("No UserId found! Please login first.");
            isBuildingInProgress = false; 
            yield break;
        }

        int userId = PlayerPrefs.GetInt("UserId");

        BuildingRequest buildingData = new BuildingRequest
        {
            UserId = userId,
            Type = "plantation",
            PosX = position.x,
            PosY = position.y,
            Level = 1
        };

        string json = JsonUtility.ToJson(buildingData);
        Debug.Log("JSON to send: " + json);

        using (UnityWebRequest www = new UnityWebRequest($"{apiBaseUrl}/PlayerBuildings", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log(" Building saved to DB! Spawning locally...");

                
                resourceManager.AddBananas(-buildCost);

                
                Vector3 spawnPos = new Vector3(position.x, position.y + 0.25f, zDepth);
                var plantation = Instantiate(plantationPrefab, spawnPos, Quaternion.identity);

                if (buildingsRoot != null)
                    plantation.transform.SetParent(buildingsRoot, true);

                
                ApplyIsoSorting(plantation);
            }
            else
            {
                
                Debug.LogError($" Failed to save building: {www.error} | Response: {www.downloadHandler.text}");
            }
        }

        
        isBuildingInProgress = false;
    }

    
    IEnumerator LoadBuildings()
    {
        if (!PlayerPrefs.HasKey("UserId"))
        {
            Debug.LogWarning(" No UserId found, skipping load.");
            yield break;
        }

        
        yield return null;
        yield return new WaitForSeconds(0.05f);

        int userId = PlayerPrefs.GetInt("UserId");

        using (UnityWebRequest www = UnityWebRequest.Get($"{apiBaseUrl}/PlayerBuildings/{userId}"))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($" Failed to load buildings: {www.error} | {www.downloadHandler.text}");
                yield break;
            }

            string json = www.downloadHandler.text;
            Debug.Log(" Buildings JSON: " + json);

            List<PlayerBuildingData> list = null;
            try
            {
                
                list = JsonConvert.DeserializeObject<List<PlayerBuildingData>>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError(" JSON parse error: " + e.Message);
                yield break;
            }

            if (list == null || list.Count == 0)
            {
                Debug.Log("No buildings found.");
                yield break;
            }

            
            ClearExistingPlantations();

            foreach (var b in list)
            {
                if (b == null) continue;

                Vector3 spawnPos = new Vector3(b.PosX, b.PosY + 0.25f, zDepth);
                var plantation = Instantiate(plantationPrefab, spawnPos, Quaternion.identity);

                if (buildingsRoot != null)
                    plantation.transform.SetParent(buildingsRoot, true);

                ApplyIsoSorting(plantation);
            }

            Debug.Log($" Loaded {list.Count} buildings from DB");
        }
    }

    void ApplyIsoSorting(GameObject go)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        sr.sortingLayerName = buildingsSortingLayer;

        
        int isoOrder = Mathf.RoundToInt(-go.transform.position.y * ySortMultiplier);

        
        sr.sortingOrder = baseSortingOrder + isoOrder;
    }

    void ClearExistingPlantations()
    {
        if (!string.IsNullOrEmpty(plantationTag))
        {
            var found = GameObject.FindGameObjectsWithTag(plantationTag);
            if (found != null && found.Length > 0)
            {
                foreach (var go in found) Destroy(go);
                return;
            }
        }

        var all = FindObjectsOfType<SpriteRenderer>();
        foreach (var sr in all)
        {
            if (sr != null && sr.gameObject != null && sr.gameObject.name.StartsWith(plantationPrefab.name))
                Destroy(sr.gameObject);
        }
    }
}


[System.Serializable]
public class BuildingRequest
{
    public int UserId;
    public string Type;
    public float PosX;
    public float PosY;
    public int Level;
}

[System.Serializable]
public class PlayerBuildingData
{
    public string Type;
    public float PosX;
    public float PosY;
    public int Level;
}