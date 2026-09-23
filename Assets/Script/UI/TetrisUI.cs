using UnityEngine;
using UnityEngine.Serialization;
using TMPro; // You need this for TextMeshPro

// Drives the scene's CurrentScore text with the local player's score
public class TetrisUI : MonoBehaviour
{
    [FormerlySerializedAs("spText")] // Keeps the existing scene reference
    public TextMeshProUGUI scoreText;
    private TetrisEngine myBoard;

    void Update()
    {
        // Find the board that belongs to THIS local player
        if (myBoard == null)
        {
            TetrisEngine[] allBoards = FindObjectsByType<TetrisEngine>(FindObjectsSortMode.None);
            foreach (TetrisEngine board in allBoards)
            {
                if (board.Object != null && board.Object.HasInputAuthority)
                {
                    myBoard = board;
                    break;
                }
            }
        }

        // Update the text box with the score
        if (myBoard != null && myBoard.Object != null && scoreText != null)
        {
            scoreText.text = "Score: " + myBoard.Score;
        }
    }
}
