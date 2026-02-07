using PGraphInCS;

// There is no need to base the new network class on LinearNetwork, so let's see an example for building it from scratch
public class CO2FocusedLinearNetwork : NetworkBase, IComparable<CO2FocusedLinearNetwork>
{
    public Dictionary<OperatingUnitNode, double> UnitCapacities { get; } = new();
    public double CO2Production { get; set; }
    public double Cost { get; set; }

    // We expect operating units with capacities. The base class (NetworkBase) contains the set of operating units, so we need to set that.
    public CO2FocusedLinearNetwork(Dictionary<OperatingUnitNode, double> capacities, double co2Production, double cost) :
        base(new OperatingUnitSet(capacities.Keys))
    {
        UnitCapacities = capacities;
        CO2Production = co2Production;
        Cost = cost;
    }

    // Ordering by default should be based on CO2 production first, cost second.
    public int CompareTo(CO2FocusedLinearNetwork? other)
    {
        if (other == null) return 1;
        if (this.CO2Production.CompareTo(other.CO2Production) != 0) return this.CO2Production.CompareTo(other.CO2Production);
        return this.Cost.CompareTo(other.Cost);
    }
}
