using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGraphInCS;

/// <summary>
/// Common base class for elements of a P-graph (materials and operating units).
/// </summary>
public class GraphNode
{
    private static Int32 GlobalId = 0;
    public String Name { get; }
    public Int32 Id { get; }
    public Dictionary<string, object> AdditionalParameters { get; } = new Dictionary<string, object>();

    public GraphNode(String name)
    {
        this.Id = ++GlobalId;
        this.Name = name;
    }

    public override int GetHashCode()
    {
        return Id;
    }
}
