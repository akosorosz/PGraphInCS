using PGraphInCS;

/*
 * In most applications, the basic data for materials and operating units, what was introduced so far, is not enough. One of the important use-cases of this library builds on extending the data of the PNS problem and its components.
 * The library provides 3 main methods for extending the used data:
 *   The quick method
 *   The efficient method
 *   The flexible method
 * Naturally, for complex problems, a combination of these 3 might be the best option.
 * 
 * This example shows the quick method of extending data, while other examples showcase the other methods.
 * In all 3 cases, we want to add a single, fix cost to each operating unit, and the objective function to minimize should be the sum of the costs of the operating units.
 */

/*
 * The quick way aims to solve the problem with the least effort.
 * Each GraphNode (i.e., MaterialNode and OperatingUnitNode) has an AdditionalParameters property: a Dictionary where any data can be stored and identified with a string key.
 * The quickest way to add new data to materials or operating units is to add them to this dictionary.
 */

// Let's start with the problem of Example 1
SimplePNSProblem problem = getSampleProblem();

// Now add a cost to each operating unit
// Naturally, it is not required to add the same data to each operating unit, but it makes the bounding simpler if it can be assumed.
problem.OperatingUnits["O1"].AdditionalParameters.Add("cost", 34);
problem.OperatingUnits["O2"].AdditionalParameters.Add("cost", 76);
problem.OperatingUnits["O3"].AdditionalParameters.Add("cost", 12);
problem.OperatingUnits["O4"].AdditionalParameters.Add("cost", 87);
problem.OperatingUnits["O5"].AdditionalParameters.Add("cost", 25);
problem.OperatingUnits["O6"].AdditionalParameters.Add("cost", 74);
problem.OperatingUnits["O7"].AdditionalParameters.Add("cost", 52);

// Now, all we need to do is create the bounding method we need, and then we can use it with any of the branch-and-bound methods (here, we choose the suggested ABB).
SimpleNetwork fixCostBounding(CommonImplementations.ABBSubproblem<SimplePNSProblem> subproblem)
{
    return new SimpleNetwork(subproblem.Included, subproblem.Included.Sum(unit => (int)unit.AdditionalParameters["cost"]));
}

// And now find all solutions:
var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<SimplePNSProblem, SimpleNetwork>(problem, fixCostBounding, maxSolutions: -1);
foreach (SimpleNetwork solution in abb.GetSolutionNetworks())
{
    Console.WriteLine($"Units: {solution.Units}, total cost: {solution.ObjectiveValue}");
}

// It is possible to make generic bounding methods which can be used by any branching methods what satisfy certain requirements (defined by interfaces)
//SimpleNetwork fixCostBounding(ISubpoblemWithIncludedExcludedGet subproblem)
//{
//    return new SimpleNetwork(subproblem.GetIncludedUnits(), subproblem.GetIncludedUnits().Sum(unit => (int)unit.AdditionalParameters["cost"]));
//}

// It is then can be used with any appropriate algorithms
//var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<SimplePNSProblem, SimpleNetwork>(problem, fixCostBounding, maxSolutions: -1);
//var binary = new DepthFirstOpenListBranchAndBoundAlgorithm<SimplePNSProblem, CommonImplementations.BinaryDecisionSubproblem<SimplePNSProblem>, SimpleNetwork>(problem, CommonImplementations.BinaryDecisionBranching, fixCostBounding, maxSolutions: -1);

// Naturally, the AdditionalParameters can be filled with any values.
// For example, if we want an investment cost and a yearly cost, they can be two different entries:
//problem.OperatingUnits["O7"].AdditionalParameters.Add("investmentcost", 12);
//problem.OperatingUnits["O7"].AdditionalParameters.Add("yearlycost", 3);

// Or they could be a single entry merged into a class:
//class CostData
//{
//    public int InvestmentCost { get; set; }
//    public int YearlyCost { get; set; }
//}
//problem.OperatingUnits["O7"].AdditionalParameters.Add("cost", new CostData { InvestmentCost = 12, YearlyCost = 3});

SimplePNSProblem getSampleProblem()
{
    SimplePNSProblem problem = new SimplePNSProblem();
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