using UnityEngine;

// A falling piece. Shape[x, y]: x = column (left to right), y = row from the TOP of its box.
// Rotation follows SRS (the Tetris Guideline "Super Rotation System"): 0 = spawn, 1 = R (clockwise),
// 2 = upside down, 3 = L (counter-clockwise). Rotating is a plain matrix turn inside the piece's box.
public class Tetromino
{
    public int[,] Shape { get; private set; }
    public Vector2Int Position { get; set; }
    public int Rotation { get; private set; }

    public Tetromino(int[,] shapeMatrix, Vector2Int startPos, int rotation = 0)
    {
        Shape = (int[,])shapeMatrix.Clone(); // Clone it so rotations don't ruin the original!
        Position = startPos;
        Rotation = rotation;
    }

    public void SetState(int[,] shape, int rotation)
    {
        Shape = shape;
        Rotation = ((rotation % 4) + 4) % 4;
    }

    // Kept for the AI planner: one clockwise turn
    public void Rotate() => SetState(RotatedClockwise(Shape), Rotation + 1);

    // Clockwise on screen: (x, y-from-top) -> (size-1-y, x)
    public static int[,] RotatedClockwise(int[,] shape)
    {
        int size = shape.GetLength(0);
        int[,] result = new int[size, size];
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                result[size - 1 - y, x] = shape[x, y];
        return result;
    }

    public static int[,] RotatedCounterClockwise(int[,] shape)
    {
        int size = shape.GetLength(0);
        int[,] result = new int[size, size];
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                result[y, size - 1 - x] = shape[x, y];
        return result;
    }
}

public static class TetrominoShapes
{
    // Written as rows (top to bottom) in the SRS spawn orientation
    public static readonly int[,] T_Shape = FromRows(".X.", "XXX", "...");
    public static readonly int[,] O_Shape = FromRows("XX", "XX");
    public static readonly int[,] I_Shape = FromRows("....", "XXXX", "....", "....");
    public static readonly int[,] S_Shape = FromRows(".XX", "XX.", "...");
    public static readonly int[,] Z_Shape = FromRows("XX.", ".XX", "...");
    public static readonly int[,] J_Shape = FromRows("X..", "XXX", "...");
    public static readonly int[,] L_Shape = FromRows("..X", "XXX", "...");

    // --- Special Skill Shapes ---
    public static readonly int[,] X_Shape = FromRows("X.X", ".X.", "X.X");

    // Piece IDs 1-7 index this array (1 = T ... 7 = L). X_Shape is left OUT so it never spawns randomly.
    public static int[][,] AllShapes = { T_Shape, O_Shape, I_Shape, S_Shape, Z_Shape, J_Shape, L_Shape };

    public const int T_ID = 1;
    public const int O_ID = 2;
    public const int I_ID = 3;

    private static int[,] FromRows(params string[] rows)
    {
        int size = rows.Length;
        int[,] shape = new int[size, size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                shape[x, y] = rows[y][x] == 'X' ? 1 : 0;
        return shape;
    }
}

// SRS wall kicks: offsets tried in order (x right, y UP) when turning from one rotation state to another
public static class SrsKicks
{
    private static readonly Vector2Int[][] Jlstz =
    {
        V((0,0), (-1,0), (-1, 1), (0,-2), (-1,-2)), // 0 -> R
        V((0,0), ( 1,0), ( 1,-1), (0, 2), ( 1, 2)), // R -> 0
        V((0,0), ( 1,0), ( 1,-1), (0, 2), ( 1, 2)), // R -> 2
        V((0,0), (-1,0), (-1, 1), (0,-2), (-1,-2)), // 2 -> R
        V((0,0), ( 1,0), ( 1, 1), (0,-2), ( 1,-2)), // 2 -> L
        V((0,0), (-1,0), (-1,-1), (0, 2), (-1, 2)), // L -> 2
        V((0,0), (-1,0), (-1,-1), (0, 2), (-1, 2)), // L -> 0
        V((0,0), ( 1,0), ( 1, 1), (0,-2), ( 1,-2)), // 0 -> L
    };

    private static readonly Vector2Int[][] I =
    {
        V((0,0), (-2,0), ( 1,0), (-2,-1), ( 1, 2)), // 0 -> R
        V((0,0), ( 2,0), (-1,0), ( 2, 1), (-1,-2)), // R -> 0
        V((0,0), (-1,0), ( 2,0), (-1, 2), ( 2,-1)), // R -> 2
        V((0,0), ( 1,0), (-2,0), ( 1,-2), (-2, 1)), // 2 -> R
        V((0,0), ( 2,0), (-1,0), ( 2, 1), (-1,-2)), // 2 -> L
        V((0,0), (-2,0), ( 1,0), (-2,-1), ( 1, 2)), // L -> 2
        V((0,0), ( 1,0), (-2,0), ( 1,-2), (-2, 1)), // L -> 0
        V((0,0), (-1,0), ( 2,0), (-1, 2), ( 2,-1)), // 0 -> L
    };

    // 180 turns aren't in classic SRS; this small set is what modern clients commonly use
    private static readonly Vector2Int[] Half = V((0,0), (0,1), (1,0), (-1,0), (0,-1));

    public static Vector2Int[] For(bool isIPiece, int from, int to)
    {
        from = ((from % 4) + 4) % 4;
        to = ((to % 4) + 4) % 4;
        if ((from + 2) % 4 == to) return Half;

        int index;
        if (from == 0 && to == 1) index = 0;
        else if (from == 1 && to == 0) index = 1;
        else if (from == 1 && to == 2) index = 2;
        else if (from == 2 && to == 1) index = 3;
        else if (from == 2 && to == 3) index = 4;
        else if (from == 3 && to == 2) index = 5;
        else if (from == 3 && to == 0) index = 6;
        else index = 7; // 0 -> L
        return (isIPiece ? I : Jlstz)[index];
    }

    private static Vector2Int[] V(params (int x, int y)[] offsets)
    {
        var result = new Vector2Int[offsets.Length];
        for (int i = 0; i < offsets.Length; i++) result[i] = new Vector2Int(offsets[i].x, offsets[i].y);
        return result;
    }
}
