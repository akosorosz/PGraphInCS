# Example 3b: Extending data the efficient way

In most applications, the basic data for materials and operating units, what was introduced so far, is not enough. One of the important use-cases of this library builds on extending the data of the PNS problem and its components.

The library provides 3 main methods for extending the used data:
- The quick method
- The efficient method
- The flexible method

Naturally, for complex problems, a combination of these 3 might be the best option.

This example shows the efficient method of extending data, while other examples showcase the other methods.
In all 3 cases, we want to add a single, fix cost to each operating unit, and the objective function to minimize should be the sum of the costs of the operating units.

The efficient way aims to handle the data with type safety and minimal performance overhead. It is done by creating derived classes from MaterialNode and OperatingUnitNode to contain the new data.

Extending a material or operating unit with new data is done by creating a derived class.

    public class OperatingUnitWithCost : OperatingUnitNode
    {
        public int Cost { get; set; }
        public OperatingUnitWithCost(string name, int cost, MaterialSet? inputs = null, MaterialSet? outputs = null) :
            base(name, inputs, outputs)
        {
            Cost = cost;
        }
    }

The PNS problem of Example 1 is still used here, however, we subclass from OperatingUnitNode, therefore we should not use SimplePNSProblem anymore.
Technically, it is still possible to work with the SimplePNSProblem class, as all operating unit types can be handled together as the base class (and the same goes for materials). However, by using the generic PNSProblem class with the proper types, it guarantees that only the materials or operating units of the derived classes can be added the the problem. This ensures that all nodes have the required data.

    PNSProblem<MaterialNode,OperatingUnitWithCost> problem = getSampleProblem();

If you don't want to write the full name of the PNS problem everywhere, you can make a derived class and give it a shorter name.
    
    public class CostBasedPNSProblem : PNSProblem<MaterialNode, OperatingUnitWithCost> { }
    CostBasedPNSProblem problem = getSampleProblem();

Now, create the bounding function. Here, we need to cast the operating units to the derived class. However, if the generic PNSProblem is used, it is guaranteed that only operating units of the derived types can appear in the problem.

    SimpleNetwork fixCostBounding(CommonImplementations.ABBSubproblem<PNSProblem<MaterialNode, OperatingUnitWithCost>> subproblem)
    {
        return new SimpleNetwork(subproblem.Included, subproblem.Included.Sum(unit => (unit as OperatingUnitWithCost)!.Cost));
    }

And now find all solutions:

    var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<PNSProblem<MaterialNode, OperatingUnitWithCost>, SimpleNetwork>(problem, fixCostBounding, maxSolutions: -1);
    foreach (SimpleNetwork solution in abb.GetSolutionNetworks())
    {
        Console.WriteLine($"Units: {solution.Units}, total cost: {solution.ObjectiveValue}");
    }

It is possible to make generic bounding methods which can be used by any branching methods what satisfy certain requirements (defined by interfaces)

    SimpleNetwork fixCostBounding(ISubpoblemWithIncludedExcludedGet subproblem)
    {
        return new SimpleNetwork(subproblem.GetIncludedUnits(), subproblem.GetIncludedUnits().Sum(unit => (unit as OperatingUnitWithCost)!.Cost));
    }

It is then can be used with any appropriate algorithms

    var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<PNSProblem<MaterialNode, OperatingUnitWithCost>, SimpleNetwork>(problem, fixCostBounding, maxSolutions: -1);
    var binary = new DepthFirstOpenListBranchAndBoundAlgorithm<PNSProblem<MaterialNode, OperatingUnitWithCost>, CommonImplementations.BinaryDecisionSubproblem<PNSProblem<MaterialNode, OperatingUnitWithCost>>, SimpleNetwork>(problem, CommonImplementations.BinaryDecisionBranching, fixCostBounding, maxSolutions: -1);

Naturally, if more new data is to be added, they can be added individually

    public class OperatingUnitWithCost : OperatingUnitNode
    {
        public int InvestmentCost { get; set; }
        public int YearlyCost { get; set; }
        public OperatingUnitWithCost(string name, int investmentCost, int yearlyCost, MaterialSet? inputs = null, MaterialSet? outputs = null) :
            base(name, inputs, outputs)
        {
            InvestmentCost = investmentCost;
            YearlyCost = yearlyCost;
        }
    }

Or they can be grouped together

    public class CostData
    {
        public int InvestmentCost { get; set; }
        public int YearlyCost { get; set; }
    }
    public class OperatingUnitWithCost : OperatingUnitNode
    {
        public CostData Cost { get; set; }
        public OperatingUnitWithCost(string name, CostData cost, MaterialSet? inputs = null, MaterialSet? outputs = null) :
            base(name, inputs, outputs)
        {
            Cost = cost;
        }
    }