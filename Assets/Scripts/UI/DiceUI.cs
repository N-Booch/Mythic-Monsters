using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DiceUI : MonoBehaviour
{
    public Image diceImage;
    public Sprite[] diceFaces; // 0 = face 1, 1 = face 2, etc.

    [Header("Board Presentation")]
    [SerializeField] private float hiddenAlpha = 0f;
    [SerializeField] private float readyAlpha = 0.72f;
    [SerializeField] private float rollingAlpha = 1f;
    [SerializeField] private float resultAlpha = 1f;
    [SerializeField] private float resultHoldSeconds = 0.85f;
    [SerializeField] private float pulseAmount = 0.045f;
    [SerializeField] private float pulseSpeed = 3.2f;
    [SerializeField] private float rollWobbleDegrees = 7f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 baseScale = Vector3.one;
    private Quaternion baseRotation = Quaternion.identity;
    private bool rollAvailable;
    private bool isRollingPresentation;
    private bool isShowingResult;
    private bool staysVisibleThisTurn;
    private Coroutine settleRoutine;

    private void Awake()
    {
        CacheReferences();
        SetAlpha(hiddenAlpha);
    }

    private void OnEnable()
    {
        CacheReferences();
        ApplyIdlePresentation();
    }

    private void Update()
    {
        if (rectTransform == null)
            return;

        if (isRollingPresentation)
        {
            float wobble = Mathf.Sin(Time.unscaledTime * 18f) * rollWobbleDegrees;
            rectTransform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, wobble);
            rectTransform.localScale = baseScale * (1.08f + Mathf.Sin(Time.unscaledTime * 14f) * 0.035f);
            return;
        }

        rectTransform.localRotation = baseRotation;

        if (rollAvailable && !isShowingResult)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            rectTransform.localScale = baseScale * pulse;
        }
    }

    /// <summary>
    /// Update the visual dice to match the current roll.
    /// </summary>
    public void ShowRoll(int roll)
    {
        if (roll < 1 || roll > 6)
        {
            Debug.LogError("Invalid dice roll: " + roll);
            return;
        }

        if (diceImage != null && diceFaces.Length == 6)
            diceImage.sprite = diceFaces[roll - 1];

        SetAlpha(isRollingPresentation ? rollingAlpha : resultAlpha);
    }

    public void SetRollAvailable(bool available)
    {
        rollAvailable = available;

        if (isRollingPresentation || isShowingResult)
            return;

        ApplyIdlePresentation();
    }

    public void BeginRollPresentation()
    {
        CacheReferences();
        StopSettleRoutine();

        staysVisibleThisTurn = true;
        isRollingPresentation = true;
        isShowingResult = false;
        SetAlpha(rollingAlpha);

        if (rectTransform != null)
            rectTransform.localScale = baseScale * 1.08f;
    }

    public void ShowFinalResult(int roll)
    {
        StopSettleRoutine();
        staysVisibleThisTurn = true;
        isRollingPresentation = false;
        isShowingResult = true;

        ShowRoll(roll);
        SetAlpha(resultAlpha);

        if (rectTransform != null)
        {
            rectTransform.localRotation = baseRotation;
            rectTransform.localScale = baseScale * 1.12f;
        }
    }

    public void CompleteRollPresentation()
    {
        StopSettleRoutine();
        settleRoutine = StartCoroutine(SettleAfterResult());
    }

    public void ResetTurnPresentation()
    {
        StopSettleRoutine();
        rollAvailable = false;
        staysVisibleThisTurn = false;
        isRollingPresentation = false;
        isShowingResult = false;
        ApplyIdlePresentation();
    }

    private IEnumerator SettleAfterResult()
    {
        yield return new WaitForSecondsRealtime(resultHoldSeconds);

        isShowingResult = false;
        ApplyIdlePresentation();
        settleRoutine = null;
    }

    private void ApplyIdlePresentation()
    {
        CacheReferences();

        isRollingPresentation = false;

        bool shouldShow = rollAvailable || staysVisibleThisTurn;
        SetAlpha(shouldShow ? readyAlpha : hiddenAlpha);

        if (rectTransform != null)
        {
            rectTransform.localRotation = baseRotation;
            rectTransform.localScale = shouldShow ? baseScale : baseScale * 0.92f;
        }
    }

    private void CacheReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                baseScale = rectTransform.localScale;
                baseRotation = rectTransform.localRotation;
            }
        }
    }

    private void SetAlpha(float alpha)
    {
        CacheReferences();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
            canvasGroup.blocksRaycasts = alpha > 0.01f;
            canvasGroup.interactable = alpha > 0.01f;
        }
        else if (diceImage != null)
        {
            Color color = diceImage.color;
            color.a = alpha;
            diceImage.color = color;
        }
    }

    private void StopSettleRoutine()
    {
        if (settleRoutine == null)
            return;

        StopCoroutine(settleRoutine);
        settleRoutine = null;
    }

}
