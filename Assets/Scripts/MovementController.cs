using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class MovementController : MonoBehaviour
{
    public Rigidbody2D rb { get; private set; }
    public Vector2 CurrentDirection => direction;

    private Vector2 direction = Vector2.zero;
    private Vector2 aiDirection = Vector2.zero;

    public float speed = 5f;
    public PlayerControlMode controlMode = PlayerControlMode.Human;

    [Header("Input Keys")]
    public Key inputUp = Key.W;
    public Key inputDown = Key.S;
    public Key inputLeft = Key.A;
    public Key inputRight = Key.D;

    [Header("Sprite Renderers")]
    public AnimatedSpriteRenderer spriteRendererUp;
    public AnimatedSpriteRenderer spriteRendererDown;
    public AnimatedSpriteRenderer spriteRendererRight;
    public AnimatedSpriteRenderer spriteRendererLeft;
    public AnimatedSpriteRenderer spriteRendererDeath;

    private AnimatedSpriteRenderer activeSpriteRenderer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        activeSpriteRenderer = spriteRendererDown;
    }

    public void SetControlMode(PlayerControlMode mode)
    {
        controlMode = mode;
        aiDirection = Vector2.zero;
    }

    public void SetExternalDirection(Vector2 newDirection)
    {
        aiDirection = SnapToCardinal(newDirection);
    }

    private void Update()
    {
        Vector2 desiredDirection = controlMode == PlayerControlMode.Human
            ? ReadHumanInput()
            : aiDirection;

        SetDirection(desiredDirection, GetRendererForDirection(desiredDirection));
    }

    private void FixedUpdate()
    {
        Vector2 position = rb.position;
        Vector2 translation = direction * speed * Time.fixedDeltaTime;
        rb.MovePosition(position + translation);
    }

    private Vector2 ReadHumanInput()
    {
        if (Keyboard.current == null) return Vector2.zero;

        if (Keyboard.current[inputUp].isPressed) return Vector2.up;
        if (Keyboard.current[inputDown].isPressed) return Vector2.down;
        if (Keyboard.current[inputRight].isPressed) return Vector2.right;
        if (Keyboard.current[inputLeft].isPressed) return Vector2.left;

        return Vector2.zero;
    }

    private AnimatedSpriteRenderer GetRendererForDirection(Vector2 newDirection)
    {
        newDirection = SnapToCardinal(newDirection);

        if (newDirection == Vector2.up) return spriteRendererUp;
        if (newDirection == Vector2.down) return spriteRendererDown;
        if (newDirection == Vector2.right) return spriteRendererRight;
        if (newDirection == Vector2.left) return spriteRendererLeft;

        return activeSpriteRenderer != null ? activeSpriteRenderer : spriteRendererDown;
    }

    private void SetDirection(Vector2 newDirection, AnimatedSpriteRenderer spriteRenderer)
    {
        direction = SnapToCardinal(newDirection);

        if (spriteRendererUp != null) spriteRendererUp.enabled = spriteRenderer == spriteRendererUp;
        if (spriteRendererDown != null) spriteRendererDown.enabled = spriteRenderer == spriteRendererDown;
        if (spriteRendererRight != null) spriteRendererRight.enabled = spriteRenderer == spriteRendererRight;
        if (spriteRendererLeft != null) spriteRendererLeft.enabled = spriteRenderer == spriteRendererLeft;

        if (spriteRenderer != null)
        {
            activeSpriteRenderer = spriteRenderer;
        }

        if (activeSpriteRenderer != null)
        {
            activeSpriteRenderer.idle = direction == Vector2.zero;
        }
    }

    private Vector2 SnapToCardinal(Vector2 value)
    {
        if (value == Vector2.zero) return Vector2.zero;

        if (Mathf.Abs(value.x) > Mathf.Abs(value.y))
        {
            return new Vector2(Mathf.Sign(value.x), 0f);
        }

        return new Vector2(0f, Mathf.Sign(value.y));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            DeathSequence();
        }
    }

    private void DeathSequence()
    {
        enabled = false;

        // SFX: suara mati
        AudioManager.Instance?.PlayDeath();

        BombController bombController = GetComponent<BombController>();
        if (bombController != null)
        {
            bombController.enabled = false;
        }

        if (spriteRendererUp != null) spriteRendererUp.enabled = false;
        if (spriteRendererDown != null) spriteRendererDown.enabled = false;
        if (spriteRendererRight != null) spriteRendererRight.enabled = false;
        if (spriteRendererLeft != null) spriteRendererLeft.enabled = false;
        if (spriteRendererDeath != null) spriteRendererDeath.enabled = true;

        Invoke(nameof(OnDeathSequenceEnded), 1.25f);
    }

    private void OnDeathSequenceEnded()
    {
        gameObject.SetActive(false);

        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.CheckWinState();
        }
    }
}
