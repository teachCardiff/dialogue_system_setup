// filepath: Assets/DialogueSystem/Scripts/Dialogue/Variables/QuestVariables.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum QuestStatus { NotStarted, InProgress, Completed }

[Serializable]
public class ObjectiveVariable : Variable
{
    [SerializeField] private string display;
    public string Display => string.IsNullOrEmpty(display) ? DisplayName : display;

    // New: objective display name variable
    public StringVar name = new StringVar { Key = "name", DisplayName = "Obj Name", value = "Objective" };

    public IntVar target = new IntVar { Key = "target", DisplayName = "Target", value = 1 };
    public IntVar progress = new IntVar { Key = "progress", DisplayName = "Progress", value = 0 };

    public bool Completed => progress.value >= target.value;

    public override Type ValueType => null; // group-like leaf with children
    public override object GetBoxed() => null;
    public override void SetBoxed(object value) { }

    public override IEnumerable<Variable> GetChildren()
    {
        if (name != null) yield return name;
        if (target != null) yield return target;
        if (progress != null) yield return progress;
    }
}

[Serializable]
public class QuestVariable : Variable
{
    public StringVar name = new StringVar { Key = "name", DisplayName = "Name" };
    public QuestVariable.EnumQuestStatus status = new QuestVariable.EnumQuestStatus();
    [SerializeReference] public List<ObjectiveVariable> objectives = new List<ObjectiveVariable>();

    public override Type ValueType => null;
    public override object GetBoxed() => null;
    public override void SetBoxed(object value) { }
    public override IEnumerable<Variable> GetChildren()
    {
        if (name != null) yield return name;
        if (status != null) yield return status;
        foreach (var obj in objectives)
        {
            if (obj == null) continue;
            yield return obj;
        }
    }

    public void EnsureOneObjective()
    {
        if (objectives == null) objectives = new System.Collections.Generic.List<ObjectiveVariable>();
        if (objectives.Count == 0)
        {
            var obj = new ObjectiveVariable { Key = "Objective 1", DisplayName = "Objective 1" };
            obj.name.value = obj.DisplayName;
            objectives.Add(obj);
        }
    }

    [Serializable]
    public class EnumQuestStatus : VariableValue<QuestStatus>
    {
        public EnumQuestStatus() { Key = "status"; DisplayName = "Status"; value = QuestStatus.NotStarted; }
    }
}
