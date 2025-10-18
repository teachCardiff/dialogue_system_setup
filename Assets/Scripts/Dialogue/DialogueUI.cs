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
    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private DialogueUIAnimationType animationType = DialogueUIAnimationType.None;
    [SerializeField] private string showTrigger = "Show";
    [SerializeField] private string hideTrigger = "Hide";
    // Event fired when the hide animation finishes and UI is fully hidden
    public UnityEngine.Events.UnityEvent onHideComplete;

    public DialogueChoice selectedChoice { get; private set; }

    public bool nextPressed; // Set to public so DialogueManager can reset it to false at the end of a dialogue.

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
    // UI ready flag so external callers can wait until the UI is initialized
    public bool IsReady { get; private set; } = false;

    private void Awake()
    {
        IsReady = false;
        OnHideAnimationComplete();
        nextButton.onClick.AddListener(() => nextPressed = true);
    }

    public void ShowDialogue(string speaker, string text)
    {
        // If UI is not active, enable and defer typewriter to next frame
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            StartCoroutine(ShowDialogueDeferred(speaker, text));
            return;
        }

        ShowDialogueInternal(speaker, text);
    }

    private IEnumerator ShowDialogueDeferred(string speaker, string text)
    {
        yield return null;
        ShowDialogueInternal(speaker, text);
    }

    private void ShowDialogueInternal(string speaker, string text)
    {
        speakerText.text = speaker;
        // We'll set the parsed text below and reveal characters with TMP's maxVisibleCharacters
        choicesPanel.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(true);
        animator?.SetInteger("AnimationType", (int)animationType);
        animator?.SetTrigger(showTrigger);
        nextPressed = false;
        skipTypewriter = false;


    // Parse rich/custom tags (now returns multiple effect ranges)
    RichTextParser.Parse(text, out string parsedText, out shakeRanges, out waveRanges, out pulseRanges, out gradientRanges);

        // Set full parsed text immediately to avoid partial tag rendering, then reveal chars via maxVisibleCharacters
        dialogueText.text = parsedText;
        dialogueText.ForceMeshUpdate();
        dialogueText.maxVisibleCharacters = 0;

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
        // If instant
        if (typewriterSpeed <= 0f)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
            yield break;
        }

        // Ensure text info is up-to-date
        dialogueText.ForceMeshUpdate();
        int total = dialogueText.textInfo.characterCount;
        int visible = 0;
        typewriterAccumulator = 0f;

        while (visible < total)
        {
            if (skipTypewriter)
            {
                dialogueText.maxVisibleCharacters = total;
                break;
            }

            // accumulate fractional characters based on speed (chars/sec)
            typewriterAccumulator += typewriterSpeed * Time.deltaTime;
            int toAdd = Mathf.FloorToInt(typewriterAccumulator);
            if (toAdd > 0)
            {
                visible = Mathf.Min(total, visible + toAdd);
                dialogueText.maxVisibleCharacters = visible;
                typewriterAccumulator -= toAdd;
            }

            yield return null;
        }

        // ensure fully visible at end
        dialogueText.maxVisibleCharacters = total;
        skipTypewriter = false;
        typewriterCoroutine = null;
    }

    public bool IsTextFullyRevealed()
    {
        if (dialogueText == null) return true;
        dialogueText.ForceMeshUpdate();
        return dialogueText.maxVisibleCharacters >= dialogueText.textInfo.characterCount;
    }

    private IEnumerator AnimateTextEffectsRoutine()
    {
        while (true)
        {
            dialogueText.ForceMeshUpdate();
            var ti = dialogueText.textInfo;
            if (ti.characterCount == 0)
            {
                yield return null;
                continue;
            }

            // take a fresh copy of the base mesh vertices/colors for the current text
            var baseMeshInfo = ti.CopyMeshInfoVertexData();

            // reset verts/colors from base copy
            for (int i = 0; i < ti.meshInfo.Length && i < baseMeshInfo.Length; i++)
            {
                var verts = ti.meshInfo[i].vertices;
                var src = baseMeshInfo[i].vertices;
                Array.Copy(src, verts, src.Length);

                var cols = ti.meshInfo[i].colors32;
                var srcCols = baseMeshInfo[i].colors32;
                Array.Copy(srcCols, cols, srcCols.Length);
            }

            float time = Time.time;

            // Apply shake
            foreach (var range in shakeRanges)
            {
                int start = Mathf.Clamp(range.start, 0, ti.characterCount - 1);
                int end = Mathf.Clamp(start + range.length, 0, ti.characterCount);
                for (int c = start; c < end; c++)
                {
                    var ch = ti.characterInfo[c];
                    if (!ch.isVisible) continue;
                    int matIndex = ch.materialReferenceIndex;
                    int vertIndex = ch.vertexIndex;
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
            }

            // Apply wave (vertical sine)
            foreach (var range in waveRanges)
            {
                int start = Mathf.Clamp(range.start, 0, ti.characterCount - 1);
                int end = Mathf.Clamp(start + range.length, 0, ti.characterCount);
                for (int c = start; c < end; c++)
                {
                    var ch = ti.characterInfo[c];
                    if (!ch.isVisible) continue;
                    int matIndex = ch.materialReferenceIndex;
                    int vertIndex = ch.vertexIndex;
                    float phase = c * 0.5f;
                    float y = Mathf.Sin(time * range.speed + phase) * range.amplitude;
                    Vector3 offset = new Vector3(0f, y, 0f);
                    var verts = ti.meshInfo[matIndex].vertices;
                    verts[vertIndex + 0] += offset;
                    verts[vertIndex + 1] += offset;
                    verts[vertIndex + 2] += offset;
                    verts[vertIndex + 3] += offset;
                }
            }

            // Apply pulse (per-character scale)
            foreach (var range in pulseRanges)
            {
                int start = Mathf.Clamp(range.start, 0, ti.characterCount - 1);
                int end = Mathf.Clamp(start + range.length, 0, ti.characterCount);
                for (int c = start; c < end; c++)
                {
                    var ch = ti.characterInfo[c];
                    if (!ch.isVisible) continue;
                    int matIndex = ch.materialReferenceIndex;
                    int vertIndex = ch.vertexIndex;
                    float scale = 1f + (Mathf.Sin(time * range.speed + c * 0.2f) * 0.5f + 0.5f) * (range.scale - 1f);
                    // compute mid point of character quad
                    var verts = ti.meshInfo[matIndex].vertices;
                    Vector3 mid = (verts[vertIndex + 0] + verts[vertIndex + 2]) * 0.5f;
                    verts[vertIndex + 0] = mid + (verts[vertIndex + 0] - mid) * scale;
                    verts[vertIndex + 1] = mid + (verts[vertIndex + 1] - mid) * scale;
                    verts[vertIndex + 2] = mid + (verts[vertIndex + 2] - mid) * scale;
                    verts[vertIndex + 3] = mid + (verts[vertIndex + 3] - mid) * scale;
                }
            }

            // Apply gradient colors
            foreach (var range in gradientRanges)
            {
                int start = Mathf.Clamp(range.start, 0, ti.characterCount - 1);
                int end = Mathf.Clamp(start + range.length, 0, ti.characterCount);
                for (int c = start; c < end; c++)
                {
                    var ch = ti.characterInfo[c];
                    if (!ch.isVisible) continue;
                    int matIndex = ch.materialReferenceIndex;
                    int vertIndex = ch.vertexIndex;
                    float t = (float)(c - start) / Mathf.Max(1, end - start - 1);
                    Color col = range.rainbow ? Color.HSVToRGB(t, 1f, 1f) : Color.Lerp(range.startColor, range.endColor, t);
                    var cols = ti.meshInfo[matIndex].colors32;
                    Color32 c32 = (Color32)col;
                    cols[vertIndex + 0] = c32;
                    cols[vertIndex + 1] = c32;
                    cols[vertIndex + 2] = c32;
                    cols[vertIndex + 3] = c32;
                }
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
        if (effectsCoroutine != null)
        {
            StopCoroutine(effectsCoroutine);
            effectsCoroutine = null; ResetMesh();
        }

        nextPressed = false;
        speakerText.text = "";
        dialogueText.text = "";
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