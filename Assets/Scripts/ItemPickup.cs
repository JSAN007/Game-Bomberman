using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public enum ItemType
    {
        ExtraBomb,
        BlastRadius,
        SpeedIncrease,
    }

    public ItemType type;

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool isPlayerTag = other.CompareTag("Player");
        bool isPlayerLayer = other.gameObject.layer == LayerMask.NameToLayer("Player");

        if (!isPlayerTag && !isPlayerLayer)
        {
            return;
        }

        OnItemPickup(other.gameObject);
    }

    private void OnItemPickup(GameObject player)
    {
        // SFX: suara ambil item
        AudioManager.Instance?.PlayPowerUp();

        switch (type)
        {
            case ItemType.ExtraBomb:
                if (player.TryGetComponent(out BombController bombController))
                {
                    bombController.AddBomb();
                }
                break;

            case ItemType.BlastRadius:
                if (player.TryGetComponent(out BombController bombRadiusController))
                {
                    bombRadiusController.explosionRadius++;
                }
                break;

            case ItemType.SpeedIncrease:
                if (player.TryGetComponent(out MovementController movementController))
                {
                    movementController.speed++;
                }
                break;
        }

        Destroy(gameObject);
    }
}