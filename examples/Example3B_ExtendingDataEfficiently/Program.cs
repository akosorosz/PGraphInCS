using PGraphInCS;

/*
 * In most applications, the basic data for materials and operating units, what was introduced so far, is not enough. One of the important use-cases of this library builds on extending the data of the PNS problem and its components.
 * The library provides 3 main methods for extending the used data:
 *   The quick method
 *   The efficient method
 *   The flexible method
 * Naturally, for complex problems, a combination of these 3 might be the best option.
 * 
 * This example shows the efficient method of extending data, while other examples showcase the other methods.
 * In all 3 cases, we want to add a single, fix cost to each operating unit, and the objective function to minimize should be the sum of the costs of the operating units.
 */

/*
 * The efficient way aims to handle the data with type safety and minimal performance overhead.
 * It is done by creating derived classes from MaterialNode and OperatingUnitNode to contain the new data.
 */

// The PNS problem of Example 1 is still used here, however, we subclass from OperatingUnitNode, therefore we should not use SimplePNSProblem anymore.
// Technically, it is still possible to work with the SimplePNSProblem class, as all operating unit types can be handled together as the base class (and the same goes for materials).
// However, by using the generic PNSProblem class with the proper types, it guarantees that only the materials or operating units of the derived classes can be added the the problem. This ensures that all nodes have the required data.
PNSProblem<MaterialNode,OperatingUnitWithCost> problem = getSampleProblem();

//If you don't want to write the full name of the PNS problem everywhere, you can make a derived class and give it a shorter name.
//CostBasedPNSProblem problem = getSampleProblem();

// Now, create the bounding function. Here, we need to cast the operating units to the derived class. However, if the generic PNSProblem is used, it is guaranteed that only operating units of the derived types can appear in the problem.
SimpleNetwork fixCostBounding(CommonImplementations.ABBSubproblem<PNSProblem<MaterialNode, OperatingUnitWithCost>> subproblem)
{
    return new SimpleNetwork(subproblem.Included, subproblem.Included.Sum(unit => (unit as OperatingUnitWithCost)!.Cost));
}

// And now find all solutions:
var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<PNSProblem<MaterialNode, OperatingUnitWithCost>, SimpleNetwork>(problem, fixCostBounding, maxSolutions: -1);
foreach (SimpleNetwork solution in abb.GetSolutionNetworks())
{
    Console.WriteLine($"Units: {solution.Units}, total cost: {solution.ObjectiveValue}");
}

// It is possible to make generic bounding methods which can be used by any branching methods what satisfy certain requirements (defined by interfaces)
//SimpleNetwork fixCostBounding(ISubpoblemWithIncludedExcludedGet subproblem)
//{
//    return new SimpleNetwork(subproblem.GetIncludedUnits(), subproblem.GetIncludedUnits().Sum(unit => (unit as OperatingUnitWithCost)!.Cost));
//}

// It is then can be used with any appropriate algorithms
//var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<PNSProblem<MaterialNode, OperatingUnitWithCost>, SimpleNetwork>(problem, fixCostBounding, maxSolutions: -1);
//var binary = new DepthFirstOpenListBranchAndBoundAlgorithm<PNSProblem<MaterialNode, OperatingUnitWithCost>, CommonImplementations.BinaryDecisionSubproblem<PNSProblem<MaterialNode, OperatingUnitWithCost>>, SimpleNetwork>(problem, CommonImplementations.BinaryDecisionBranching, fixCostBounding, maxSolutions: -1);

PNSProblem<MaterialNode, OperatingUnitWithCost> getSampleProblem()
{
    PNSProblem<MaterialNode, OperatingUnitWithCost> problem = new PNSProblem<MaterialNode, OperatingUnitWithCost>();
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

    OperatingUnitWithCost O1 = new OperatingUnitWithCost("O1", 34, inputs: [mC], outputs: [mA, mF]);
    OperatingUnitWithCost O2 = new OperatingUnitWithCost("O2", 76, inputs: [mD], outputs: [mA, mB]);
    OperatingUnitWithCost O3 = new OperatingUnitWithCost("O3", 12, inputs: [mE, mF], outputs: [mC]);
    OperatingUnitWithCost O4 = new OperatingUnitWithCost("O4", 87, inputs: [mF, mG], outputs: [mC, mD]);
    OperatingUnitWithCost O5 = new OperatingUnitWithCost("O5", 25, inputs: [mG, mH], outputs: [mD]);
    OperatingUnitWithCost O6 = new OperatingUnitWithCost("O6", 74, inputs: [mJ], outputs: [mF]);
    OperatingUnitWithCost O7 = new OperatingUnitWithCost("O7", 52, inputs: [mK, mL], outputs: [mH]);

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