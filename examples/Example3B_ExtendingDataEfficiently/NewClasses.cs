using PGraphInCS;

// Extending a material or operating unit with new data is done by creating a derived class.
public class OperatingUnitWithCost : OperatingUnitNode
{
    public int Cost { get; set; }
    public OperatingUnitWithCost(string name, int cost, MaterialSet? inputs = null, MaterialSet? outputs = null) :
        base(name, inputs, outputs)
    {
        Cost = cost;
    }
}

//If you don't want to write the full name of the PNS problem everywhere, you can make a derived class and give it a shorter name.
public class CostBasedPNSProblem : PNSProblem<MaterialNode, OperatingUnitWithCost> { }

// Naturally, if more new data is to be added, they can be added individually
//public class OperatingUnitWithCost : OperatingUnitNode
//{
//    public int InvestmentCost { get; set; }
//    public int YearlyCost { get; set; }
//    public OperatingUnitWithCost(string name, int investmentCost, int yearlyCost, MaterialSet? inputs = null, MaterialSet? outputs = null) :
//        base(name, inputs, outputs)
//    {
//        InvestmentCost = investmentCost;
//        YearlyCost = yearlyCost;
//    }
//}

// Or they can be grouped together
//public class CostData
//{
//    public int InvestmentCost { get; set; }
//    public int YearlyCost { get; set; }
//}
//public class OperatingUnitWithCost : OperatingUnitNode
//{
//    public CostData Cost { get; set; }
//    public OperatingUnitWithCost(string name, CostData cost, MaterialSet? inputs = null, MaterialSet? outputs = null) :
//        base(name, inputs, outputs)
//    {
//        Cost = cost;
//    }
//}