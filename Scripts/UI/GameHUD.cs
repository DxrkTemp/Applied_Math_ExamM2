using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    public static readonly Color SuccessColor = new Color(0.35f, 1f, 0.45f);
    public static readonly Color FailColor = new Color(1f, 0.3f, 0.3f);
    public static readonly Color InfoColor = new Color(0.85f, 0.85f, 1f);

    [Header("Leave empty to auto-create")]
    public TextMeshProUGUI positionText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI backstabText;
    public TextMeshProUGUI controlsText;
    public TMP_FontAsset font;

    [Header("References")]
    public PlayerController player;
    public MatrixPlatform platformToDisplay;
    public bool showPlatformMatrix = true;

    float messageTimer;
    float messageDuration;
    Vector3 messageBasePos;

    bool bsHasEnemy, bsBehind, bsFacing;
    float bsDist, bsBehindDot, bsFacingDot, bsRange;

    void Awake()
    {
        Instance = this;
        if (positionText == null || messageText == null) BuildHUD();
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (messageText != null)
        {
            messageText.text = "";
            messageBasePos = messageText.rectTransform.localPosition;
        }
        if (controlsText != null)
            controlsText.text = "<b>CONTROLS</b>\nWASD  Move\nSHIFT  Sprint\nSPACE  Jump\nF / LMB  Backstab\nMOUSE  Camera\nESC  Free cursor\nR  Restart level";
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        UpdatePositionText();
        UpdateBackstabText();
        UpdateMessage();
    }

    public static void Show(string message, Color color, float seconds)
    {
        if (Instance == null || Instance.messageText == null) { Debug.Log(message); return; }
        Instance.messageText.text = message;
        Instance.messageText.color = color;
        Instance.messageTimer = seconds;
        Instance.messageDuration = seconds;
    }

    public static void SetLevel(string text)
    {
        if (Instance != null && Instance.levelText != null) Instance.levelText.text = text;
    }

    public static void SetBackstabInfo(bool hasEnemy, float dist, float behindDot, float facingDot,
                                       bool behind, bool facing, float range)
    {
        if (Instance == null) return;
        Instance.bsHasEnemy = hasEnemy; Instance.bsDist = dist;
        Instance.bsBehindDot = behindDot; Instance.bsFacingDot = facingDot;
        Instance.bsBehind = behind; Instance.bsFacing = facing; Instance.bsRange = range;
    }

    void UpdatePositionText()
    {
        if (positionText == null || player == null) return;
        Vector3 c = player.CurrentPosition, n = player.NewPosition, v = player.Velocity;
        string s =
            "<b>PLAYER</b>\n" +
            "Current Pos: (" + c.x.ToString("F2") + ", " + c.y.ToString("F2") + ", " + c.z.ToString("F2") + ")\n" +
            "New Pos:     (" + n.x.ToString("F2") + ", " + n.y.ToString("F2") + ", " + n.z.ToString("F2") + ")\n" +
            "Velocity:    (" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")\n" +
            "Gravity:     " + player.gravity.ToString("F1") + " m/s²\n" +
            "Grounded:    " + (player.IsGrounded ? "<color=#6f6>YES</color>" : "<color=#f66>NO</color>");

        MatrixPlatform mp = player.CurrentPlatform != null ? player.CurrentPlatform : platformToDisplay;
        if (showPlatformMatrix && mp != null)
        {
            Matrix4x4 m = mp.CurrentMatrix;
            s += "\n\n<b>" + mp.name + "</b>  M = T·R·S" + (player.CurrentPlatform != null ? "  <color=#6cf>(riding)</color>" : "") + "\n" +
                 "<mspace=0.55em>" + Row(m, 0) + "\n" + Row(m, 1) + "\n" + Row(m, 2) + "\n" + Row(m, 3) + "</mspace>";
        }
        positionText.text = s;
    }

    static string Row(Matrix4x4 m, int r)
    {
        return "| " + m[r, 0].ToString("F2").PadLeft(6) + " " + m[r, 1].ToString("F2").PadLeft(6) + " " +
               m[r, 2].ToString("F2").PadLeft(6) + " " + m[r, 3].ToString("F2").PadLeft(7) + " |";
    }

    void UpdateBackstabText()
    {
        if (backstabText == null) return;
        if (!bsHasEnemy) { backstabText.text = "<b>GHOST</b>\nAll ghosts defeated"; return; }

        string inRange = bsDist <= bsRange ? "<color=#6f6>IN RANGE</color>" : "<color=#aaa>too far</color>";
        string behind = bsBehind ? "<color=#6f6>YES</color>" : "<color=#f66>NO</color>";
        string facing = bsFacing ? "<color=#6f6>YES</color>" : "<color=#f66>NO</color>";
        float angle = Mathf.Acos(Mathf.Clamp(bsBehindDot, -1f, 1f)) * Mathf.Rad2Deg;

        backstabText.text =
            "<b>GHOST</b>  distance " + bsDist.ToString("F1") + " m  " + inRange + "\n" +
            "enemyFwd · toPlayer = " + bsBehindDot.ToString("F2") + "  (" + angle.ToString("F0") + "°)  Behind: " + behind + "\n" +
            "playerFwd · toEnemy = " + bsFacingDot.ToString("F2") + "  Facing: " + facing;
    }

    void UpdateMessage()
    {
        if (messageText == null) return;
        if (messageTimer <= 0f) { if (messageText.text.Length > 0) messageText.text = ""; return; }

        messageTimer -= Time.deltaTime;
        float age = messageDuration - messageTimer;

        float s = age < 0.15f ? Mathf.Lerp(1.6f, 1f, age / 0.15f) : 1f;
        Matrix4x4 S = MatrixMath.Scale(new Vector3(s, s, 1f));
        Matrix4x4 T = MatrixMath.Translation(messageBasePos);
        MatrixMath.ApplyToLocal(messageText.rectTransform, T * S);

        Color c = messageText.color;
        c.a = Mathf.Clamp01(messageTimer / 0.4f);
        messageText.color = c;
    }

    void BuildHUD()
    {
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject go = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (positionText == null)
            positionText = MakeText(canvas.transform, "PositionText", new Vector2(0, 1), new Vector2(30, -30), new Vector2(700, 500), 24, TextAlignmentOptions.TopLeft);
        if (levelText == null)
            levelText = MakeText(canvas.transform, "LevelText", new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1000, 120), 34, TextAlignmentOptions.Top);
        if (messageText == null)
            messageText = MakeText(canvas.transform, "MessageText", new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 220), 64, TextAlignmentOptions.Center);
        if (backstabText == null)
            backstabText = MakeText(canvas.transform, "BackstabText", new Vector2(0, 0), new Vector2(30, 30), new Vector2(900, 140), 24, TextAlignmentOptions.BottomLeft);
        if (controlsText == null)
            controlsText = MakeText(canvas.transform, "ControlsText", new Vector2(1, 0), new Vector2(-30, 30), new Vector2(320, 260), 20, TextAlignmentOptions.BottomRight);

        messageText.fontStyle = FontStyles.Bold;
        messageText.outlineWidth = 0.2f;
        messageText.outlineColor = Color.black;
    }

    TextMeshProUGUI MakeText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = InfoColor;
        t.richText = true;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }
}
