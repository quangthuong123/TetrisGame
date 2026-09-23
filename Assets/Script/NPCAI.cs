using UnityEngine;
using System.Collections;

public class NPCAI : MonoBehaviour
{
    [Header("AI Settings")]
    public float initialAttackInterval = 15f;
    public float minimumAttackInterval = 5f;
    public float difficultyIncrease = 0.5f;

    private float currentInterval;

    void Start()
    {
        currentInterval = initialAttackInterval;
        StartCoroutine(AttackLoop());
    }

    IEnumerator AttackLoop()
    {
        // Wait a few seconds for the board to spawn and the match to start
        yield return new WaitForSeconds(5f);

        while (true)
        {
            // Wait for the timer
            yield return new WaitForSeconds(currentInterval);

            // Find the player's board
            TetrisEngine playerBoard = Object.FindAnyObjectByType<TetrisEngine>();

            if (playerBoard != null && !playerBoard.IsGameOver)
            {
                // Pick a random attack (1 = Delete Block, 2 = Z-Piece, 3 = X-Piece)
                int randomAttack = Random.Range(1, 4);

                if (randomAttack == 1) playerBoard.ReceiveBlockDelete();
                else if (randomAttack == 2) playerBoard.ReceiveForcedPiece(5);
                else if (randomAttack == 3) playerBoard.ReceiveForcedPiece(99);

                Debug.Log("NPC attacked with tier: " + randomAttack);

                // Make the NPC attack faster next time (Survival Mode!)
                if (currentInterval > minimumAttackInterval)
                {
                    currentInterval -= difficultyIncrease;
                }
            }
        }
    }
}