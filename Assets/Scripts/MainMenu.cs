using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameplayScene = "Bomberman";

    [Header("Default Match")]
    [SerializeField] private int defaultHumanPlayerCount = 1;

    public void Load1P()
    {
        LoadMatch(1);
    }

    public void Load2P()
    {
        LoadMatch(2);
    }

    public void Load3P()
    {   
        LoadMatch(3);
    }

    public void Load4P()
    {
        LoadMatch(4);
    }

    public void Mainmenu()
    {
        Time.timeScale = 1f; // reset timeScale kalau dipanggil dari pause menu
        SceneManager.LoadScene(5);
    }

    // Wrapper buat wiring button lama di scene sekarang:
    // 2P button -> P1
    // 3P button -> P2
    // 4P button -> P3
    public void P1()
    {
        Load2P();
    }

    public void P2()
    {
        Load3P();
    }

    public void P3()
    {
        Load4P();
    }

    public void PlayDefault()
    {
        LoadMatch(defaultHumanPlayerCount);
    }

    public void QuitGame()
    {
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }

    private void LoadMatch(int humanPlayerCount)
    {
        humanPlayerCount = Mathf.Clamp(humanPlayerCount, 1, 4);

        MatchConfig.humanPlayerCount = humanPlayerCount;
        MatchConfig.gameplayScene = gameplayScene;

        if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
        {
            Debug.LogError($"Scene '{gameplayScene}' belum masuk Build Settings.");
            return;
        }

        SceneManager.LoadScene(gameplayScene);
    }
}
