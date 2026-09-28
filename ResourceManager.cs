using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Text;
using Newtonsoft.Json;

public class ResourceManager : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI bananasText;

    [Header("Settings")]
    [SerializeField] private float syncInterval = 5f;

    private int userId;
    private int bananas;
    private string apiUrl = "http://localhost:5092/api/users";

    private float timer;

    
    private bool isDirty = false;    
    private bool isSyncing = false;  

    void Start()
    {
        if (!PlayerPrefs.HasKey("UserId"))
        {
            Debug.LogError(" No UserId found! Please login first.");
            enabled = false;
            return;
        }

        userId = PlayerPrefs.GetInt("UserId");
        Debug.Log($" Logged in as user {userId}");

        StartCoroutine(GetResources());
    }

    void Update()
    {
        timer += Time.deltaTime;

        
        if (timer >= syncInterval && isDirty && !isSyncing)
        {
            timer = 0f;
            StartCoroutine(UpdateResources());
        }
    }


    public int GetCurrentBananas()
    {
        return bananas;
    }

    public void AddBananas(int amount)
    {
        bananas += amount;
        isDirty = true;
        UpdateUI();
    }

    public IEnumerator RefreshFromServer()
    {
        yield return GetResources();
    }

    

    IEnumerator GetResources()
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{apiUrl}/{userId}"))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string json = www.downloadHandler.text;
                Debug.Log($" Loaded user data: {json}");

                UserData userData = JsonConvert.DeserializeObject<UserData>(json);

                if (userData != null && userData.Resources != null)
                {
                    if (userData.Resources.TryGetValue("bananas", out int value))
                    {
                        bananas = value;
                        UpdateUI();
                    }
                }

                
                isDirty = false;
            }
            else
            {
                Debug.LogError($" Failed to load resources: {www.error}");
            }
        }
    }

    IEnumerator UpdateResources()
    {
        isSyncing = true;

        var newResources = new Dictionary<string, int>
        {
            { "bananas", bananas }
        };

        string json = JsonConvert.SerializeObject(new UpdateResourcesRequest(newResources));

        using (UnityWebRequest www = new UnityWebRequest($"{apiUrl}/{userId}/resources", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($" Resources synced: {json}");
                isDirty = false;
            }
            else
            {
                Debug.LogError($" Failed to update resources: {www.error}");
                Debug.Log($" Server response: {www.downloadHandler.text}");
            }
        }

        isSyncing = false;
    }

    

    void UpdateUI()
    {
        if (bananasText != null)
            bananasText.text = bananas.ToString();
    }
}



[System.Serializable]
public class UpdateResourcesRequest
{
    public Dictionary<string, int> resources;

    public UpdateResourcesRequest(Dictionary<string, int> res)
    {
        resources = res;
    }
}

public class UserData
{
    public int Id;
    public string Username;
    public Dictionary<string, int> Resources;
}
