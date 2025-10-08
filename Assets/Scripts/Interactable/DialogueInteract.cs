using UnityEngine;

public class DialogueInteract : MonoBehaviour
{
    public Dialogue dialogueAsset;

    void Start()
    {
        
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            DialogueManager.Instance.StartDialogue(dialogueAsset, true);
        }
    }

    
}
