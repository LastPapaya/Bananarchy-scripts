using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Text;
using UnityEngine.SceneManagement;

public class WorldMapUI : MonoBehaviour
{
    [Header("UI State Panels")]
    public GameObject defaultStatePanel;
    public GameObject selectedPlayerPanel;
    public GameObject battleResultPanel;

    [Header("UI Text Components")]
    public TextMeshProUGUI selectedText;
    public TextMeshProUGUI resultText;

    [Header("Config")]
    public string apiBaseUrl = "http://localhost:5092/api";

    [Header("Behaviour")]
    public float reloadDelay = 0.15f;

    [Header("BattleLogs")]
    public BattleLogsUI battleLogsUI;

    public enum UIState { Default, PlayerSelected, BattleResolved }

    private int selectedPlayerId = -1;
    private string selectedUsername = "";
    private bool isAttacking = false;

    void Start()
    {
        
        int id = PlayerPrefs.GetInt("SelectedPlayerId", -1);
        string name = PlayerPrefs.GetString("SelectedUsername", "");

        string lastResult = PlayerPrefs.GetString("LastBattleResult", "");

        if (id > 0 && !string.IsNullOrEmpty(name))
        {
            ShowPlayerInfo(id, name);

            
            if (!string.IsNullOrEmpty(lastResult))
            {
                SetResult(lastResult);
                SetUIState(UIState.BattleResolved);
            }
        }
        else
        {
           
            SetUIState(UIState.Default);
        }
    }

    
    public void SetUIState(UIState newState)
    {
        if (defaultStatePanel != null) defaultStatePanel.SetActive(false);
        if (selectedPlayerPanel != null) selectedPlayerPanel.SetActive(false);
        if (battleResultPanel != null) battleResultPanel.SetActive(false);

        switch (newState)
        {
            case UIState.Default:
                if (defaultStatePanel != null) defaultStatePanel.SetActive(true);
                break;

            case UIState.PlayerSelected:
                if (defaultStatePanel != null) defaultStatePanel.SetActive(true); 
                if (selectedPlayerPanel != null) selectedPlayerPanel.SetActive(true);
                break;

            case UIState.BattleResolved:
                if (defaultStatePanel != null) defaultStatePanel.SetActive(true);
                if (battleResultPanel != null) battleResultPanel.SetActive(true);
                break;
        }
    }

    
    public void ShowPlayerInfo(int id, string username)
    {
        selectedPlayerId = id;
        selectedUsername = username;

        if (selectedText != null)
            selectedText.text = $"Selected: {username} (id {id})";

        
        SetUIState(UIState.PlayerSelected);
    }

    public void AttackSelected()
    {
        if (isAttacking)
        {
            SetResult("Attack in progress...");
            return;
        }

        int attackerId = PlayerPrefs.GetInt("UserId", -1);
        if (attackerId <= 0)
        {
            SetResult("No UserId found. Login first.");
            SetUIState(UIState.BattleResolved); 
            return;
        }

        if (selectedPlayerId <= 0)
        {
            SetResult("No player selected.");
            SetUIState(UIState.BattleResolved);
            return;
        }

        if (attackerId == selectedPlayerId)
        {
            SetResult("You can't attack yourself.");
            SetUIState(UIState.BattleResolved);
            return;
        }

        StartCoroutine(AttackRequest(attackerId, selectedPlayerId));
    }

    IEnumerator AttackRequest(int attackerId, int defenderId)
    {
        isAttacking = true;

        
        SetResult("Attacking...");
        SetUIState(UIState.BattleResolved);

        string body = $"{{\"AttackerId\":{attackerId},\"DefenderId\":{defenderId}}}";

        using (UnityWebRequest www = new UnityWebRequest($"{apiBaseUrl}/battle/attack", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Attack failed: " + www.error + " | " + www.downloadHandler.text);
                SetResult("Attack failed: " + www.downloadHandler.text);
                isAttacking = false;
                yield break;
            }

            string resp = www.downloadHandler.text;
            Debug.Log("Attack result: " + resp);

            if (battleLogsUI != null)
                battleLogsUI.Refresh();

            
            PlayerPrefs.SetInt("SelectedPlayerId", selectedPlayerId);
            PlayerPrefs.SetString("SelectedUsername", selectedUsername);

            
            string pretty = $"Battle finished vs {selectedUsername}";
            PlayerPrefs.SetString("LastBattleResult", pretty);
            PlayerPrefs.Save();

            
            yield return new WaitForSeconds(reloadDelay);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    
    public void CloseBattleLogs()
    {
        PlayerPrefs.DeleteKey("SelectedPlayerId");
        PlayerPrefs.DeleteKey("SelectedUsername");
        PlayerPrefs.DeleteKey("LastBattleResult");
        PlayerPrefs.Save();

        selectedPlayerId = -1;
        selectedUsername = "";

        if (resultText != null) resultText.text = "";

        SetUIState(UIState.Default);
    }

    public void BackToBase()
    {
        
        PlayerPrefs.DeleteKey("SelectedPlayerId");
        PlayerPrefs.DeleteKey("SelectedUsername");
        PlayerPrefs.DeleteKey("LastBattleResult");
        PlayerPrefs.Save();

        SceneManager.LoadScene("Game Scene");
    }

    void SetResult(string msg)
    {
        if (resultText != null)
            resultText.text = msg;
    }

    void OnEnable()
    {
        isAttacking = false;
    }
}