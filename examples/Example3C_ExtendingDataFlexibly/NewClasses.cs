using PGraphInCS;

// To extend the data in the flexible way, it is necessary to make a dedicated PNS problem class which stores the new data
// How to actually store and set the data is not limited, but a Dictionary is generally a good starting point
public class CostBasedPNSProblem : PNSProblem<MaterialNode, OperatingUnitNode>
{
    public Dictionary<OperatingUnitNode, int> UnitCosts = new();

    public void SetUnitCost(OperatingUnitNode unit, int cost)
    {
        UnitCosts[unit] = cost;
    }
    public void SetUnitCost(string name, int cost)
    {
        OperatingUnitNode? unit = OperatingUnits.FindByName(name);
        if (unit != null)
        {
            UnitCosts[unit] = cost;
        }
    }
    public void SetUnitCost(int id, int cost)
    {
        OperatingUnitNode? unit = OperatingUnits.FindById(id);
        if (unit != null)
        {
            UnitCosts[unit] = cost;
        }
    }

    // It is advised to override FinalizeData() and extend it. This can ensure that all materials and operating units have at least a default value assigned.
    // If this is omitted, then everywhere you want to use the new data, you have to check for its existence.
    public override void FinalizeData()
    {
        base.FinalizeData();

        foreach (var unit in OperatingUnits)
        {
            if (!UnitCosts.ContainsKey(unit))
                UnitCosts.Add(unit, 0);
        }
    }
}

// Naturally, if more new data is to be added, they can be added individually
//public class CostBasedPNSProblem : PNSProblem<MaterialNode, OperatingUnitNode>
//{
//    public Dictionary<OperatingUnitNode, int> InvestmentCosts = new();
//    public Dictionary<OperatingUnitNode, int> YearlyCosts = new();
//}

// Or they can be grouped together
//public class CostData
//{
//    public int InvestmentCost { get; set; }
//    public int YearlyCost { get; set; }
//}

//public class CostBasedPNSProblem : PNSProblem<MaterialNode, OperatingUnitNode>
//{
//    public Dictionary<OperatingUnitNode, CostData> CostData = new();
//}