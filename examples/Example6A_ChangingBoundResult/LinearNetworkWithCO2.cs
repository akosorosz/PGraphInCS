using PGraphInCS;
using PGraphInCS.LinearPNS.Efficient;

// The network is an extension of the built-in LinearNetwork
public class LinearNetworkWithCO2 : LinearNetwork
{
    public double CO2Production { get; set; }
    public LinearNetworkWithCO2(Dictionary<OperatingUnitNode, double> capacities, double objectiveValue, double co2Production) : base(capacities, objectiveValue)
    {
        CO2Production = co2Production;
    }
}
