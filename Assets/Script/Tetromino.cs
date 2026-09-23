using UnityEngine;

public class Tetromino
{
    public int[,] Shape { get; private set; }
    public Vector2Int Position { get; set; }

    public Tetromino(int[,] shapeMatrix, Vector2Int startPos)
    {
        Shape = (int[,])shapeMatrix.Clone(); // Clone it so rotations don't ruin the original!
        Position = startPos;
    }

    // A simple 90-degree clockwise rotation for a square matrix
    public void Rotate()
    {
        int size = Shape.GetLength(0);
        int[,] newShape = new int[size, size];

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                newShape[j, size - 1 - i] = Shape[i, j];
            }
        }
        Shape = newShape;
    }
}

public static class TetrominoShapes
{
    public static readonly int[,] T_Shape = { { 0, 1, 0 }, { 1, 1, 1 }, { 0, 0, 0 } };
    public static readonly int[,] O_Shape = { { 1, 1 }, { 1, 1 } };
    public static readonly int[,] I_Shape = { { 0, 0, 0, 0 }, { 1, 1, 1, 1 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } };
    public static readonly int[,] S_Shape = { { 0, 1, 1 }, { 1, 1, 0 }, { 0, 0, 0 } };
    public static readonly int[,] Z_Shape = { { 1, 1, 0 }, { 0, 1, 1 }, { 0, 0, 0 } };
    public static readonly int[,] J_Shape = { { 1, 0, 0 }, { 1, 1, 1 }, { 0, 0, 0 } };
    public static readonly int[,] L_Shape = { { 0, 0, 1 }, { 1, 1, 1 }, { 0, 0, 0 } };

    // --- NEW: Special Skill Shapes ---
    public static readonly int[,] X_Shape = {
        { 1, 0, 1 },
        { 0, 1, 0 },
        { 1, 0, 1 }
    };

    // Notice we left X_Shape OUT of this array so it doesn't spawn randomly!
    public static int[][,] AllShapes = { T_Shape, O_Shape, I_Shape, S_Shape, Z_Shape, J_Shape, L_Shape };
}