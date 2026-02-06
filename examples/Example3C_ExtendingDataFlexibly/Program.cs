using PGraphInCS;

/*
 * In most applications, the basic data for materials and operating units, what was introduced so far, is not enough. One of the important use-cases of this library builds on extending the data of the PNS problem and its components.
 * The library provides 3 main methods for extending the used data:
 *   The quick method
 *   The efficient method
 *   The flexible method
 * Naturally, for complex problems, a combination of these 3 might be the best option.
 * 
 * This example shows the flexible method of extending data, while other examples showcase the other methods.
 * In all 3 cases, we want to add a single, fix cost to each operating unit, and the objective function to minimize should be the sum of the costs of the operating units.
 */

/*
 * The flexible way aims to prioritize flexibility of the PNS problems in relation to the new data.
 * Here, the materials and operating units do not store any new data, instead, everything is stored by the PNS problem.
 * This way it is possible to handle multiple PNS problems with the same materials and operating units which only vary in some internal parameters (e.g. the problem is the same, but the cost of some units are different).
 */

// The PNS problem of Example 1 is still used here, however, we subclass from the PNS problem
CostBasedPNSProblem problem = getSampleProblem();

// We could set the costs in the previous method, or outside
problem.SetUnitCost("O1", 34);
problem.SetUnitCost("O2", 76);
problem.SetUnitCost("O3", 12);
problem.SetUnitCost("O4", 87);
problem.SetUnitCost("O5", 25);
problem.SetUnitCost("O6", 74);
problem.SetUnitCost("O7", 52);

// Don't forget FinalizeData(), since the data stored in the PNS problem has changed!
problem.FinalizeData();

// Now, create the bounding function. The PNS problem is always available from the subproblem
SimpleNetwork fixCostBounding(CommonImplementations.ABBSubproblem<CostBasedPNSProblem> subproblem)
{
    return new SimpleNetwork(subproblem.Included, subproblem.Included.Sum(unit => subproblem.Problem.UnitCosts[unit]));
}

// And now find all solutions:
var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<CostBasedPNSProblem, SimpleNetwork>(problem, fixCostBounding, maxSolutions: -1);
foreach (SimpleNetwork solution in abb.GetSolutionNetworks())
{
    Console.WriteLine($"Units: {solution.Units}, total cost: {solution.ObjectiveValue}");
}

// It is possible to make generic bounding methods which can be used by any branching methods what satisfy certain requirements (defined by interfaces or base classes)
//SimpleNetwork fixCostBounding<SubproblemType>(SubproblemType subproblem)
//    where SubproblemType : SubproblemBase<CostBasedPNSProblem>, ISubpoblemWithIncludedExcludedGet
//{
//    return new SimpleNetwork(subproblem.GetIncludedUnits(), subproblem.GetIncludedUnits().Sum(unit => subproblem.Problem.UnitCosts[unit]));
//}

// It is then can be used with any appropriate algorithms
//var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<CostBasedPNSProblem, SimpleNetwork>(problem, fixCostBounding, maxSolutions: -1);
//var binary = new DepthFirstOpenListBranchAndBoundAlgorithm<CostBasedPNSProblem, CommonImplementations.BinaryDecisionSubproblem<CostBasedPNSProblem>, SimpleNetwork>(problem, CommonImplementations.BinaryDecisionBranching, fixCostBounding, maxSolutions: -1);

// Since the flexible extension was used, it is easy to create a copy of the problem with the same operating units and material, and change the cost data without invalidating the results of the original problem.
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



CostBasedPNSProblem getSampleProblem()
{
    CostBasedPNSProblem problem = new CostBasedPNSProblem();
    MaterialNode mA = new MaterialNode("A");
    MaterialNode mB = new MaterialNode("B");
    MaterialNode mC = new MaterialNode("C");
    MaterialNode mD = new MaterialNode("D");
    MaterialNode mE = new MaterialNode("E");
    MaterialNode mF = new MaterialNode("F");
    MaterialNode mG = new MaterialNode("G");
    MaterialNode mH = new MaterialNode("H");
    MaterialNode mJ = new MaterialNode("J");
    MaterialNode mK = new MaterialNode("K");
    MaterialNode mL = new MaterialNode("L");

    OperatingUnitNode O1 = new OperatingUnitNode("O1", inputs: [mC], outputs: [mA, mF]);
    OperatingUnitNode O2 = new OperatingUnitNode("O2", inputs: [mD], outputs: [mA, mB]);
    OperatingUnitNode O3 = new OperatingUnitNode("O3", inputs: [mE, mF], outputs: [mC]);
    OperatingUnitNode O4 = new OperatingUnitNode("O4", inputs: [mF, mG], outputs: [mC, mD]);
    OperatingUnitNode O5 = new OperatingUnitNode("O5", inputs: [mG, mH], outputs: [mD]);
    OperatingUnitNode O6 = new OperatingUnitNode("O6", inputs: [mJ], outputs: [mF]);
    OperatingUnitNode O7 = new OperatingUnitNode("O7", inputs: [mK, mL], outputs: [mH]);

    problem.AddMaterial(mA);
    problem.AddMaterial(mB);
    problem.AddMaterial(mC);
    problem.AddMaterial(mD);
    problem.AddMaterial(mE);
    problem.AddMaterial(mF);
    problem.AddMaterial(mG);
    problem.AddMaterial(mH);
    problem.AddMaterial(mJ);
    problem.AddMaterial(mK);
    problem.AddMaterial(mL);

    problem.AddOperatingUnit(O1);
    problem.AddOperatingUnit(O2);
    problem.AddOperatingUnit(O3);
    problem.AddOperatingUnit(O4);
    problem.AddOperatingUnit(O5);
    problem.AddOperatingUnit(O6);
    problem.AddOperatingUnit(O7);

    problem.SetRawMaterialsAndProducts(rawMaterials: new MaterialSet { mE, mG, mJ, mK, mL }, products: new MaterialSet { mA });

    problem.FinalizeData();

    return problem;
}