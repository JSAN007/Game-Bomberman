using UnityEngine;
using UnityEngine.Tilemaps;

public class BombRuntime : MonoBehaviour
{
    public GameObject owner;
    public float placedTime;
    public float fuseTime = 3f;
    public int blastRadius = 1;

    public float DetonateAt => placedTime + Mathf.Max(0.1f, fuseTime);
    public float RemainingTime => Mathf.Max(0f, DetonateAt - Time.time);

    private BombController ownerController;
    private Explosion explosionPrefab;
    private LayerMask explosionLayerMask;
    private float explosionDuration = 1f;
    private Tilemap destructibleTiles;
    private Destructible destructiblePrefab;

    private bool exploded;
    private bool slotReleased;

    private void Awake()
    {
        if (placedTime <= 0f)
        {
            placedTime = Time.time;
        }

        fuseTime = Mathf.Max(0.1f, fuseTime);
        blastRadius = Mathf.Max(1, blastRadius);
    }

    public void Configure(
        BombController controller,
        GameObject bombOwner,
        float bombFuseTime,
        int radius,
        Explosion explosionPrefab,
        LayerMask explosionLayerMask,
        float explosionDuration,
        Tilemap destructibleTiles,
        Destructible destructiblePrefab)
    {
        ownerController = controller;
        owner = bombOwner;
        placedTime = Time.time;
        fuseTime = Mathf.Max(0.1f, bombFuseTime);
        blastRadius = Mathf.Max(1, radius);

        this.explosionPrefab = explosionPrefab;
        this.explosionLayerMask = explosionLayerMask;
        this.explosionDuration = explosionDuration;
        this.destructibleTiles = destructibleTiles;
        this.destructiblePrefab = destructiblePrefab;
    }

    private void Update()
    {
        if (!exploded && Time.time >= DetonateAt)
        {
            ExplodeNow();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (exploded) return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            ExplodeNow();
        }
    }

    private void ExplodeNow()
    {
        if (exploded) return;
        exploded = true;

        Vector2 position = transform.position;
        position.x = Mathf.Round(position.x);
        position.y = Mathf.Round(position.y);

        if (explosionPrefab != null)
        {
            Explosion centerExplosion = Instantiate(explosionPrefab, position, Quaternion.identity);
            centerExplosion.SetActiveRenderer(centerExplosion.start);
            centerExplosion.DestroyAfter(explosionDuration);

            Explode(position, Vector2.up, blastRadius);
            Explode(position, Vector2.down, blastRadius);
            Explode(position, Vector2.right, blastRadius);
            Explode(position, Vector2.left, blastRadius);
        }

        ReleaseBombSlot();
        Destroy(gameObject);
    }

    private void Explode(Vector2 position, Vector2 direction, int length)
    {
        if (length <= 0 || explosionPrefab == null)
        {
            return;
        }

        position += direction;

        if (Physics2D.OverlapBox(position, Vector2.one * 0.5f, 0f, explosionLayerMask))
        {
            ClearDestructible(position);
            return;
        }

        Explosion explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        explosion.SetActiveRenderer(length > 1 ? explosion.middle : explosion.end);
        explosion.SetDirection(direction);
        explosion.DestroyAfter(explosionDuration);

        Explode(position, direction, length - 1);
    }

    private void ClearDestructible(Vector2 position)
    {
        if (destructibleTiles == null) return;

        Vector3Int cell = destructibleTiles.WorldToCell(position);
        TileBase tile = destructibleTiles.GetTile(cell);

        if (tile != null)
        {
            if (destructiblePrefab != null)
            {
                Instantiate(destructiblePrefab, position, Quaternion.identity);
            }

            destructibleTiles.SetTile(cell, null);
        }
    }

    private void ReleaseBombSlot()
    {
        if (slotReleased) return;
        slotReleased = true;

        if (ownerController != null)
        {
            ownerController.NotifyBombExploded();
        }
    }

    private void OnDestroy()
    {
        ReleaseBombSlot();
    }
}
