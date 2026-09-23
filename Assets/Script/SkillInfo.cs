// Names and effects of the three attack skills (tier 1-3), shared by the HUD and alerts
public static class SkillInfo
{
    public static string Name(int tier)
    {
        switch (tier)
        {
            case 1: return "BLOCK BREAKER";
            case 2: return "FORCED Z";
            case 3: return "X-BOMB";
            default: return "SKILL";
        }
    }

    // What the skill does to the player it hits
    public static string EffectOnYou(int tier)
    {
        switch (tier)
        {
            case 1: return "A block was knocked out of your stack";
            case 2: return "Your next piece is a Z";
            case 3: return "Your next piece is the X block";
            default: return "";
        }
    }

    // Tier of the skill that forces this piece (5 = Z, 99 = X)
    public static int TierForForcedPiece(int pieceID) => pieceID == 99 ? 3 : 2;
}
