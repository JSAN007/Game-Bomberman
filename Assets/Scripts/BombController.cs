using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class BombController : MonoBehaviour
{
    [Header("Bomb")]
    public GameObject bombPrefab;
    public Key inputKey = Key.Space;
    public float bombFuseTime = 3f;
    public int bombAmount = 1;
    private int bombsRemaining;

    [Header("Control")]
    public PlayerControlMode controlMode = PlayerControlMode.Human;
    private bool aiBombRequested;

    [Header("Explosion")]
    public Explosion explosionPrefab;
    public LayerMask explosionLayerMask;
    public float explosionDuration = 1f;
    public int explosionRadius = 1;

    [Header("Tilemap")]
    public Tilemap destructibleTiles;
    public Destructible destructiblePrefab;

    private void OnEnable()
    {
        bombsRemaining = bombAmount;
        aiBombRequested = false;
    }

    public void SetControlMode(PlayerControlMode mode)
    {
        controlMode = mode;
        aiBombRequested = false;
    }

    public void RequestBomb()
    {
        aiBombRequested = true;
    }

    public bool CanPlaceBomb()
    {
        return bombsRemaining > 0;
    }

    public void NotifyBombExploded()
    {
        bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmount);
    }

    private void Update()
    {
        if (bombPrefab == null || explosionPrefab == null) return;
        if (bombsRemaining <= 0) return;

        bool shouldPlaceBomb = false;

        if (controlMode == PlayerControlMode.Human)
        {
            if (Keyboard.current == null) return;
            shouldPlaceBomb = Keyboard.current[inputKey].wasPressedThisFrame;
        }
        else
        {
            shouldPlaceBomb = aiBombRequested;
            aiBombRequested = false;
        }

        if (shouldPlaceBomb)
        {
            TryPlaceBomb();
        }
    }

    private void TryPlaceBomb()
    {
        Vector2 position = GetRoundedPosition();

        if (HasBombAtPosition(position))
        {
            return;
        }

        GameObject bomb = Instantiate(bombPrefab, position, Quaternion.identity);
        BombRuntime runtime = bomb.GetComponent<BombRuntime>();

        if (runtime == null)
        {
            runtime = bomb.AddComponent<BombRuntime>();
        }

        runtime.Configure(
            this,
            gameObject,
            bombFuseTime,
            explosionRadius,
            explosionPrefab,
            explosionLayerMask,
            explosionDuration,
            destructibleTiles,
            destructiblePrefab
        );

        bombsRemaining = Mathf.Max(0, bombsRemaining - 1);

        // SFX: suara bom diletakkan
        AudioManager.Instance?.PlayBombDrop();
    }

    private Vector2 GetRoundedPosition()
    {
        Vector2 position = transform.position;
        position.x = Mathf.Round(position.x);
        position.y = Mathf.Round(position.y);
        return position;
    }

    private bool HasBombAtPosition(Vector2 position)
    {
        int bombMask = LayerMask.GetMask("Bomb");

        if (bombMask == 0)
        {
            return false;
        }

        Collider2D hit = Physics2D.OverlapBox(position, Vector2.one * 0.6f, 0f, bombMask);
        return hit != null;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Bomb"))
        {
            other.isTrigger = false;
        }
    }

    public void AddBomb()
    {
        bombAmount++;
        bombsRemaining++;
    }
}
