using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(MovementController), typeof(BombController))]
public class FuzzyBotAI : MonoBehaviour
{
    private enum BotMode
    {
        Escape,
        Collect,
        Farm,
        Hunt,
        Reposition
    }

    [HideInInspector] public GameObject[] players;
    [HideInInspector] public Tilemap destructibleTiles;
    [HideInInspector] public LayerMask obstacleMask;

    [Header("Thinking")]
    public float thinkInterval = 0.14f;
    public int searchDepth = 10;
    public float cellTolerance = 0.08f;
    public float targetHoldTime = 0.10f;

    [Header("Bomb")]
    public float postBombEscapeTime = 1.0f;
    public float bombDecisionCooldown = 0.60f;

    [Header("Danger")]
    public int fallbackBombRadius = 2;
    public float fallbackBombFuseTime = 1.5f;
    public float dangerHorizon = 3.0f;
    public float dangerEscapeThreshold = 0.50f;

    [Header("Behavior")]
    public bool debugMode;

    private MovementController movement;
    private BombController bombController;

    private float nextThinkTime;
    private float forcedEscapeUntil;
    private float lastBombTime = -999f;
    private float targetLockUntil;

    private Vector2 targetCell;
    private bool hasTargetCell;
    private Vector2 lastBombCell;
    private Vector2 lastVisitedCell;
    private Vector2 previousVisitedCell;
    private bool hasVisitedHistory;
    private BotMode currentMode;

    private static readonly Vector2[] CardDirs =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right
    };

    private void Awake()
    {
        movement = GetComponent<MovementController>();
        bombController = GetComponent<BombController>();
    }

    private void OnEnable()
    {
        nextThinkTime = 0f;
        forcedEscapeUntil = 0f;
        lastBombTime = -999f;
        targetLockUntil = 0f;

        targetCell = RoundToCell(transform.position);
        hasTargetCell = false;
        lastBombCell = targetCell;

        lastVisitedCell = targetCell;
        previousVisitedCell = targetCell;
        hasVisitedHistory = false;

        currentMode = BotMode.Reposition;
    }

    private void Update()
    {
        if (players == null || players.Length == 0)
        {
            movement.SetExternalDirection(Vector2.zero);
            return;
        }

        Vector2 origin = RoundToCell(transform.position);
        UpdateVisitedHistory(origin);

        if (hasTargetCell)
        {
            if (IsNearCell(transform.position, targetCell, cellTolerance))
            {
                hasTargetCell = false;
                movement.SetExternalDirection(Vector2.zero);
            }
            else
            {
                movement.SetExternalDirection(DirectionToCell(transform.position, targetCell));
                return;
            }
        }

        if (!IsNearCell(transform.position, origin, 0.18f))
        {
            movement.SetExternalDirection(movement.CurrentDirection);
            return;
        }

        if (Time.time >= nextThinkTime && Time.time >= targetLockUntil)
        {
            nextThinkTime = Time.time + thinkInterval;
            Think(origin);
        }

        movement.SetExternalDirection(hasTargetCell ? DirectionToCell(transform.position, targetCell) : Vector2.zero);
    }

    private void UpdateVisitedHistory(Vector2 origin)
    {
        if (!hasVisitedHistory)
        {
            lastVisitedCell = origin;
            previousVisitedCell = origin;
            hasVisitedHistory = true;
            return;
        }

        if (origin != lastVisitedCell)
        {
            previousVisitedCell = lastVisitedCell;
            lastVisitedCell = origin;
        }
    }

    private void Think(Vector2 origin)
    {
        List<BombInfo> bombs = ObserveBombs();
        SearchResult reachable = BuildSearch(origin, searchDepth, null);

        float originDanger = EvaluateDanger(origin, bombs);
        bool mustEscape = HasActiveExplosion(origin) || Time.time < forcedEscapeUntil || originDanger >= dangerEscapeThreshold;

        if (mustEscape)
        {
            currentMode = BotMode.Escape;
            Vector2 safeCell = FindBestEscapeCell(origin, reachable, bombs, lastBombCell);
            SetTarget(NextStepToward(origin, safeCell, reachable), origin);
            LogMode(origin, safeCell, "ESCAPE");
            return;
        }

        if (!HasItem(origin) && CanUseBombAtCell(origin, bombs, out BombPlan immediateBombPlan))
        {
            currentMode = immediateBombPlan.enemyTargets > 0 ? BotMode.Hunt : BotMode.Farm;
            bombController.RequestBomb();
            lastBombTime = Time.time;
            forcedEscapeUntil = Time.time + postBombEscapeTime;
            lastBombCell = origin;

            SearchResult escapeSearch = BuildSearch(origin, searchDepth, origin);
            List<BombInfo> projectedBombs = CloneBombs(bombs);
            projectedBombs.Add(new BombInfo(origin, Mathf.Max(1, bombController.explosionRadius), Time.time + bombController.bombFuseTime));

            Vector2 safeCell = FindBestEscapeCell(origin, escapeSearch, projectedBombs, origin);
            SetTarget(NextStepToward(origin, safeCell, escapeSearch), origin);
            LogMode(origin, safeCell, "BOMB");
            return;
        }

        Goal itemGoal = FindBestItemGoal(reachable, bombs);
        if (itemGoal.valid && itemGoal.utility > 0.10f)
        {
            currentMode = BotMode.Collect;
            SetTarget(NextStepToward(origin, itemGoal.cell, reachable), origin);
            LogMode(origin, itemGoal.cell, "ITEM");
            return;
        }

        Goal farmGoal = FindBestFarmGoal(reachable, bombs);
        if (farmGoal.valid && farmGoal.utility > 0.10f)
        {
            currentMode = BotMode.Farm;
            SetTarget(NextStepToward(origin, farmGoal.cell, reachable), origin);
            LogMode(origin, farmGoal.cell, "FARM");
            return;
        }

        Goal huntGoal = FindBestHuntGoal(reachable, bombs);
        if (huntGoal.valid && huntGoal.utility > 0.08f)
        {
            currentMode = BotMode.Hunt;
            SetTarget(NextStepToward(origin, huntGoal.cell, reachable), origin);
            LogMode(origin, huntGoal.cell, "HUNT");
            return;
        }

        currentMode = BotMode.Reposition;
        Vector2 roamCell = FindBestRoamCell(origin, reachable, bombs);
        SetTarget(NextStepToward(origin, roamCell, reachable), origin);
        LogMode(origin, roamCell, "ROAM");
    }

    private void LogMode(Vector2 origin, Vector2 target, string label)
    {
        if (!debugMode) return;
        Debug.Log($"{name} [{label}] from={origin} to={target} mode={currentMode}");
    }

    private Goal FindBestItemGoal(SearchResult reachable, List<BombInfo> bombs)
    {
        Goal best = Goal.Invalid();

        foreach (KeyValuePair<Vector2, int> entry in reachable.distance)
        {
            Vector2 cell = entry.Key;
            if (!HasItem(cell)) continue;
            if (HasActiveExplosion(cell)) continue;

            float safety = 1f - EvaluateDanger(cell, bombs);
            if (safety < 0.20f) continue;

            float value = GetItemValue(cell);
            float openness = OpenNeighborCount(cell) / 4f;
            float utility =
                value * 3.8f +
                safety * 1.8f +
                openness * 0.6f -
                entry.Value * 0.30f;

            if (cell == previousVisitedCell) utility -= 1.0f;

            if (utility > best.utility)
            {
                best = new Goal(cell, utility);
            }
        }

        return best;
    }

    private Goal FindBestFarmGoal(SearchResult reachable, List<BombInfo> bombs)
    {
        Goal best = Goal.Invalid();

        foreach (KeyValuePair<Vector2, int> entry in reachable.distance)
        {
            Vector2 cell = entry.Key;
            if (HasActiveExplosion(cell)) continue;

            int destructibleCount = CountDestructiblesFromCell(cell);
            if (destructibleCount <= 0) continue;
            if (!CanUseBombAtCell(cell, bombs, out BombPlan plan)) continue;

            float safety = 1f - EvaluateDanger(cell, bombs);
            float openness = OpenNeighborCount(cell) / 4f;
            float utility =
                destructibleCount * 2.4f +
                plan.escapeScore * 1.8f +
                safety * 1.2f +
                openness * 0.4f -
                entry.Value * 0.35f;

            if (cell == previousVisitedCell) utility -= 0.8f;

            if (utility > best.utility)
            {
                best = new Goal(cell, utility);
            }
        }

        return best;
    }

    private Goal FindBestHuntGoal(SearchResult reachable, List<BombInfo> bombs)
    {
        Goal best = Goal.Invalid();

        foreach (KeyValuePair<Vector2, int> entry in reachable.distance)
        {
            Vector2 cell = entry.Key;
            if (HasActiveExplosion(cell)) continue;

            float safety = 1f - EvaluateDanger(cell, bombs);
            if (safety < 0.25f) continue;

            GameObject enemy = FindClosestEnemy(cell);
            if (enemy == null) continue;

            Vector2 enemyCell = RoundToCell(enemy.transform.position);
            float dist = Manhattan(cell, enemyCell);
            float laneBonus = SharesLane(cell, enemyCell) ? 0.8f : 0f;
            float utility =
                (1f - Mathf.Clamp01(dist / 8f)) * 2.1f +
                laneBonus +
                safety * 1.0f -
                entry.Value * 0.28f;

            if (cell == previousVisitedCell) utility -= 0.6f;

            if (utility > best.utility)
            {
                best = new Goal(cell, utility);
            }
        }

        return best;
    }

    private Vector2 FindBestEscapeCell(Vector2 origin, SearchResult reachable, List<BombInfo> bombs, Vector2 avoidBombOrigin)
    {
        Vector2 best = origin;
        float bestScore = float.NegativeInfinity;

        foreach (KeyValuePair<Vector2, int> entry in reachable.distance)
        {
            Vector2 cell = entry.Key;
            if (HasActiveExplosion(cell)) continue;

            float safety = 1f - EvaluateDanger(cell, bombs);
            float openness = OpenNeighborCount(cell) / 4f;
            float score =
                safety * 5.0f +
                openness * 1.8f -
                entry.Value * 0.22f;

            if (WouldBombHitCell(avoidBombOrigin, cell, Mathf.Max(1, bombController.explosionRadius)))
            {
                score -= 5f;
            }

            if (IsDeadEnd(cell)) score -= 2.0f;
            if (cell == previousVisitedCell) score -= 0.8f;

            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }

        return best;
    }

    private Vector2 FindBestRoamCell(Vector2 origin, SearchResult reachable, List<BombInfo> bombs)
    {
        Vector2 best = origin;
        float bestScore = float.NegativeInfinity;

        foreach (KeyValuePair<Vector2, int> entry in reachable.distance)
        {
            Vector2 cell = entry.Key;
            if (cell == origin) continue;
            if (HasActiveExplosion(cell)) continue;

            float safety = 1f - EvaluateDanger(cell, bombs);
            float openness = OpenNeighborCount(cell) / 4f;
            float score =
                safety * 2.8f +
                openness * 2.0f -
                entry.Value * 0.22f;

            if (HasItem(cell))
            {
                score += GetItemValue(cell) * 1.5f;
            }

            if (cell == previousVisitedCell) score -= 1.4f;
            if (IsDeadEnd(cell)) score -= 1.0f;

            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }

        return best;
    }

    private bool CanUseBombAtCell(Vector2 bombCell, List<BombInfo> bombs, out BombPlan plan)
    {
        plan = new BombPlan();

        if (!bombController.CanPlaceBomb()) return false;
        if (Time.time - lastBombTime < bombDecisionCooldown) return false;
        if (HasActiveExplosion(bombCell)) return false;

        int destructibleCount = CountDestructiblesFromCell(bombCell);
        int enemyCount = CountEnemiesInBlast(bombCell, Mathf.Max(1, bombController.explosionRadius));

        if (destructibleCount <= 0 && enemyCount <= 0)
        {
            return false;
        }

        SearchResult escapeSearch = BuildSearch(bombCell, searchDepth, bombCell);
        List<BombInfo> projectedBombs = CloneBombs(bombs);
        projectedBombs.Add(new BombInfo(bombCell, Mathf.Max(1, bombController.explosionRadius), Time.time + bombController.bombFuseTime));

        Vector2 safeCell = FindBestEscapeCell(bombCell, escapeSearch, projectedBombs, bombCell);
        bool safeFound =
            safeCell != bombCell &&
            !HasActiveExplosion(safeCell) &&
            EvaluateDanger(safeCell, projectedBombs) < 0.40f &&
            !WouldBombHitCell(bombCell, safeCell, Mathf.Max(1, bombController.explosionRadius));

        if (!safeFound)
        {
            return false;
        }

        plan.destructibleTargets = destructibleCount;
        plan.enemyTargets = enemyCount;
        plan.escapeCell = safeCell;
        plan.escapeScore = Mathf.Clamp01(1f - EvaluateDanger(safeCell, projectedBombs));
        return true;
    }

    private SearchResult BuildSearch(Vector2 origin, int maxDepth, Vector2? futureBombCell)
    {
        SearchResult result = new SearchResult();
        Queue<Vector2> queue = new Queue<Vector2>();

        result.parent[origin] = origin;
        result.distance[origin] = 0;
        queue.Enqueue(origin);

        while (queue.Count > 0)
        {
            Vector2 cell = queue.Dequeue();
            int depth = result.distance[cell];
            if (depth >= maxDepth) continue;

            foreach (Vector2 dir in CardDirs)
            {
                Vector2 next = cell + dir;
                if (result.distance.ContainsKey(next)) continue;
                if (IsBlockedForPath(next, origin, futureBombCell)) continue;

                result.parent[next] = cell;
                result.distance[next] = depth + 1;
                queue.Enqueue(next);
            }
        }

        return result;
    }

    private bool IsBlockedForPath(Vector2 cell, Vector2 origin, Vector2? futureBombCell)
    {
        if (futureBombCell.HasValue && cell == futureBombCell.Value && cell != origin)
        {
            return true;
        }

        if (HasActiveExplosion(cell))
        {
            return true;
        }

        if (IsStageBlocked(cell) || HasDestructibleTile(cell))
        {
            return true;
        }

        int bombMask = LayerMask.GetMask("Bomb");
        if (bombMask != 0)
        {
            Collider2D hit = Physics2D.OverlapBox(cell, Vector2.one * 0.55f, 0f, bombMask);
            if (hit != null)
            {
                Vector2 bombCell = RoundToCell(hit.transform.position);
                if (bombCell != origin)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private List<BombInfo> ObserveBombs()
    {
        List<BombInfo> bombs = new List<BombInfo>();
        HashSet<Vector2> seen = new HashSet<Vector2>();

        BombRuntime[] runtimes = FindObjectsByType<BombRuntime>(FindObjectsSortMode.None);
        foreach (BombRuntime runtime in runtimes)
        {
            if (runtime == null || !runtime.gameObject.activeInHierarchy) continue;
            Vector2 cell = RoundToCell(runtime.transform.position);
            bombs.Add(new BombInfo(cell, Mathf.Max(1, runtime.blastRadius), runtime.DetonateAt));
            seen.Add(cell);
        }

        int bombMask = LayerMask.GetMask("Bomb");
        if (bombMask != 0)
        {
            Collider2D[] rawBombs = Physics2D.OverlapCircleAll(transform.position, searchDepth + 2f, bombMask);
            foreach (Collider2D raw in rawBombs)
            {
                if (raw == null) continue;
                Vector2 cell = RoundToCell(raw.transform.position);
                if (seen.Contains(cell)) continue;
                bombs.Add(new BombInfo(cell, fallbackBombRadius, Time.time + fallbackBombFuseTime));
            }
        }

        return bombs;
    }

    private float EvaluateDanger(Vector2 cell, List<BombInfo> bombs)
    {
        if (HasActiveExplosion(cell))
        {
            return 1f;
        }

        float danger = 0f;
        foreach (BombInfo bomb in bombs)
        {
            if (!WouldBombHitCell(bomb.cell, cell, bomb.radius)) continue;

            float timeToBlast = Mathf.Max(0f, bomb.detonateAt - Time.time);
            float timeFactor = 1f - Mathf.Clamp01(timeToBlast / Mathf.Max(0.1f, dangerHorizon));
            float distanceFactor = 1f - Mathf.Clamp01((Manhattan(cell, bomb.cell) - 0.5f) / Mathf.Max(1f, bomb.radius));
            float local = Mathf.Clamp01(0.2f + timeFactor * 0.8f) * Mathf.Clamp01(0.45f + distanceFactor * 0.55f);
            danger = Mathf.Max(danger, local);
        }

        if (IsDeadEnd(cell))
        {
            danger = Mathf.Clamp01(danger + 0.08f);
        }

        return danger;
    }

    private int CountEnemiesInBlast(Vector2 bombCell, int radius)
    {
        int count = 0;
        foreach (GameObject player in players)
        {
            if (player == null || player == gameObject || !player.activeSelf) continue;
            if (WouldBombHitCell(bombCell, RoundToCell(player.transform.position), radius))
            {
                count++;
            }
        }
        return count;
    }

    private int CountDestructiblesFromCell(Vector2 origin)
    {
        int count = 0;
        int radius = Mathf.Max(1, bombController.explosionRadius);

        foreach (Vector2 dir in CardDirs)
        {
            for (int step = 1; step <= radius; step++)
            {
                Vector2 cell = origin + dir * step;
                if (HasDestructibleTile(cell))
                {
                    count++;
                    break;
                }
                if (IsStageBlocked(cell))
                {
                    break;
                }
            }
        }

        return count;
    }

    private float GetItemValue(Vector2 cell)
    {
        int itemMask = LayerMask.GetMask("Item");
        if (itemMask == 0) return 0f;

        Collider2D[] hits = Physics2D.OverlapBoxAll(cell, Vector2.one * 0.45f, 0f, itemMask);
        float best = 0f;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            ItemPickup item = hit.GetComponent<ItemPickup>();
            if (item == null)
            {
                best = Mathf.Max(best, 0.55f);
                continue;
            }

            switch (item.type)
            {
                case ItemPickup.ItemType.ExtraBomb:
                    best = Mathf.Max(best, bombController.bombAmount <= 1 ? 1f : 0.75f);
                    break;
                case ItemPickup.ItemType.BlastRadius:
                    best = Mathf.Max(best, bombController.explosionRadius <= 2 ? 0.95f : 0.70f);
                    break;
                case ItemPickup.ItemType.SpeedIncrease:
                    best = Mathf.Max(best, movement.speed <= 5f ? 0.90f : 0.65f);
                    break;
            }
        }

        return best;
    }

    private bool HasItem(Vector2 cell)
    {
        int itemMask = LayerMask.GetMask("Item");
        return itemMask != 0 && Physics2D.OverlapBox(cell, Vector2.one * 0.45f, 0f, itemMask) != null;
    }

    private int OpenNeighborCount(Vector2 cell)
    {
        int count = 0;
        foreach (Vector2 dir in CardDirs)
        {
            if (!IsBlockedForPath(cell + dir, cell, null))
            {
                count++;
            }
        }
        return count;
    }

    private bool HasActiveExplosion(Vector2 cell)
    {
        int explosionMask = LayerMask.GetMask("Explosion");
        if (explosionMask == 0) return false;
        return Physics2D.OverlapBox(cell, Vector2.one * 0.45f, 0f, explosionMask) != null;
    }

    private bool IsDeadEnd(Vector2 cell)
    {
        return OpenNeighborCount(cell) <= 1;
    }

    private bool IsStageBlocked(Vector2 cell)
    {
        if (obstacleMask == 0) return false;
        return Physics2D.OverlapBox(cell, Vector2.one * 0.55f, 0f, obstacleMask) != null;
    }

    private bool HasDestructibleTile(Vector2 cell)
    {
        if (destructibleTiles == null) return false;
        return destructibleTiles.GetTile(destructibleTiles.WorldToCell(cell)) != null;
    }

    private bool WouldBombHitCell(Vector2 bombPos, Vector2 targetCell, int radius)
    {
        if (bombPos == targetCell) return true;

        bool sameRow = Mathf.Abs(bombPos.y - targetCell.y) < 0.1f;
        bool sameColumn = Mathf.Abs(bombPos.x - targetCell.x) < 0.1f;
        if (!sameRow && !sameColumn) return false;
        if (Manhattan(bombPos, targetCell) > radius) return false;

        return PathClearBetween(bombPos, targetCell);
    }

    private bool PathClearBetween(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        Vector2 step;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            step = new Vector2(Mathf.Sign(delta.x), 0f);
        }
        else
        {
            step = new Vector2(0f, Mathf.Sign(delta.y));
        }

        Vector2 cell = from + step;
        while (cell != to)
        {
            if (IsStageBlocked(cell) || HasDestructibleTile(cell))
            {
                return false;
            }
            cell += step;
        }

        return true;
    }

    private GameObject FindClosestEnemy(Vector2 origin)
    {
        GameObject closest = null;
        float bestDist = float.MaxValue;

        foreach (GameObject player in players)
        {
            if (player == null || player == gameObject || !player.activeSelf) continue;

            float dist = Manhattan(origin, RoundToCell(player.transform.position));
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = player;
            }
        }

        return closest;
    }

    private Vector2 NextStepToward(Vector2 origin, Vector2 target, SearchResult reachable)
    {
        if (target == origin || !reachable.parent.ContainsKey(target))
        {
            return origin;
        }

        Vector2 current = target;
        while (reachable.parent[current] != origin)
        {
            current = reachable.parent[current];
        }

        return current;
    }

    private void SetTarget(Vector2 nextCell, Vector2 origin)
    {
        if (nextCell == origin)
        {
            hasTargetCell = false;
            targetCell = origin;
            return;
        }

        targetCell = nextCell;
        hasTargetCell = true;
        targetLockUntil = Time.time + targetHoldTime;
    }

    private Vector2 DirectionToCell(Vector2 worldPosition, Vector2 cell)
    {
        Vector2 delta = cell - worldPosition;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return new Vector2(Mathf.Sign(delta.x), 0f);
        }
        return new Vector2(0f, Mathf.Sign(delta.y));
    }

    private bool IsNearCell(Vector2 worldPosition, Vector2 cell, float tolerance)
    {
        return Vector2.Distance(worldPosition, cell) <= tolerance;
    }

    private Vector2 RoundToCell(Vector2 worldPosition)
    {
        return new Vector2(Mathf.Round(worldPosition.x), Mathf.Round(worldPosition.y));
    }

    private float Manhattan(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private bool SharesLane(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.1f || Mathf.Abs(a.y - b.y) < 0.1f;
    }

    private List<BombInfo> CloneBombs(List<BombInfo> bombs)
    {
        List<BombInfo> copy = new List<BombInfo>(bombs.Count);
        foreach (BombInfo bomb in bombs)
        {
            copy.Add(bomb);
        }
        return copy;
    }

    private struct BombInfo
    {
        public Vector2 cell;
        public int radius;
        public float detonateAt;

        public BombInfo(Vector2 cell, int radius, float detonateAt)
        {
            this.cell = cell;
            this.radius = radius;
            this.detonateAt = detonateAt;
        }
    }

    private struct Goal
    {
        public Vector2 cell;
        public float utility;
        public bool valid;

        public Goal(Vector2 cell, float utility)
        {
            this.cell = cell;
            this.utility = utility;
            valid = true;
        }

        public static Goal Invalid()
        {
            Goal goal = new Goal();
            goal.cell = Vector2.zero;
            goal.utility = float.NegativeInfinity;
            goal.valid = false;
            return goal;
        }
    }

    private struct BombPlan
    {
        public int destructibleTargets;
        public int enemyTargets;
        public Vector2 escapeCell;
        public float escapeScore;
    }

    private class SearchResult
    {
        public readonly Dictionary<Vector2, Vector2> parent = new Dictionary<Vector2, Vector2>();
        public readonly Dictionary<Vector2, int> distance = new Dictionary<Vector2, int>();
    }
}
