using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public GameObject[] players;

    [SerializeField] private float endRoundDelay = 2f;
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    private bool roundEnded;

    public void CheckWinState()
    {
        if (roundEnded) return;

        int aliveCount = 0;
        GameObject lastAlive = null;

        foreach (GameObject player in players)
        {
            if (player != null && player.activeSelf)
            {
                aliveCount++;
                lastAlive = player;
            }
        }

        if (aliveCount <= 1)
        {
            roundEnded = true;

            if (lastAlive != null)
            {
                Debug.Log(lastAlive.name + " menang!");
            }
            else
            {
                Debug.Log("Draw!");
            }

            Invoke(nameof(ReturnToMainMenu), endRoundDelay);
        }
    }

    public void ReturnToMainMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError("Scene Main Menu belum ada di Build Settings.");
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}