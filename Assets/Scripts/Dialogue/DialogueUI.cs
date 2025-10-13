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

    public DialogueChoice selectedChoice { get; private set; }

    public bool nextPressed; // Set to public so DialogueManager can reset it to false at the end of a dialogue.

    [Header("Typewriter Settings")]
    [Tooltip("Characters per second. 0 = instant.")]
    public float typewriterSpeed = 30f;

    private Coroutine typewriterCoroutine;
    private bool skipTypewriter;

    private void Awake()
    {
        Hide();
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
        dialogueText.text = "";
        choicesPanel.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(true);
        nextPressed = false;
        skipTypewriter = false;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }
        typewriterCoroutine = StartCoroutine(TypeText(text));
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
        if (typewriterSpeed <= 0f)
        {
            dialogueText.text = text;
            yield break;
        }

        dialogueText.text = "";
        for (int i = 0; i < text.Length; i++)
        {
            if (skipTypewriter)
            {
                dialogueText.text = text;
                yield break;
            }
            dialogueText.text += text[i];
            yield return new WaitForSeconds(1f / typewriterSpeed);
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
        speakerText.text = "";
        dialogueText.text = "";
        gameObject.SetActive(false);
    }
}