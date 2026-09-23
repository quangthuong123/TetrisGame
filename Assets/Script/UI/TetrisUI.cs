using UnityEngine;
using TMPro; // You need this for TextMeshPro

public class TetrisUI : MonoBehaviour
{
    public TextMeshProUGUI spText;
    private TetrisEngine myBoard;

    void Update()
    {
        // Find the board that belongs to THIS local player
        if (myBoard == null)
        {
            TetrisEngine[] allBoards = FindObjectsOfType<TetrisEngine>();
            foreach (TetrisEngine board in allBoards)
            {
                if (board.Object != null && board.Object.HasInputAuthority)
                {
                    myBoard = board;
                    break;
                }
            }
        }

        // Update the text box with the Skill Points
        if (myBoard != null)
        {
            spText.text = "Skill Points: " + myBoard.SkillPoints;
        }
    }
}