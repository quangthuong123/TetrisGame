using Fusion;
using UnityEngine;
using TMPro;

public class BoardUI : NetworkBehaviour
{
    [Header("References")]
    public TetrisEngine engine;
    public GameObject uiCanvasObject;

    [Header("Skill Icons & SP")]
    public TextMeshProUGUI spText;
    [Tooltip("Visual SP bar. Create one with Tools > Tetris > Create SP Bar")]
    public SkillPointBar spBar;

    private int _lastCountdown = -1;

    // Explicitly using UnityEngine.UI.Image to prevent ambiguity!
    public UnityEngine.UI.Image skill1Icon;
    public UnityEngine.UI.Image skill2Icon;
    public UnityEngine.UI.Image skill3Icon;

    [Header("Incoming Attack Warning")]
    public GameObject warningPanel;
    public TextMeshProUGUI warningText;
    [Tooltip("Fading bubble for the warnings. Set by Tools > Tetris > Restyle Board Alerts")]
    public WarningToast warningToast;

    [Header("Skill Alert (opponent's skill hits you)")]
    [Tooltip("Icons for skills 1-3, shown in the alert. Set by Tools > Tetris > Setup Skill Icons")]
    public Sprite[] skillIcons = new Sprite[3];
    public SkillAlert skillAlert;
    private int _seenSkillHits = -1;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;

    [Header("Colors")]
    public Color affordableColor = Color.white;
    public Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

    public override void Spawned()
    {
        if (engine == null) engine = GetComponentInParent<TetrisEngine>();
        if (uiCanvasObject == null) uiCanvasObject = gameObject;

        uiCanvasObject.SetActive(HasInputAuthority);
    }

    public override void Render()
    {
        if (engine == null) engine = GetComponentInParent<TetrisEngine>();
        if (!HasInputAuthority || engine == null) return;

        // 1. Update the SP Number
        if (spText != null) spText.text = "SP: " + engine.SkillPoints;
        if (spBar != null) spBar.SetSkillPoints(engine.SkillPoints);

        // 2. Light up the Skill Icons based on the 3 Tiers
        if (skill1Icon != null) skill1Icon.color = engine.SkillPoints >= TetrisEngine.SkillCost(1) ? affordableColor : lockedColor;
        if (skill2Icon != null) skill2Icon.color = engine.SkillPoints >= TetrisEngine.SkillCost(2) ? affordableColor : lockedColor;
        if (skill3Icon != null) skill3Icon.color = engine.SkillPoints >= TetrisEngine.SkillCost(3) ? affordableColor : lockedColor;

        // 2b. Pop up the skill the opponent just hit us with
        if (_seenSkillHits < 0) _seenSkillHits = engine.SkillHitCount;
        else if (engine.SkillHitCount != _seenSkillHits)
        {
            _seenSkillHits = engine.SkillHitCount;
            ShowSkillAlert(engine.LastSkillHit);
        }

        // 3. Show a warning if an attack is queued up!
        int countdown = Mathf.CeilToInt(engine.StartCountdown);
        if (countdown != _lastCountdown)
        {
            if (countdown > 0) AudioManager.Play(Sfx.CountdownTick);
            else if (_lastCountdown > 0) AudioManager.Play(Sfx.CountdownGo);
            _lastCountdown = countdown;
        }

        string warning = null;
        if (countdown > 0)
        {
            warning = "GET READY... " + countdown;
        }
        else if (engine.ForcedNextPiece > 0)
        {
            int tier = SkillInfo.TierForForcedPiece(engine.ForcedNextPiece);
            warning = (tier == 3 ? "DANGER: " : "WARNING: ") + SkillInfo.Name(tier) + " INCOMING!";
        }
        else if (engine.PendingGarbage > 0)
        {
            warning = $"INCOMING: {engine.PendingGarbage} GARBAGE LINE{(engine.PendingGarbage > 1 ? "S" : "")}!";
        }

        if (warningToast != null)
        {
            // Pops in when the warning changes, fades after 2 seconds
            warningToast.SetMessage(warning);
        }
        else if (warningPanel != null)
        {
            warningPanel.SetActive(warning != null);
            if (warning != null && warningText != null) warningText.text = warning;
        }

        // 4. Toggle the Game Over Panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(engine.IsGameOver);
        }
    }

    void ShowSkillAlert(int tier)
    {
        if (skillAlert == null || tier < 1 || tier > 3) return;
        Sprite icon = skillIcons != null && skillIcons.Length >= tier ? skillIcons[tier - 1] : null;
        skillAlert.Show(icon, "OPPONENT USED " + SkillInfo.Name(tier) + "!", SkillInfo.EffectOnYou(tier));
    }
}