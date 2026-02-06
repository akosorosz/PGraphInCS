# Example 3b: Extending data the flexible way

In most applications, the basic data for materials and operating units, what was introduced so far, is not enough. One of the important use-cases of this library builds on extending the data of the PNS problem and its components.

The library provides 3 main methods for extending the used data:
- The quick method
- The efficient method
- The flexible method

Naturally, for complex problems, a combination of these 3 might be the best option.

This example shows the flexible method of extending data, while other examples showcase the other methods.
In all 3 cases, we want to add a single, fix cost to each operating unit, and the objective function to minimize should be the sum of the costs of the operating units.

The flexible way aims to prioritize flexibility of the PNS problems in relation to the new data. Here, the materials and operating units do not store any new data, instead, everything is stored by the PNS problem.
This way it is possible to handle multiple PNS problems with the same materials and operating units which only vary in some internal parameters (e.g. the problem is the same, but the cost of some units are different).

To extend the data in the flexible way, it is necessary to make a dedicated PNS problem class which stores the new data. How to actually store and set the data is not limited, but a Dictionary is generally a good starting point

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

        //...
    }

It is advised to override FinalizeData() and extend it. This can ensure that all materials and operating units have at least a default value assigned. If this is omitted, then everywhere you want to use the new data, you have to check for its existence.
        
    public class CostBasedPNSProblem : PNSProblem<MaterialNode, OperatingUnitNode>
    {
        //...
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

The PNS problem of Example 1 is still used here, however, we subclass from the PNS problem

    CostBasedPNSProblem problem = getSampleProblem();

We could set the costs in the previous method, or outside

    problem.SetUnitCost("O1", 34);
    problem.SetUnitCost("O2", 76);
    problem.SetUnitCost("O3", 12);
    problem.SetUnitCost("O4", 87);
    problem.SetUnitCost("O5", 25);
    problem.SetUnitCost("O6", 74);
    problem.SetUnitCost("O7", 52);

Don't forget FinalizeData(), since the data stored in the PNS problem has changed!

    problem.FinalizeData();

Now, create the bounding function. The PNS problem is always available from the subproblem

    SimpleNetwork fixCostBounding(CommonImplementations.ABBSubproblem<CostBasedPNSProblem> subproblem)
    {
        return new SimpleNetwork(subproblem.Included, subproblem.Included.Sum(unit => subproblem.Problem.UnitCosts[unit]));
    }

And now find all solutions:

    var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<CostBasedPNSProblem, SimpleNetwork>(problem, fixCostBounding, maxSolutions: -1);
    foreach (SimpleNetwork solution in abb.GetSolutionNetworks())
    {
        Console.WriteLine($"Units: {solution.Units}, total cost: {solution.ObjectiveValue}");
    }

It is possible to make generic bounding methods which can be used by any branching methods what satisfy certain requirements (defined by interfaces or base classes)

    SimpleNetwork fixCostBounding<SubproblemType>(SubproblemType subproblem)
        where SubproblemType : SubproblemBase<CostBasedPNSProblem>, ISubpoblemWithIncludedExcludedGet
    {
        return new SimpleNetwork(subproblem.GetIncludedUnits(), subproblem.GetIncludedUnits().Sum(unit => subproblem.Problem.UnitCosts[unit]));
    }

It is then can be used with any appropriate algorithms

    var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<CostBasedPNSProblem, SimpleNetwork>(problem, fixCostBounding, maxSolutions: -1);
    var binary = new DepthFirstOpenListBranchAndBoundAlgorithm<CostBasedPNSProblem, CommonImplementations.BinaryDecisionSubproblem<CostBasedPNSProblem>, SimpleNetwork>(problem, CommonImplementations.BinaryDecisionBranching, fixCostBounding, maxSolutions: -1);

Since the flexible extension was used, it is easy to create a copy of the problem with the same operating units and material, and change the cost data without invalidating the results of the original problem.

    CostBasedPNSProblem newProblem = new CostBasedPNSProblem();
    foreach (MaterialNode material in problem.Materials)
        newProblem.AddMaterial(material);
    foreach (OperatingUnitNode unit in problem.OperatingUnits)
        newProblem.AddOperatingUnit(unit);
    newProblem.SetUnitCost("O1", 87);
    newProblem.SetUnitCost("O2", 21);
    newProblem.SetUnitCost("O3", 98);
    newProblem.SetUnitCost("O4", 25);
    newProblem.SetUnitCost("O5", 84);
    newProblem.SetUnitCost("O6", 74);
    newProblem.SetUnitCost("O7", 68);
    
    newProblem.FinalizeData();


Naturally, if more new data is to be added, they can be added individually

    public class CostBasedPNSProblem : PNSProblem<MaterialNode, OperatingUnitNode>
    {
        public Dictionary<OperatingUnitNode, int> InvestmentCosts = new();
        public Dictionary<OperatingUnitNode, int> YearlyCosts = new();
    }

Or they can be grouped together

    public class CostData
    {
        public int InvestmentCost { get; set; }
        public int YearlyCost { get; set; }
    }
    
    public class CostBasedPNSProblem : PNSProblem<MaterialNode, OperatingUnitNode>
    {
        public Dictionary<OperatingUnitNode, CostData> CostData = new();
    }