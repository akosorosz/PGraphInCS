using PGraphInCS;

// We need to store the new data for the operating units
public class ExtendedOperatingUnit : OperatingUnitNode
{
    public int Cost { get; set; } = 0;
    public int Weight { get; set; } = 0;
    public ExtendedOperatingUnit(string name, MaterialSet? inputs = null, MaterialSet? outputs = null, int cost = 0, int weight = 0) :
        base(name, inputs, outputs)
    {
        Cost = cost;
        Weight = weight;
    }
}

// The PNS problem needs to contain the weight limit
public class WeightLimitPNSProblem : PNSProblem<MaterialNode, ExtendedOperatingUnit>
{
    public int WeightLimit { get; set; } = 10000000;
}

// And we want to store the total weight in the network
// We want to keep total cost as the main objective, but in case the cost is the same, we prefer networks with lower total weight, so we define a custom comparator
public class WeightedNetwork : SimpleNetwork, IComparable<WeightedNetwork>
{
    public int TotalWeight { get; set; }
    public WeightedNetwork(double objectiveValue, int totalWeight) : base(objectiveValue)
    {
        TotalWeight = totalWeight;
    }

    public WeightedNetwork(OperatingUnitSet units, double objectiveValue, int totalWeight) : base(units, objectiveValue)
    {
        TotalWeight = totalWeight;
    }

    public int CompareTo(WeightedNetwork? other)
    {
        if (other == null) return 1;
        if (this.ObjectiveValue.CompareTo(other.ObjectiveValue) != 0) return this.ObjectiveValue.CompareTo(other.ObjectiveValue);
        return this.TotalWeight.CompareTo(other.TotalWeight);
    }
}