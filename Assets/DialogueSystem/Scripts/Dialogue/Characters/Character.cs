using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Dialogue/Character")]
public class Character : ScriptableObject
{
    public string npcName;
    public Sprite defaultSprite;

    [System.Serializable]
    public class Expression
    {
        public string expressionName;
        public Sprite sprite;
    }

    public List<Expression> expressions = new List<Expression>();

    public Sprite GetSprite(string expression)
    {
        var found = expressions.Find(e => e.expressionName == expression);
        return found != null && found.sprite != null ? found.sprite : defaultSprite;
    }
}