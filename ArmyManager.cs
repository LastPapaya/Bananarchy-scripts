using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using Newtonsoft.Json;

public class ArmyManager : MonoBehaviour
{
    [Header("Refs")]
    public ResourceManager resourceManager;
    public TextMeshProUGUI armyText;

    [Header("Config")]
    public string apiBaseUrl = "http://localhost:5092/api";
    public int trainCost = 30;

    private int userId;
    private int armyCount;

    void Start()
    {
        if (!PlayerPrefs.HasKey("UserId"))
        {
            Debug.LogError("No UserId found. Please login first.");
            return;
        }

        userId = PlayerPrefs.GetInt("UserId");
        StartCoroutine(RefreshFromServer());
    }

    public int GetArmyCount() => armyCount;

    
    public void TrainMonkey()
    {
        if (resourceManager == null)
        {
            Debug.LogError("ResourceManager is not assigned.");
            return;
        }

        if (resourceManager.GetCurrentBananas() < trainCost)
        {
            Debug.Log("Not enough bananas to train.");
            return;
        }

        StartCoroutine(TrainMonkeyRequest());
    }

    
    private IEnumerator TrainMonkeyRequest()
    {
        string url = $"{apiBaseUrl}/users/{userId}/train-monkey";

        
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("{}"));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Train request failed: {www.error}");
                Debug.LogError($"Server response: {www.downloadHandler.text}");
                yield break;
            }

            Debug.Log("Monkey trained successfully!");

            
            yield return RefreshFromServer();
            if (resourceManager != null)
                yield return resourceManager.RefreshFromServer();
        }
    }

   
    public IEnumerator RefreshFromServer()
    {
        string url = $"{apiBaseUrl}/users/{userId}";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to load user state: " + www.error);
                Debug.LogError("Server response: " + www.downloadHandler.text);
                yield break;
            }

            string json = www.downloadHandler.text;
            Debug.Log("Loaded user state: " + json);

            var dto = JsonConvert.DeserializeObject<UserStateDto>(json);
            if (dto != null)
                armyCount = dto.ArmyCount;

            UpdateArmyUI();
        }
    }

    private void UpdateArmyUI()
    {
        if (armyText != null)
            armyText.text = "Army: " + armyCount;
    }
}

[System.Serializable]
public class UserStateDto
{
    public int Id;
    public string Username;
    public int ArmyCount;
}
