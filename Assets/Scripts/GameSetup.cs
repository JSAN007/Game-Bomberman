using UnityEngine;

[RequireComponent(typeof(GameManager))]
public class GameSetup : MonoBehaviour
{
    private void Awake()
    {
        GameManager gameManager = GetComponent<GameManager>();

        if (gameManager.players == null || gameManager.players.Length == 0)
        {
            Debug.LogError("GameManager.players belum diisi.");
            return;
        }

        int humanCount = Mathf.Clamp(MatchConfig.humanPlayerCount, 1, gameManager.players.Length);

        for (int i = 0; i < gameManager.players.Length; i++)
        {
            GameObject player = gameManager.players[i];
            if (player == null) continue;

            bool isHuman = i < humanCount;

            MovementController movement = player.GetComponent<MovementController>();
            BombController bomb = player.GetComponent<BombController>();
            FuzzyBotAI bot = player.GetComponent<FuzzyBotAI>();

            if (bot == null)
            {
                bot = player.AddComponent<FuzzyBotAI>();
            }

            if (movement != null)
            {
                movement.SetControlMode(isHuman ? PlayerControlMode.Human : PlayerControlMode.AI);
            }

            if (bomb != null)
            {
                bomb.SetControlMode(isHuman ? PlayerControlMode.Human : PlayerControlMode.AI);
            }

            bot.players = gameManager.players;
            bot.enabled = !isHuman;

            if (bomb != null)
            {
                bot.obstacleMask = bomb.explosionLayerMask;
                bot.destructibleTiles = bomb.destructibleTiles;
            }
        }
    }
}
