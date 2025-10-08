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
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Transform choicesPanel;
    [SerializeField] private Button choiceButtonPrefab;

    public DialogueChoice selectedChoice { get; private set; }

    private bool nextPressed;

    private void Awake()
    {
        Hide();
        nextButton.onClick.AddListener(() => nextPressed = true);
    }

    public void ShowDialogue(string speaker, string text)
    {
        
        gameObject.SetActive(true);
        speakerText.text = speaker;
        dialogueText.text = text;
        choicesPanel.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(true);
        nextPressed = false;
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
        gameObject.SetActive(false);
    }
}