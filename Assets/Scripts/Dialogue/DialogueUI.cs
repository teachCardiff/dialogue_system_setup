using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI for displaying dialogue.
/// Prefab with: SpeakerText, DialogueText, NextButton, ChoicesPanel with Button prefab.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] public TextMeshProUGUI dialogueText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Transform choicesPanel;
    [SerializeField] private Button choiceButtonPrefab;

    // Animation settings (kept from existing branch)
    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private DialogueUIAnimationType animationType = DialogueUIAnimationType.None;
    [SerializeField] private string showTrigger = "Show";
    [SerializeField] private string hideTrigger = "Hide";
    // Event fired when the hide animation finishes and UI is fully hidden
    public UnityEngine.Events.UnityEvent onHideComplete;

    // Optional sprite support (from Sprite-Support branch)
    [Header("Optional UI Elements")]
    [SerializeField] private Image speakerImage;
    [SerializeField] private Image listenerImage;

    public DialogueChoice selectedChoice { get; private set; }

    [HideInInspector] public bool nextPressed; // Set to public so DialogueManager can reset it to false at the end of a dialogue.

    [Header("Typewriter Settings")]
    [Tooltip("Characters per second. 0 = instant.")]
    public float typewriterSpeed = 30f;

    private Coroutine typewriterCoroutine;
    private bool skipTypewriter;
    private float typewriterAccumulator = 0f;

    // runtime-only: effect ranges computed from parsed markup
    private List<RichTextParser.ShakeRange> shakeRanges = new List<RichTextParser.ShakeRange>();
    private List<RichTextParser.WaveRange> waveRanges = new List<RichTextParser.WaveRange>();
    private List<RichTextParser.PulseRange> pulseRanges = new List<RichTextParser.PulseRange>();
    private List<RichTextParser.GradientRange> gradientRanges = new List<RichTextParser.GradientRange>();
    private Coroutine effectsCoroutine;
    private int currentRevealCount = 0;
    [Header("Gradient Animation")]
    [Tooltip("Global speed at which gradient ranges animate (cycles per second)")]
    [SerializeField] private float gradientSpeed = 0.2f;
    [Tooltip("If true, each character will offset the gradient slightly for a flowing effect")]
    [SerializeField] private bool gradientPerCharacterPhase = true;
    // UI ready flag so external callers can wait until the UI is initialized
    public bool IsReady { get; private set; } = false;

    private void Awake()
    {
        IsReady = false;
        OnHideAnimationComplete();
        nextButton.onClick.AddListener(() => nextPressed = true);
    }

    public void ShowDialogue(string speaker, string text, Sprite speakerSprite, Sprite listenerSprite = null)
    {
        // If UI is not active, enable and defer typewriter to next frame
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            StartCoroutine(ShowDialogueDeferred(speaker, text, speakerSprite, listenerSprite));
            return;
        }

        ShowDialogueInternal(speaker, text, speakerSprite, listenerSprite);
    }

    private IEnumerator ShowDialogueDeferred(string speaker, string text, Sprite speakerSprite, Sprite listenerSprite = null)
    {
        yield return null;
        ShowDialogueInternal(speaker, text, speakerSprite, listenerSprite);
    }

    private void ShowDialogueInternal(string speaker, string text, Sprite speakerSprite, Sprite listenerSprite = null)
    {
        //Debug.Log($"[DialogueUI] ShowDialogueInternal: speaker='{speaker}', text length={text?.Length ?? 0}");
        speakerText.text = speaker;

        // Clear TMP text until parsed text is set below
        dialogueText.text = string.Empty;

        // Sprite support (if provided)
        if (speakerSprite != null && speakerImage != null)
        {
            speakerImage.sprite = speakerSprite;
            speakerImage.gameObject.SetActive(true);
        }
        else
        {
            speakerImage?.gameObject?.SetActive(false);
        }

        if (listenerSprite != null && listenerImage != null)
        {
            listenerImage.sprite = listenerSprite;
            listenerImage.gameObject.SetActive(true);
        }
        else
        {
            listenerImage?.gameObject?.SetActive(false);
        }

        // Common UI setup
        choicesPanel.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(true);
        animator?.SetInteger("AnimationType", (int)animationType);
        animator?.SetTrigger(showTrigger);

        nextPressed = false;
        skipTypewriter = false;


    // Parse rich/custom tags (now returns multiple effect ranges)
    RichTextParser.Parse(text, out string parsedText, out shakeRanges, out waveRanges, out pulseRanges, out gradientRanges);
    // Log parsed ranges for debugging
    //Debug.Log($"[DialogueUI] Parsed ranges: shake={shakeRanges?.Count ?? 0}, wave={waveRanges?.Count ?? 0}, pulse={pulseRanges?.Count ?? 0}, gradient={gradientRanges?.Count ?? 0}");

        // Set parsed text and start with zero visible characters to avoid partial tag rendering or flash
        dialogueText.text = parsedText;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();
        currentRevealCount = 0;
        //Debug.Log($"[DialogueUI] ShowDialogueInternal: parsed text set, totalChars={dialogueText.textInfo.characterCount}");

    if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
    typewriterCoroutine = StartCoroutine(TypeText(parsedText));

        // start unified effects coroutine if any ranges found
        bool hasAnyEffects = (shakeRanges != null && shakeRanges.Count > 0) ||
                             (waveRanges != null && waveRanges.Count > 0) ||
                             (pulseRanges != null && pulseRanges.Count > 0) ||
                             (gradientRanges != null && gradientRanges.Count > 0);

        if (hasAnyEffects)
        {
            if (effectsCoroutine != null) StopCoroutine(effectsCoroutine);
            effectsCoroutine = StartCoroutine(AnimateTextEffectsRoutine());
        }
        else
        {
            if (effectsCoroutine != null) { StopCoroutine(effectsCoroutine); effectsCoroutine = null; ResetMesh(); }
        }

        // Mark UI as ready once initialization is complete
        IsReady = true;
    }

    public void ShowChoices(List<DialogueChoice> choices)
    {
        nextButton.gameObject.SetActive(false);
        choicesPanel.gameObject.SetActive(true);
        foreach (Transform child in choicesPanel) Destroy(child.gameObject);

        for (int i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            var btn = Instantiate(choiceButtonPrefab, choicesPanel);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = choice.choiceText;
            btn.onClick.AddListener(() => SelectChoice(choice));
        }
    }

    private IEnumerator TypeText(string text)
    {
        //Debug.Log($"[DialogueUI] TypeText start: totalChars={dialogueText.textInfo.characterCount}");
        // If instant
        if (typewriterSpeed <= 0f)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
            yield break;
        }

        // Reveal characters by advancing TMP's maxVisibleCharacters.
        dialogueText.ForceMeshUpdate();
        int total = dialogueText.textInfo.characterCount;
        int visible = dialogueText.maxVisibleCharacters;
        typewriterAccumulator = 0f;

        if (typewriterSpeed <= 0f)
        {
            dialogueText.maxVisibleCharacters = total;
            currentRevealCount = total;
            typewriterCoroutine = null;
            yield break;
        }

        while (visible < total)
        {
            if (skipTypewriter)
            {
                // reveal all immediately
                dialogueText.maxVisibleCharacters = total;
                visible = total;
                currentRevealCount = visible;
                break;
            }

            // accumulate fractional characters based on speed (chars/sec)
            typewriterAccumulator += typewriterSpeed * Time.deltaTime;
            int toAdd = Mathf.FloorToInt(typewriterAccumulator);
            if (toAdd > 0)
            {
                int nextVisible = Mathf.Min(total, visible + toAdd);
                dialogueText.maxVisibleCharacters = nextVisible;
                //Debug.Log($"[DialogueUI] TypeText reveal -> {visible}..{nextVisible}");
                visible = nextVisible;
                currentRevealCount = visible;
                typewriterAccumulator -= toAdd;
            }

            yield return null;
        }

        // ensure fully visible at end
        dialogueText.maxVisibleCharacters = total;
        currentRevealCount = total;
        //Debug.Log($"[DialogueUI] TypeText complete: currentRevealCount={currentRevealCount}");
        skipTypewriter = false;
        typewriterCoroutine = null;
    }

    private void RestoreMeshColorsForRange(int fromInclusive, int toExclusive)
    {
        // Removed: reveal is now driven by dialogueText.maxVisibleCharacters
        return;
    }

    public bool IsTextFullyRevealed()
    {
        if (dialogueText == null) return true;
        // rely on our reveal counter which tracks visible characters when using per-vertex alpha reveal
        dialogueText.ForceMeshUpdate();
        return currentRevealCount >= dialogueText.textInfo.characterCount;
    }

    private IEnumerator AnimateTextEffectsRoutine()
    {
    // Give TMP a couple frames to settle its internal mesh data before we start mutating it.
    yield return new WaitForEndOfFrame();
    yield return null;
        while (true)
        {
            // Give TMP one frame to generate text/mesh data after initialization/typewriter starts.
            dialogueText.ForceMeshUpdate();
            var ti = dialogueText.textInfo;
            int totalChars = ti.characterCount;
            // Use the explicit reveal counter (we manage per-character visibility via alpha)
            int visibleCount = Mathf.Clamp(currentRevealCount, 0, totalChars);
            if (totalChars == 0)
            {
                yield return null;
                continue;
            }
            // take a fresh copy of the base mesh vertices/colors for the current text
            var baseMeshInfo = ti.CopyMeshInfoVertexData();

            // reset verts/colors from base copy (use fresh meshInfo as base)
            for (int i = 0; i < ti.meshInfo.Length && i < baseMeshInfo.Length; i++)
            {
                var verts = ti.meshInfo[i].vertices;
                var src = baseMeshInfo[i].vertices;
                Array.Copy(src, verts, src.Length);

                var cols = ti.meshInfo[i].colors32;
                var srcCols = baseMeshInfo[i].colors32;
                int copyLen = Mathf.Min(srcCols.Length, cols.Length);
                Array.Copy(srcCols, cols, copyLen);
            }

            float time = Time.time;

            // Apply shake
            foreach (var range in shakeRanges)
            {
                int start = Math.Max(0, range.start);
                int end = Math.Min(visibleCount, range.start + Math.Max(0, range.length));
                if (start >= end)
                {
                    // Range is outside visible area or invalid
                    // Debug info for diagnosing parser vs visibleCount
                    if (range.length <= 0) Debug.LogWarning($"[DialogueUI] Shake empty range start={range.start} length={range.length} visible={visibleCount}");
                    continue;
                }
                for (int c = start; c < end; c++)
                {
                    try
                    {
                        if (c >= ti.characterCount) break;
                        var ch = ti.characterInfo[c];
                        if (!ch.isVisible) continue;
                        // don't apply positional effects to unrevealed characters
                        if (c >= currentRevealCount) continue;
                        int matIndex = ch.materialReferenceIndex;
                        int vertIndex = ch.vertexIndex;
                        if (matIndex >= ti.meshInfo.Length || vertIndex < 0 || vertIndex + 3 >= ti.meshInfo[matIndex].vertices.Length)
                        {
                            Debug.LogWarning($"[DialogueUI] Shake skip: meshInfo mismatch mat={matIndex} vert={vertIndex} for char={c}");
                            continue;
                        }
                        float seed = c * 0.13f;
                        float jitterX = (Mathf.PerlinNoise(time * 10f + seed, seed) - 0.5f) * 2f * 0.5f * range.intensity;
                        float jitterY = (Mathf.PerlinNoise(seed, time * 10f + seed) - 0.5f) * 2f * 0.5f * range.intensity;
                        Vector3 offset = new Vector3(jitterX, jitterY, 0f);
                        var verts = ti.meshInfo[matIndex].vertices;
                        verts[vertIndex + 0] += offset;
                        verts[vertIndex + 1] += offset;
                        verts[vertIndex + 2] += offset;
                        verts[vertIndex + 3] += offset;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        Debug.LogWarning($"[DialogueUI] Exception in Shake for char={c} start={start} end={end} totalChars={totalChars}");
                        continue;
                    }
                }
            }

            // Apply wave (vertical sine)
            foreach (var range in waveRanges)
            {
                int start = Math.Max(0, range.start);
                int end = Math.Min(visibleCount, range.start + Math.Max(0, range.length));
                if (start >= end)
                {
                    if (range.length <= 0) Debug.LogWarning($"[DialogueUI] Wave empty range start={range.start} length={range.length} visible={visibleCount}");
                    continue;
                }
                for (int c = start; c < end; c++)
                {
                    try
                    {
                        if (c >= ti.characterCount) break;
                        var ch = ti.characterInfo[c];
                        if (!ch.isVisible) continue;
                        if (c >= currentRevealCount) continue;
                        int matIndex = ch.materialReferenceIndex;
                        int vertIndex = ch.vertexIndex;
                        if (matIndex >= ti.meshInfo.Length || vertIndex < 0 || vertIndex + 3 >= ti.meshInfo[matIndex].vertices.Length)
                        {
                            Debug.LogWarning($"[DialogueUI] Wave skip: meshInfo mismatch mat={matIndex} vert={vertIndex} for char={c}");
                            continue;
                        }
                        float phase = c * 0.5f;
                        float y = Mathf.Sin(time * range.speed + phase) * range.amplitude;
                        Vector3 offset = new Vector3(0f, y, 0f);
                        var verts = ti.meshInfo[matIndex].vertices;
                        verts[vertIndex + 0] += offset;
                        verts[vertIndex + 1] += offset;
                        verts[vertIndex + 2] += offset;
                        verts[vertIndex + 3] += offset;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        Debug.LogWarning($"[DialogueUI] Exception in Wave for char={c} start={start} end={end} totalChars={totalChars}");
                        continue;
                    }
                }
            }

            // Apply pulse (per-character scale)
            foreach (var range in pulseRanges)
            {
                int start = Math.Max(0, range.start);
                int end = Math.Min(visibleCount, range.start + Math.Max(0, range.length));
                if (start >= end)
                {
                    if (range.length <= 0) Debug.LogWarning($"[DialogueUI] Pulse empty range start={range.start} length={range.length} visible={visibleCount}");
                    continue;
                }
                for (int c = start; c < end; c++)
                {
                    try
                    {
                        if (c >= ti.characterCount) break;
                        var ch = ti.characterInfo[c];
                        if (!ch.isVisible) continue;
                        if (c >= currentRevealCount) continue;
                        int matIndex = ch.materialReferenceIndex;
                        int vertIndex = ch.vertexIndex;
                        if (matIndex >= ti.meshInfo.Length || vertIndex < 0 || vertIndex + 3 >= ti.meshInfo[matIndex].vertices.Length)
                        {
                            Debug.LogWarning($"[DialogueUI] Pulse skip: meshInfo mismatch mat={matIndex} vert={vertIndex} for char={c}");
                            continue;
                        }
                        float scale = 1f + (Mathf.Sin(time * range.speed + c * 0.2f) * 0.5f + 0.5f) * (range.scale - 1f);
                        // compute mid point of character quad
                        var verts = ti.meshInfo[matIndex].vertices;
                        Vector3 mid = (verts[vertIndex + 0] + verts[vertIndex + 2]) * 0.5f;
                        verts[vertIndex + 0] = mid + (verts[vertIndex + 0] - mid) * scale;
                        verts[vertIndex + 1] = mid + (verts[vertIndex + 1] - mid) * scale;
                        verts[vertIndex + 2] = mid + (verts[vertIndex + 2] - mid) * scale;
                        verts[vertIndex + 3] = mid + (verts[vertIndex + 3] - mid) * scale;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        Debug.LogWarning($"[DialogueUI] Exception in Pulse for char={c} start={start} end={end} totalChars={totalChars}");
                        continue;
                    }
                }
            }

            // Apply gradient colors
            foreach (var range in gradientRanges)
            {
                int start = Math.Max(0, range.start);
                int end = Math.Min(visibleCount, range.start + Math.Max(0, range.length));
                if (start >= end)
                {
                    if (range.length <= 0) Debug.LogWarning($"[DialogueUI] Gradient empty range start={range.start} length={range.length} visible={visibleCount}");
                    continue;
                }
                for (int c = start; c < end; c++)
                {
                    try
                    {
                        if (c >= ti.characterCount) break;
                        var ch = ti.characterInfo[c];
                        if (!ch.isVisible) continue;
                        int matIndex = ch.materialReferenceIndex;
                        int vertIndex = ch.vertexIndex;
                        if (matIndex >= baseMeshInfo.Length || vertIndex < 0 || vertIndex + 3 >= baseMeshInfo[matIndex].colors32.Length)
                        {
                            Debug.LogWarning($"[DialogueUI] Gradient skip: meshInfo mismatch mat={matIndex} vert={vertIndex} for char={c}");
                            continue;
                        }
                        // base position along the gradient (0..1)
                        float baseT = (float)(c - start) / Mathf.Max(1, end - start - 1);
                        // time-driven offset cycles the gradient across the range
                        float timeOffset = (time * gradientSpeed) % 1f;
                        float charOffset = gradientPerCharacterPhase ? (c * 0.02f) % 1f : 0f;
                        float t = (baseT + timeOffset + charOffset) % 1f;
                        Color col = range.rainbow ? Color.HSVToRGB(t, 1f, 1f) : Color.Lerp(range.startColor, range.endColor, t);
                        var cols = ti.meshInfo[matIndex].colors32;
                        // preserve the base alpha from TMP's generated colors
                        byte a = 255;
                        var baseCols = baseMeshInfo[matIndex].colors32;
                        if (baseCols != null && vertIndex < baseCols.Length) a = baseCols[vertIndex].a;
                        if (c >= currentRevealCount) a = 0; // keep unrevealed characters invisible
                        Color32 c32 = new Color32(
                            (byte)(Mathf.Clamp01(col.r) * 255f),
                            (byte)(Mathf.Clamp01(col.g) * 255f),
                            (byte)(Mathf.Clamp01(col.b) * 255f),
                            a
                        );
                        cols[vertIndex + 0] = c32;
                        cols[vertIndex + 1] = c32;
                        cols[vertIndex + 2] = c32;
                        cols[vertIndex + 3] = c32;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        Debug.LogWarning($"[DialogueUI] Exception in Gradient for char={c} start={start} end={end} totalChars={totalChars}");
                        continue;
                    }
                }
            }

            // debug: log per-frame summary occasionally
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[DialogueUI] Effects frame: visible={visibleCount}, reveal={currentRevealCount}, totalChars={totalChars}");
            }

            // push changes to meshes
            for (int i = 0; i < ti.meshInfo.Length; i++)
            {
                var mesh = ti.meshInfo[i].mesh;
                mesh.vertices = ti.meshInfo[i].vertices;
                mesh.colors32 = ti.meshInfo[i].colors32;
                dialogueText.UpdateGeometry(mesh, i);
            }

            yield return null;
        }
    }

    private void ResetMesh()
    {
        // Force a fresh rebuild which will restore original geometry
        dialogueText.ForceMeshUpdate();
        var ti = dialogueText.textInfo;
        for (int i = 0; i < ti.meshInfo.Length; i++)
        {
            var mesh = ti.meshInfo[i].mesh;
            mesh.vertices = ti.meshInfo[i].vertices;
            dialogueText.UpdateGeometry(mesh, i);
        }
    }

    public void SkipTypewriter()
    {
        skipTypewriter = true;
    }

    public void ClearChoices()
    {
        selectedChoice = null;
    }

    private void SelectChoice(DialogueChoice choice)
    {
        selectedChoice = choice;
    }

    public bool IsNextPressed() => nextPressed;

    public void Hide()
    {
        nextPressed = false;
        if (animator == null)
        {
            // No animator: immediately perform hide complete logic
            OnHideAnimationComplete();
            onHideComplete?.Invoke();
            return;
        }

        animator.SetInteger("AnimationType", (int)animationType);
        animator.SetTrigger(hideTrigger);
        // Prefer using an Animation Event at the end of the hide clip to call OnHideAnimationComplete()
    }
    // Note: Use an Animation Event at the end of the hide animation clip to call OnHideAnimationComplete().

    public void OnHideAnimationComplete()
    {
        // stop coroutines and clear cached mesh/color data so next show starts fresh
        if (effectsCoroutine != null)
        {
            StopCoroutine(effectsCoroutine);
            effectsCoroutine = null;
        }

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        // restore mesh to a clean state
        ResetMesh();

    // clear ranges
        currentRevealCount = 0;
        shakeRanges?.Clear();
        waveRanges?.Clear();
        pulseRanges?.Clear();
        gradientRanges?.Clear();

        nextPressed = false;
        speakerText.text = "";
        dialogueText.text = "";
        //speakerImage.gameObject.SetActive(false);
        gameObject.SetActive(false);
        IsReady = false;
        onHideComplete?.Invoke();
    }
}

public enum DialogueUIAnimationType
{
    None = 0,
    SlideUp = 1,
    FadeInOut = 2
}