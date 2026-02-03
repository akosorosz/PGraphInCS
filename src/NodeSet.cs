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

    public void AddRange(IEnumerable<NodeType> items) => _internalStorage.UnionWith(items);

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

/// <summary>
/// Class to generate power set of NodeSets
/// </summary>
/// <typeparam name="NodeType">Type of the items in the original set</typeparam>
public class NodePowerSet<NodeType>
    where NodeType : GraphNode
{
    /// <summary>
    /// Generate subsets of a set with exactly a given number of items. 
    /// </summary>
    /// <typeparam name="SetType">Type of set to return</typeparam>
    /// <param name="data">Original set</param>
    /// <param name="maxItemCount">Item count for subsets. If less than 0, will be considered as 0. If gretear than item count of original set, will be considered as item count of original set.</param>
    /// <returns></returns>
    public static IEnumerable<SetType> GetCombinations<SetType>(SetType data, int numberOfItems)
        where SetType : NodeSet<NodeType>, new()
    {
        int itemCount = Math.Min(data.Count(), Math.Max(0, numberOfItems));
        int maxStartIndex = data.Count() - itemCount;
        if (itemCount == 0) return new List<SetType> { new SetType() };
        else return Enumerable.Range(0, maxStartIndex + 1).SelectMany(i => GetCombinationsInner(data.Skip(i + 1), itemCount - 1).Select(c => { SetType result = new(); result.AddRange(data.Skip(i).Take(1).Concat(c)); return result; }));
    }
    private static IEnumerable<IEnumerable<NodeType>> GetCombinationsInner(IEnumerable<NodeType> data, int numberOfItems)
    {
        int itemCount = Math.Min(data.Count(), Math.Max(0, numberOfItems));
        int maxStartIndex = data.Count() - itemCount;
        if (itemCount == 0) return new List<IEnumerable<NodeType>> { Enumerable.Empty<NodeType>() };
        else return Enumerable.Range(0, maxStartIndex + 1).SelectMany(i => GetCombinationsInner(data.Skip(i + 1), itemCount - 1).Select(c => data.Skip(i).Take(1).Concat(c)));
    }

    /// <summary>
    /// Generates the complete power set, or just subsets up to a certain item count
    /// </summary>
    /// <typeparam name="SetType">Type of set to return</typeparam>
    /// <param name="data">Original set</param>
    /// <param name="maxItemCount">Item count upper bound. Default: -1 (generate all subsets).</param>
    /// <returns></returns>
    public static IEnumerable<SetType> GetPowerSet<SetType>(SetType data, int maxItemCount = -1)
        where SetType : NodeSet<NodeType>, new()
    {
        int itemCount = maxItemCount == -1 ? data.Count() : Math.Min(data.Count(), Math.Max(0, maxItemCount));
        if (itemCount == 0) return new List<SetType> { new SetType() };
        else return GetPowerSet(data, itemCount - 1).Concat(GetCombinations(data, itemCount));
    }
}