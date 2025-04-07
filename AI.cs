using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AI : MonoBehaviour
{
    //Context-steering AI to determine best direction to travel
    //Weighs a number of directions and picks the best one to move in
    //Two modes: orbit and attack
    //Orbit moves around the target in a circular motion
    //Attack moves directly towards the target to get in range

    public struct Weight
    {
        public float weight;
        public Vector2 dir;
    }

    Rigidbody2D rb;

    Vector2 unobstructedDir;
    int unobstructedIndex = 0;

    bool pathObstructed = false;

    [SerializeField] Transform target;
    [Header("Settings")]
    [SerializeField] float currentDirSmoothing = 0.5f;
    [SerializeField] float objectRadius = 0.2f;
    [SerializeField] Vector2 centerOffset = new Vector2(0, 0);
    [SerializeField] float speed = 2f;
    [SerializeField] float orbitRadius = 1f;
    [SerializeField] float orbitRadiusThickness = 0.2f;
    [SerializeField] float minDistanceThreshold = 0.05f;
    [SerializeField] float biasThreshold = 1.1f;
    float orbitOffset;
    [Header("Weight Settings")]
    [SerializeField][Range(4, 24)] int numOfWeights = 16; //Number of directions to choose from
    [SerializeField] bool visualizeWeights = true;
    [Header("Obstacle Settings")]
    [SerializeField][Range(0.01f, 1000f)] float obstacleDistanceSensitivity = .48f; //Specifies how bad being close to obstacles is
    [SerializeField] float desireKeepSameDir = 0.1f; //Specified desire to keep going in same dir rather than move towards target
    [SerializeField] float noObstacleDesire = 0.3f;
    [SerializeField] string comradeTag = "Enemy"; //Used to prioritize avoiding clumping with comrades
    [SerializeField] LayerMask obstacleMask;
    [SerializeField] LayerMask wallMask;
    [Header("Path Obstruction Settings")]
    [Tooltip("Weight value to identity a path obstruction")]
    [SerializeField] float obstructionDetectionThreshold = 0.1f;
    [Tooltip("Maximum weight value needed to change current direction when a path obstruction is detected")]
    [SerializeField] float maxObstructionChangeDir = 0f;
    [Tooltip("Minimum weight value of current direction needed to change current direction when a path obstruction is detected")]
    [SerializeField] float minObstructionChangeDir = -0.8f;
    Weight[] weights;

    Weight[] longRange_Weights;

    Vector2 newDir;
    Vector2 currentDir;

    public Vector2 CurrentDir { get => currentDir; }

    bool canMove = true;

    public void SetTarget(Transform target)
    {
        this.target = target;
    }
    public void SetSpeed(float speed)
    {
        this.speed = speed;
    }
    public void SetCanMove(bool canMove)
    {
        this.canMove = canMove;
    }

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        orbitOffset = Random.Range(0, orbitRadius / 2f);

        //Initialize the weights and their directions
        float angle = 0;
        float dAngle = 360f / numOfWeights;
        weights = new Weight[numOfWeights];
        longRange_Weights = new Weight[numOfWeights];
        for (int i = 0; i < numOfWeights; i++)
        {
            float x = Mathf.Cos(angle * Mathf.Deg2Rad);
            float y = Mathf.Sin(angle * Mathf.Deg2Rad);
            weights[i].weight = 0;
            weights[i].dir = new Vector2(x, y).normalized;
            longRange_Weights[i].weight = 0;
            longRange_Weights[i].dir = weights[i].dir;

            angle += dAngle;
        }
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void Move()
    {
        currentDir = Vector2.Lerp(currentDir, newDir, currentDirSmoothing * Time.deltaTime * 10f);
        rb.velocity = currentDir * speed;
    }

    //Move directly towards target transform
    public void MoveTowardsTarget()
    {
        if (!canMove || target == null) { return; }

        Vector2 dirToTarget = (target.position - transform.position).normalized;

        float largestWeight = 0;
        //Set baseline weight based off dot product to desire direction
        for (int i = 0; i < numOfWeights; i++)
        {
            float w = Vector2.Dot(weights[i].dir, dirToTarget);
            weights[i].weight = w;

            float w2 = Vector2.Dot(weights[i].dir, currentDir);
            weights[i].weight += w2 * desireKeepSameDir;
        }

        ObstacleDetection(false);

        //Find largest weight and head in that direction
        largestWeight = weights[0].weight;
        int largestWeightIndex = 0;
        newDir = weights[0].dir;
        for (int i = 1; i < numOfWeights; i++)
        {
            if (weights[i].weight > largestWeight)
            {
                newDir = weights[i].dir;
                largestWeightIndex = i;
                largestWeight = weights[i].weight;
            }
        }

        Move();
    }
    //Move directly towards provided target position
    public bool MoveTowardsTarget(Vector3 targetPosition)
    {
        if (!canMove) {return false;}

        Vector2 vectorToTarget = targetPosition - transform.position;
        float distToTarget = vectorToTarget.magnitude;
        Vector2 dirToTarget = vectorToTarget.normalized;

        if (distToTarget < minDistanceThreshold) 
        { 
            rb.velocity = Vector2.zero; 
            return false; 
        }

        float largestWeight = 0;
        //Set baseline weight based off dot product to desire direction
        for (int i = 0; i < numOfWeights; i++)
        {
            float w = Vector2.Dot(weights[i].dir, dirToTarget);
            weights[i].weight = w;

            float w2 = Vector2.Dot(weights[i].dir, currentDir);
            weights[i].weight += w2 * desireKeepSameDir;
        }

        ObstacleDetection(false);

        //Find largest weight and head in that direction
        largestWeight = weights[0].weight;
        int largestWeightIndex = 0;
        newDir = weights[0].dir;
        for (int i = 1; i < numOfWeights; i++)
        {
            if (weights[i].weight > largestWeight)
            {
                newDir = weights[i].dir;
                largestWeightIndex = i;
                largestWeight = weights[i].weight;
            }
        }

        Move();

        return true;
    }

    //Move towards and orbit around the target transform
    bool isOrbiting;
    public void OrbitAroundTarget()
    {
        if (!canMove || target == null) return;

        Vector2 vectorToTarget = target.position - transform.position;
        Vector2 dirToTarget = vectorToTarget.normalized;

        float distToTarget = vectorToTarget.magnitude;

        if (distToTarget > orbitRadius + orbitOffset + orbitRadiusThickness)
            isOrbiting = false;
        else if (distToTarget <= orbitRadius + orbitOffset)
            isOrbiting = true;

        float largestWeight = 0;
        //Set baseline weight based off dot product to desire direction
        for (int i = 0; i < numOfWeights; i++)
        {
            float w = Vector2.Dot(weights[i].dir, dirToTarget);

            if (isOrbiting)
            {
                w = 1 - Mathf.Abs(w);
            }

            weights[i].weight = w;

            float w2 = Vector2.Dot(weights[i].dir, currentDir);
            weights[i].weight += w2 * desireKeepSameDir;
        }

        ObstacleDetection(isOrbiting);

        //Find largest weight and head in that direction
        largestWeight = weights[0].weight;
        int largestWeightIndex = 0;
        newDir = weights[0].dir;
        for (int i = 1; i < numOfWeights; i++)
        {
            if (weights[i].weight > largestWeight)
            {
                newDir = weights[i].dir;
                largestWeightIndex = i;
                largestWeight = weights[i].weight;
            }
        }

        Move();
    }
    //Move towards and orbit around provided target position
    public void OrbitAroundTarget(Vector3 targetPos)
    {
        if (!canMove) return;

        Vector2 vectorToTarget = targetPos - transform.position;
        Vector2 dirToTarget = vectorToTarget.normalized;

        float distToTarget = vectorToTarget.magnitude;

        if (distToTarget > orbitRadius + orbitOffset + orbitRadiusThickness)
            isOrbiting = false;
        else if (distToTarget <= orbitRadius + orbitOffset)
            isOrbiting = true;

        float largestWeight = 0;
        //Set baseline weight based off dot product to desire direction
        for (int i = 0; i < numOfWeights; i++)
        {
            float w = Vector2.Dot(weights[i].dir, dirToTarget);

            if (isOrbiting)
            {
                w = 1 - Mathf.Abs(w);
            }

            weights[i].weight = w;

            float w2 = Vector2.Dot(weights[i].dir, currentDir);
            weights[i].weight += w2 * desireKeepSameDir;
        }

        ObstacleDetection(isOrbiting);

        //Find largest weight and head in that direction
        largestWeight = weights[0].weight;
        int largestWeightIndex = 0;
        newDir = weights[0].dir;
        for (int i = 1; i < numOfWeights; i++)
        {
            if (weights[i].weight > largestWeight)
            {
                newDir = weights[i].dir;
                largestWeightIndex = i;
                largestWeight = weights[i].weight;
            }
        }

        Move();
    }

    private void PathObstruction(float largestWeight, int largestWeightIndex)
    {
        //If path obstructed priortize traveling in one direction until unobstructed
        if (pathObstructed)
        {
            float newLargestWeight = -1;
            int newLargestWeightIndex = 0;
            for (int i = 0; i < numOfWeights; i++)
            {
                float w = Vector2.Dot(weights[i].dir, unobstructedDir);
                weights[i].weight += w * 0.4f;
                if (weights[i].weight > newLargestWeight)
                {
                    newLargestWeight = weights[i].weight;

                    newLargestWeightIndex = i;
                    newDir = weights[i].dir;
                }
            }

            if (newLargestWeight > maxObstructionChangeDir || weights[unobstructedIndex].weight < minObstructionChangeDir)
            {
                unobstructedDir = newDir;
                unobstructedIndex = newLargestWeightIndex;
            }

            newDir = unobstructedDir;

            if (weights[unobstructedIndex].weight >= obstructionDetectionThreshold)
            {
                newDir = weights[largestWeightIndex].dir;
                pathObstructed = false;
            }
        }

        if (!pathObstructed && largestWeight < obstructionDetectionThreshold)
        {
            pathObstructed = true;
            unobstructedDir = newDir;
            unobstructedIndex = largestWeightIndex;
        }
    }

    //Sets weight of possible directions based on nearness to obstacles
    //and propagates that nearness to nearby directions
    private void ObstacleDetection(bool isOrbiting)
    {
        //Check for obstacles in the path of weights
        for (int i = 0; i < numOfWeights; i++)
        {
            RaycastHit2D[] hitInfos = Physics2D.RaycastAll((Vector2)transform.position + centerOffset + weights[i].dir * objectRadius, weights[i].dir, obstacleDistanceSensitivity, obstacleMask);
            if (hitInfos.Length > 0)
            {
                RaycastHit2D hitInfo = hitInfos[hitInfos.Length - 1];

                //Find closest obstacle that isnt self
                for (int j = 0; j < hitInfos.Length; j++)
                {
                    if (hitInfos[j].collider != null && hitInfos[j].collider.gameObject.name != name && hitInfos[j].distance < hitInfo.distance)
                        hitInfo = hitInfos[j];
                }

                if (hitInfo.collider != null && hitInfo.collider.gameObject.name != name)
                {
                    float negativeWeight = 1 - Mathf.Clamp(hitInfo.distance / obstacleDistanceSensitivity, 0f, 1f);

                    if (isOrbiting && hitInfo.collider.gameObject.CompareTag(comradeTag))
                        negativeWeight = 1 - Mathf.Abs(negativeWeight - 0.65f);

                    weights[i].weight -= negativeWeight;

                    //Propogate the negative weight across nearby weights at reduced amount
                    weights[(i + 1) % numOfWeights].weight -= negativeWeight / 2;
                    weights[(i + 2) % numOfWeights].weight -= negativeWeight / 3;
                    weights[(i - 1) >= 0 ? i - 1 : i - 1 + numOfWeights].weight -= negativeWeight / 2;
                    weights[(i - 2) >= 0 ? i - 2 : i - 2 + numOfWeights].weight -= negativeWeight / 3;
                }
                else
                {
                    //Reward no obstacles in path
                    weights[i].weight += weights[i].weight * noObstacleDesire;
                }
            }

            weights[i].weight = Mathf.Clamp(weights[i].weight, -1f, 1f);
        }
    }

    private bool CheckForDirectPath(Vector2 dir, float dist)
    {
        return Physics2D.Raycast((Vector2)transform.position + centerOffset, dir, dist, wallMask).collider == null;
    }

    private Vector2 IndirectMoveTowardsTarget(Vector2 dirToTarget, float distToTarget)
    {
        for (int i = 0; i < numOfWeights; i++)
        {
            longRange_Weights[i].weight = 0;
        }

        RaycastHit2D[] hitInfos = new RaycastHit2D[numOfWeights];

        for (int i = 0; i < numOfWeights; i++)
        {
            RaycastHit2D hitInfo = Physics2D.Raycast((Vector2)transform.position + centerOffset + longRange_Weights[i].dir * objectRadius, longRange_Weights[i].dir, 9999f, wallMask);
            hitInfos[i] = hitInfo;

            float w = Vector2.Dot(longRange_Weights[i].dir, dirToTarget);

            float shapedw = 1 - Mathf.Abs(w);

            if (hitInfo.collider != null && hitInfo.distance <= distToTarget)
            {
                float distToWall = hitInfo.distance;
                float distFromWallToTarget = distToWall * w;

                float negativeWeight = (distToTarget - distFromWallToTarget) * -1;

                longRange_Weights[i].weight += negativeWeight;
                longRange_Weights[(i + 1) % numOfWeights].weight += negativeWeight / 2f;
                longRange_Weights[(i + 2) % numOfWeights].weight += negativeWeight / 3;
                longRange_Weights[(i - 1) >= 0 ? i - 1 : i - 1 + numOfWeights].weight += negativeWeight / 2f;
                longRange_Weights[(i - 2) >= 0 ? i - 2 : i - 2 + numOfWeights].weight += negativeWeight / 3f;
            }
            else
            {
                float distToWall = distToTarget;
                float distFromWallToTarget = distToWall * w;

                longRange_Weights[i].weight += distToTarget * 2 - (distToTarget - distFromWallToTarget);
            }

            if(shapedw > 0.2f || w < -0.2f)
            {
                Vector2 newRayOrigin = (Vector2)transform.position + centerOffset + longRange_Weights[i].dir * (objectRadius + hitInfo.distance);
                Vector2 newVectorToTarget = (Vector2)target.position - newRayOrigin;
                float newDistToTarget = newVectorToTarget.magnitude;
                Vector2 newDirToTarget = newVectorToTarget.normalized;

                RaycastHit2D newHitInfo = Physics2D.Raycast(newRayOrigin, newDirToTarget, newDistToTarget, wallMask);
                if(newHitInfo.collider != null)
                {

                } else
                {
                    longRange_Weights[i].weight += (distToTarget * 2) - newDistToTarget;// * Mathf.Max(0.2f, shapedw);
                }
            }
        }

        float largestWeight = longRange_Weights[0].weight;
        int largestWeightIndex = 0;
        float secondLargestWeight = longRange_Weights[1].weight;
        int secondLargestWeightIndex = 1;
        Vector2 newDir = longRange_Weights[0].dir;
        for (int i = 1; i < numOfWeights; i++)
        {
            if (longRange_Weights[i].weight > largestWeight)
            {
                secondLargestWeight = largestWeight;
                secondLargestWeightIndex = largestWeightIndex;
                largestWeight = longRange_Weights[i].weight;
                largestWeightIndex = i;
            }
            else if (longRange_Weights[i].weight > secondLargestWeight)
            {
                secondLargestWeight = longRange_Weights[i].weight;
                secondLargestWeightIndex = i;
            }
        }
        newDir = longRange_Weights[largestWeightIndex].dir;

        if(Mathf.Abs(largestWeight - secondLargestWeight) < biasThreshold)
        {
            newDir = longRange_Weights[secondLargestWeightIndex].dir;
        }

        return newDir;
    }

    Coroutine pauseMovement;
    public void PauseMovement(float length)
    {
        if (pauseMovement != null)
            StopCoroutine(pauseMovement);

        pauseMovement = StartCoroutine(PauseMovementSequence(length));
    }
    IEnumerator PauseMovementSequence(float length)
    {
        canMove = false;
        yield return new WaitForSeconds(length);
        canMove = true;
    }

    //Visualize direction weights for debugging as well as orbit and target ranges
    private void OnDrawGizmos()
    {
        if (!visualizeWeights) return;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere((Vector2)transform.position + centerOffset, objectRadius);

        if (weights == null) return;

        for (int i = 0; i < numOfWeights; i++)
        {
            if(weights[i].weight >= 0)
            {
                Gizmos.color = Color.green;
            } else
            {
                Gizmos.color = Color.red;
            }
            Gizmos.DrawLine((Vector2)transform.position + centerOffset +  weights[i].dir * objectRadius, (Vector2)transform.position + weights[i].dir * objectRadius + weights[i].dir * Mathf.Abs(weights[i].weight));
        }

        if (longRange_Weights == null) return;
        for (int i = 0; i < numOfWeights; i++)
        {
            if (longRange_Weights[i].weight >= 0)
            {
                Gizmos.color = Color.cyan;
            }
            else
            {
                Gizmos.color = Color.magenta;
            }
            Gizmos.DrawLine((Vector2)transform.position + centerOffset + longRange_Weights[i].dir * objectRadius, (Vector2)transform.position + longRange_Weights[i].dir * objectRadius + longRange_Weights[i].dir * Mathf.Abs(longRange_Weights[i].weight));
        }
    }
}
