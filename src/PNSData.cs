

using System.Linq;

namespace PGraphInCS;

/// <summary>
/// Set implementation specific to materials. Operatings like Union, Except, and Intersect return a new set.
/// Iterating over the elements does not guarantee any particular order. 
/// </summary>
public class MaterialSet : NodeSet<MaterialNode>
{
    public MaterialSet() { }
    public MaterialSet(IEnumerable<MaterialNode> items) : base(items) { }

    public override MaterialSet Clone()
    {
        return new MaterialSet(_internalStorage);
    }
    public new MaterialSet Except(IEnumerable<MaterialNode> other)
    {
        return new MaterialSet(_internalStorage.Except(other));
    }
    public new MaterialSet Intersect(IEnumerable<MaterialNode> other)
    {
        return new MaterialSet(_internalStorage.Intersect(other));
    }
    public new MaterialSet Union(IEnumerable<MaterialNode> other)
    {
        return new MaterialSet(_internalStorage.Union(other));
    }
    public new MaterialSet Union(MaterialNode item)
    {
        var result = this.Clone();
        result.Add(item);
        return result;
    }
    public new MaterialSet Except(MaterialNode item)
    {
        var result = this.Clone();
        result.Remove(item);
        return result;
    }
}

/// <summary>
/// /// Set implementation specific to operating units. Operatings like Union, Except, and Intersect return a new set.
/// Iterating over the elements does not guarantee any particular order. 
/// </summary>
public class OperatingUnitSet : NodeSet<OperatingUnitNode>
{
    public OperatingUnitSet() { }
    public OperatingUnitSet(IEnumerable<OperatingUnitNode> items) : base(items) { }

    public override OperatingUnitSet Clone()
    {
        return new OperatingUnitSet(_internalStorage);
    }
    public new OperatingUnitSet Except(IEnumerable<OperatingUnitNode> other)
    {
        return new OperatingUnitSet(_internalStorage.Except(other));
    }
    public new OperatingUnitSet Intersect(IEnumerable<OperatingUnitNode> other)
    {
        return new OperatingUnitSet(_internalStorage.Intersect(other));
    }
    public new OperatingUnitSet Union(IEnumerable<OperatingUnitNode> other)
    {
        return new OperatingUnitSet(_internalStorage.Union(other));
    }

    public new OperatingUnitSet Union(OperatingUnitNode item)
    {
        var result = this.Clone();
        result.Add(item);
        return result;
    }
    public new OperatingUnitSet Except(OperatingUnitNode item)
    {
        var result = this.Clone();
        result.Remove(item);
        return result;
    }

    public MaterialSet Inputs()
    {
        return new MaterialSet(this.SelectMany(u => u.Inputs));
    }
    public MaterialSet Outputs()
    {
        return new MaterialSet(this.SelectMany(u => u.Outputs));
    }
    public OperatingUnitSet Consuming(MaterialNode material)
    {
        return new OperatingUnitSet(this.Where(u => u.Inputs.Contains(material)));
    }
    public OperatingUnitSet ConsumingAnyOf(MaterialSet materials)
    {
        return new OperatingUnitSet(this.Where(u => u.Inputs.Intersect(materials).Any()));
    }
    public OperatingUnitSet Producing(MaterialNode material)
    {
        return new OperatingUnitSet(this.Where(u => u.Outputs.Contains(material)));
    }
    public OperatingUnitSet ProducingAnyOf(MaterialSet materials)
    {
        return new OperatingUnitSet(this.Where(u => u.Outputs.Intersect(materials).Any()));
    }
}

public class MaterialNode : GraphNode
{
    public MaterialNode(String name) : base(name)
    {
    }
}

public class OperatingUnitNode : GraphNode
{
    public MaterialSet Inputs { get; } = new MaterialSet();
    public MaterialSet Outputs { get; } = new MaterialSet();

    public virtual void AddInput(MaterialNode node)
    {
        Inputs.Add(node);
    }
    public virtual void AddOutput(MaterialNode node)
    {
        Outputs.Add(node);
    }
    public OperatingUnitNode(String name, MaterialSet? inputs = null, MaterialSet? outputs = null) : base(name)
    {
        if (inputs != null)
            this.Inputs = inputs;
        if (outputs != null)
            this.Outputs = outputs;
    }
}

/// <summary>
/// Base class for PNS problem. Stores the materials and operating units of the problem, along with all necessary data.
/// </summary>
public abstract class PNSProblemBase
{
    public MaterialSet Materials { get; } = new MaterialSet();
    public OperatingUnitSet OperatingUnits { get; } = new OperatingUnitSet();
    public List<OperatingUnitSet> MutuallyExclusiveSets { get; } = new List<OperatingUnitSet>();
    public MaterialSet RawMaterials { get; } = new MaterialSet();
    public MaterialSet Intermediates { get; } = new MaterialSet();
    public MaterialSet Products { get; } = new MaterialSet();
    public Dictionary<MaterialNode, int> MaxParallelProduction { get; } = new Dictionary<MaterialNode, int>();
    public Dictionary<MaterialNode, OperatingUnitSet> Producers { get; } = new Dictionary<MaterialNode, OperatingUnitSet> { }; // only for optimization purposes
    public Dictionary<MaterialNode, OperatingUnitSet> Consumers { get; } = new Dictionary<MaterialNode, OperatingUnitSet> { }; // only for optimization purposes
    public Dictionary<OperatingUnitNode, OperatingUnitSet> MutuallyExclusiveUnits { get; } = new Dictionary<OperatingUnitNode, OperatingUnitSet>(); // only for optimization purposes
    public MaterialSet MaterialsWithParallelProductionLimit { get; } = new MaterialSet();

    protected void AddMaterialBase(MaterialNode node)
    {
        Materials.Add(node);
    }
    protected void AddOperatingUnitBase(OperatingUnitNode node)
    {
        OperatingUnits.Add(node);
    }
    public void RemoveMaterial(MaterialNode node)
    {
        if (Materials.Contains(node)) Materials.Remove(node);
        if (RawMaterials.Contains(node)) RawMaterials.Remove(node);
        if (Products.Contains(node)) Products.Remove(node);
        if (Intermediates.Contains(node)) Intermediates.Remove(node);
        if (MaterialsWithParallelProductionLimit.Contains(node)) MaterialsWithParallelProductionLimit.Remove(node);
        if (MaxParallelProduction.ContainsKey(node)) MaxParallelProduction.Remove(node);
        if (Producers.ContainsKey(node)) Producers.Remove(node);
        if (Consumers.ContainsKey(node)) Consumers.Remove(node);
    }
    public void RemoveOperatingUnit(OperatingUnitNode node)
    {
        if (OperatingUnits.Contains(node)) OperatingUnits.Remove(node);
        if (MutuallyExclusiveUnits.ContainsKey(node)) MutuallyExclusiveUnits.Remove(node);
    }
    public void AddMutuallyExclusiveSet(OperatingUnitSet units)
    {
        MutuallyExclusiveSets.Add(units);
    }

    public virtual void FinalizeData()
    {
        foreach (MaterialNode material in Materials)
        {
            if (!Producers.ContainsKey(material)) Producers.Add(material, new OperatingUnitSet());
            else Producers[material].Clear();
            if (!Consumers.ContainsKey(material)) Consumers.Add(material, new OperatingUnitSet());
            else Consumers[material].Clear();
            if (!MaxParallelProduction.ContainsKey(material)) MaxParallelProduction.Add(material, -1);
        }
        foreach (OperatingUnitNode unit in OperatingUnits)
        {
            foreach (MaterialNode material in unit.Inputs)
            {
                if (!Materials.Contains(material))
                {
                    Materials.Add(material);
                    Producers.Add(material, new OperatingUnitSet());
                    Consumers.Add(material, new OperatingUnitSet());
                    MaxParallelProduction.Add(material, -1);
                }
                Consumers[material].Add(unit);
            }
            foreach (MaterialNode material in unit.Outputs)
            {
                if (!Materials.Contains(material))
                {
                    Materials.Add(material);
                    Producers.Add(material, new OperatingUnitSet());
                    Consumers.Add(material, new OperatingUnitSet());
                    MaxParallelProduction.Add(material, -1);
                }
                Producers[material].Add(unit);
            }
        }
        MutuallyExclusiveUnits.Clear();
        foreach (OperatingUnitNode unit in OperatingUnits)
            MutuallyExclusiveUnits[unit] = new OperatingUnitSet();
        foreach (OperatingUnitSet units in MutuallyExclusiveSets)
        {
            foreach (OperatingUnitNode unit in units)
            {
                MutuallyExclusiveUnits[unit].UnionWith(units.Where(u => u != unit));
            }
        }
        MaterialsWithParallelProductionLimit.Clear();
        foreach (var (material, value) in MaxParallelProduction)
        {
            if (value != -1) MaterialsWithParallelProductionLimit.Add(material);
        }
    }

    public MaterialSet InputsOf(OperatingUnitSet units)
    {
        MaterialSet result = new MaterialSet();
        foreach (OperatingUnitNode unit in units)
            result.UnionWith(unit.Inputs);
        return result;
    }
    public MaterialSet OutputsOf(OperatingUnitSet units)
    {
        MaterialSet result = new MaterialSet();
        foreach (OperatingUnitNode unit in units)
            result.UnionWith(unit.Outputs);
        return result;
    }
    public OperatingUnitSet ProducersOf(MaterialSet materials)
    {
        OperatingUnitSet result = new OperatingUnitSet();
        foreach (MaterialNode material in materials)
            result.UnionWith(Producers[material]);
        return result;
    }
    public OperatingUnitSet ConsumersOf(MaterialSet materials)
    {
        OperatingUnitSet result = new OperatingUnitSet();
        foreach (MaterialNode material in materials)
            result.UnionWith(Consumers[material]);
        return result;
    }
    public OperatingUnitSet ProducersOf(MaterialNode material)
    {
        return Producers[material];
    }
    public OperatingUnitSet ConsumersOf(MaterialNode material)
    {
        return Consumers[material];
    }

    public OperatingUnitSet MutuallyExclusiveWith(OperatingUnitSet units)
    {
        OperatingUnitSet result = new OperatingUnitSet();
        foreach (OperatingUnitNode unit in units)
            result.UnionWith(MutuallyExclusiveUnits[unit]);
        return result;
    }

    public OperatingUnitSet MutuallyExclusiveWith(OperatingUnitNode unit)
    {
        return MutuallyExclusiveUnits[unit];
    }

    public void SetRawMaterialsAndProducts(MaterialSet rawMaterials, MaterialSet products)
    {
        RawMaterials.Clear();
        RawMaterials.UnionWith(rawMaterials);
        Products.Clear();
        Products.UnionWith(products);
        Intermediates.Clear();
        Intermediates.UnionWith(Materials.Except(rawMaterials).Except(products));
    }

    public void SetMaxParallelProduction(MaterialNode material, int maxParallelProd)
    {
        MaxParallelProduction[material] = maxParallelProd;
    }

    public void SetMaxParallelProduction(string materialName, int maxParallelProd)
    {
        var material = Materials.FindByName(materialName);
        if (material != null)
            MaxParallelProduction[material] = maxParallelProd;
    }
}

/// <summary>
/// Generic base class for PNS problems restricting the types of material and operating unit classes
/// </summary>
/// <typeparam name="MaterialNodeType"></typeparam>
/// <typeparam name="OperatingUnitNodeType"></typeparam>
public class PNSProblem<MaterialNodeType, OperatingUnitNodeType> : PNSProblemBase
    where MaterialNodeType : MaterialNode
    where OperatingUnitNodeType : OperatingUnitNode
{
    public virtual void AddMaterial(MaterialNodeType node)
    {
        base.AddMaterialBase(node);
    }
    public virtual void AddOperatingUnit(OperatingUnitNodeType node)
    {
        base.AddOperatingUnitBase(node);
    }
    public virtual void RemoveMaterial(MaterialNodeType node)
    {
        base.RemoveMaterial(node);
    }
    public virtual void RemoveOperatingUnit(OperatingUnitNodeType node)
    {
        base.RemoveOperatingUnit(node);
    }
}

/// <summary>
/// Simple PNS problem class using the basic material and operating units classes
/// </summary>
public class SimplePNSProblem : PNSProblem<MaterialNode, OperatingUnitNode>
{ }
