using System.Collections;

namespace PGraphInCS;

/// <summary>
/// Wrapper class for set implementation. Operatings like Union, Except, and Intersect return a new set.
/// Iterating over the elements does not guarantee any particular order.
/// </summary>
/// <typeparam name="NodeType">The type of the contained items. Must be derived from GraphNode.</typeparam>
public class NodeSet<NodeType> : ICollection<NodeType>
    where NodeType : GraphNode
{
    protected HashSet<NodeType> _internalStorage = new();
    public NodeSet() { }
    public NodeSet(IEnumerable<NodeType> items)
    {
        _internalStorage = new HashSet<NodeType>(items);
    }

    public int Count => _internalStorage.Count;
    public bool IsReadOnly => false;
    public void Add(NodeType item) => _internalStorage.Add(item);
    public void Clear() => _internalStorage.Clear();
    public bool Contains(NodeType item) => _internalStorage.Contains(item);
    public void CopyTo(NodeType[] array, int arrayIndex) => _internalStorage.CopyTo(array, arrayIndex);
    public IEnumerator<NodeType> GetEnumerator() => _internalStorage.GetEnumerator();
    public bool Remove(NodeType item) => _internalStorage.Remove(item);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void ExceptWith(IEnumerable<NodeType> other) => _internalStorage.ExceptWith(other);
    public void IntersectWith(IEnumerable<NodeType> other) => _internalStorage.IntersectWith(other);
    public void UnionWith(IEnumerable<NodeType> other) => _internalStorage.UnionWith(other);
    public bool IsProperSubsetOf(IEnumerable<NodeType> other) => _internalStorage.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<NodeType> other) => _internalStorage.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<NodeType> other) => _internalStorage.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<NodeType> other) => _internalStorage.IsSupersetOf(other);

    public virtual NodeSet<NodeType> Clone()
    {
        return new NodeSet<NodeType>(_internalStorage);
    }
    public NodeSet<NodeType> Except(IEnumerable<NodeType> other)
    {
        return new NodeSet<NodeType>(_internalStorage.Except(other));
    }
    public NodeSet<NodeType> Intersect(IEnumerable<NodeType> other)
    {
        return new NodeSet<NodeType>(_internalStorage.Intersect(other));
    }
    public NodeSet<NodeType> Union(IEnumerable<NodeType> other)
    {
        return new NodeSet<NodeType>(_internalStorage.Union(other));
    }
    public NodeSet<NodeType> Union(NodeType item)
    {
        var result = this.Clone();
        result.Add(item);
        return result;
    }
    public NodeSet<NodeType> Except(NodeType item)
    {
        var result = this.Clone();
        result.Remove(item);
        return result;
    }

    public override string ToString()
    {
        return "{" + String.Join(", ", _internalStorage.Select(m => m.Name)) + "}";
    }
    public string ToStringSortedById()
    {
        return "{" + String.Join(", ", _internalStorage.OrderBy(m => m.Id).Select(m => m.Name)) + "}";
    }
    public string ToStringSortedByName()
    {
        return "{" + String.Join(", ", _internalStorage.OrderBy(m => m.Name).Select(m => m.Name)) + "}";
    }
    public string ToString(Func<NodeType,string> itemFormatter)
    {
        return "{" + String.Join(", ", _internalStorage.Select(m => itemFormatter(m))) + "}";
    }
    public string ToStringSortedById(Func<NodeType, string> itemFormatter)
    {
        return "{" + String.Join(", ", _internalStorage.OrderBy(m => m.Id).Select(m => itemFormatter(m))) + "}";
    }
    public string ToStringSortedByName(Func<NodeType, string> itemFormatter)
    {
        return "{" + String.Join(", ", _internalStorage.OrderBy(m => m.Name).Select(m => itemFormatter(m))) + "}";
    }

    public NodeType? FindById(int id)
    {
        return _internalStorage.FirstOrDefault(m => m.Id == id);
    }
    public NodeType? FindByName(string name)
    {
        return _internalStorage.FirstOrDefault(m => m.Name == name);
    }
    public NodeType this[int id]
    {
        get => _internalStorage.First(m => m.Id == id);
    }
    public NodeType this[string name]
    {
        get => _internalStorage.First(m => m.Name == name);
    }

}