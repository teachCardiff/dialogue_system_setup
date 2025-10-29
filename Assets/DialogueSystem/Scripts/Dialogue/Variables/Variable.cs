// filepath: Assets/DialogueSystem/Scripts/Dialogue/Variables/Variable.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public abstract class Variable
{
    [SerializeField, HideInInspector] private string id;
    [SerializeField] private string key; // unique among siblings for path lookup
    [SerializeField] private string displayName;

    [NonSerialized] private Variable parent;

    public string Id
    {
        get
        {
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString();
            return id;
        }
    }

    public string Key { get => string.IsNullOrEmpty(key) ? displayName : key; set => key = value; }
    public string DisplayName { get => string.IsNullOrEmpty(displayName) ? key : displayName; set => displayName = value; }
    public Variable Parent { get => parent; internal set => parent = value; }

    public virtual IEnumerable<Variable> GetChildren() { yield break; }
    public abstract Type ValueType { get; }
    public abstract object GetBoxed();
    public abstract void SetBoxed(object value);

    public string GetPath()
    {
        var stack = new Stack<string>();
        var node = this;
        while (node != null)
        {
            if (!string.IsNullOrEmpty(node.Key)) stack.Push(node.Key);
            node = node.Parent;
        }
        return string.Join("/", stack);
    }
}

[Serializable]
public abstract class VariableValue<T> : Variable
{
    public T value;
    public override Type ValueType => typeof(T);
    public override object GetBoxed() => value;
    public override void SetBoxed(object v)
    {
        if (v is T tv) value = tv;
        else if (typeof(T).IsEnum)
        {
            if (v is string s)
            {
                value = (T)Enum.Parse(typeof(T), s, true);
            }
            else if (v != null)
            {
                value = (T)Enum.ToObject(typeof(T), v);
            }
        }
        else if (v is IConvertible)
        {
            value = (T)Convert.ChangeType(v, typeof(T));
        }
    }
}

[Serializable]
public class VariableGroup : Variable
{
    [SerializeReference] private List<Variable> children = new List<Variable>();
    public IReadOnlyList<Variable> Children => children;

    public override Type ValueType => null;
    public override object GetBoxed() => null;
    public override void SetBoxed(object value) { }
    public override IEnumerable<Variable> GetChildren() => children;

    public void AddChild(Variable child)
    {
        if (child == null) return;
        if (!children.Contains(child)) children.Add(child);
        child.Parent = this;
    }

    public void RemoveChild(Variable child)
    {
        if (child == null) return;
        children.Remove(child);
        if (child.Parent == this) child.Parent = null;
    }

    public Variable FindByGuid(string guid)
    {
        foreach (var v in Traverse(this))
        {
            if (v == null) continue;
            if (v.Id == guid) return v;
        }
        return null;
    }

    public Variable FindByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        Variable current = this;
        Variable found = null;
        foreach (var part in parts)
        {
            found = null;
            foreach (var child in current.GetChildren())
            {
                if (child == null) continue;
                if (string.Equals(child.Key, part, StringComparison.OrdinalIgnoreCase))
                {
                    found = child;
                    break;
                }
            }
            if (found == null) return null;
            current = found;
        }
        return found;
    }

    public VariableGroup EnsureGroup(params string[] parts)
    {
        Variable current = this;
        VariableGroup currentGroup = this;
        foreach (var part in parts)
        {
            // Search among current's children
            VariableGroup next = null;
            foreach (var child in current.GetChildren())
            {
                if (child is VariableGroup g && string.Equals(g.Key, part, StringComparison.OrdinalIgnoreCase))
                {
                    next = g; break;
                }
            }
            if (next == null)
            {
                next = new VariableGroup { Key = part, DisplayName = part };
                currentGroup.AddChild(next);
            }
            current = next;
            currentGroup = next;
        }
        return currentGroup;
    }

    public static IEnumerable<Variable> Traverse(Variable v)
    {
        if (v == null) yield break;
        yield return v;
        foreach (var c in v.GetChildren())
        {
            if (c == null) continue;
            foreach (var t in Traverse(c)) yield return t;
        }
    }

    public void RebuildParentLinks()
    {
        // Assign parents for direct children and recurse into all composite children
        foreach (var child in children.ToArray())
        {
            if (child == null)
            {
                children.Remove(child);
                continue;
            }
            child.Parent = this;
            AssignParentsRecursive(child);
        }
    }

    private static void AssignParentsRecursive(Variable parent)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child == null) continue;
            child.Parent = parent;
            AssignParentsRecursive(child);
        }
    }
}

[Serializable] public class IntVar : VariableValue<int> { }
[Serializable] public class FloatVar : VariableValue<float> { }
[Serializable] public class BoolVar : VariableValue<bool> { }
[Serializable] public class StringVar : VariableValue<string> { }
