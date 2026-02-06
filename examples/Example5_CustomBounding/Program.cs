using PGraphInCS;

/*
 * The concept of providing custom bounding methods has been introduced before. However, the bounding method does more than just determining an objective value.
 * The bounding method has 3 responsibilities:
 *   Determine feasibility: if the subproblem is infeasbile, the method should return null (structural feasibility is usually guaranteed by the branching, while the bounding examines non-structural feasbility, but you can deviate from this if necessary)
 *   Determine objective value: for leaf subproblems, the method should return the exact value of the objective function
 *   Determine bound: for intermediate subproblems, the method should return a lower bound which guarantees that no descendant subproblem has a lower objective value / bound
 * Note that it is not required to use a single number as the objective value. If the problem requires you can define the objective as a complex value with custom comparison logic.
 */

// In this example we continue the data extension from Examples 3A, 3B, and 3C, where all operating units had a fix cost assigned.
// Here, we also assign a weight to each operating unit. Furthermore, a weight limit is introduced to the PNS problem, and the total weight of the operating units cannot exceed this limit.
// For this code, we use the efficient extension of the operating units, while storing the weight limit in the PNS problem.

// Let's use the same PNS problem introduced in Example 1, extended with the new data (the costs are different, to showcase the network ordering later)
WeightLimitPNSProblem problem = getSampleProblem();

// Set the weight limit
problem.WeightLimit = 20;

/*
 * Let's write the bounding method. It will do two things:
 *   Calculate the total cost of the included units
 *   Calculate the total weight of the included units, and mark the subproblem as infeasible if the weight is too high
 * Let's also make it generic, to make it compatible with any branching algorithm
 */
WeightedNetwork? boundMethod<SubproblemType>(SubproblemType subproblem)
    where SubproblemType : SubproblemBase<WeightLimitPNSProblem>, ISubpoblemWithIncludedExcludedGet
{
    OperatingUnitSet includedUnits = subproblem.GetIncludedUnits();
    int totalWeigth = includedUnits.Cast<ExtendedOperatingUnit>().Sum(u => u.Weight);
    if (totalWeigth > subproblem.Problem.WeightLimit) return null;
    
    int totalCost = includedUnits.Cast<ExtendedOperatingUnit>().Sum(u => u.Cost);
    return new WeightedNetwork(includedUnits, totalCost, totalWeigth);
}

// Now, let's solve the problem with ABB, as that's the simpler to use
var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<WeightLimitPNSProblem, WeightedNetwork>(problem, boundMethod, maxSolutions: -1);
foreach (WeightedNetwork network in abb.GetSolutionNetworks())
{
    Console.WriteLine($"{network.Units,25} -- cost: {network.ObjectiveValue,3}, weight: {network.TotalWeight}");
}

WeightLimitPNSProblem getSampleProblem()
{
    WeightLimitPNSProblem problem = new WeightLimitPNSProblem();
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

    ExtendedOperatingUnit O1 = new ExtendedOperatingUnit("O1", inputs: [mC], outputs: [mA, mF], cost: 34, weight: 8);
    ExtendedOperatingUnit O2 = new ExtendedOperatingUnit("O2", inputs: [mD], outputs: [mA, mB], cost: 66, weight: 4);
    ExtendedOperatingUnit O3 = new ExtendedOperatingUnit("O3", inputs: [mE, mF], outputs: [mC], cost: 12, weight: 5);
    ExtendedOperatingUnit O4 = new ExtendedOperatingUnit("O4", inputs: [mF, mG], outputs: [mC, mD], cost: 87, weight: 2);
    ExtendedOperatingUnit O5 = new ExtendedOperatingUnit("O5", inputs: [mG, mH], outputs: [mD], cost: 20, weight: 9);
    ExtendedOperatingUnit O6 = new ExtendedOperatingUnit("O6", inputs: [mJ], outputs: [mF], cost: 78, weight: 7);
    ExtendedOperatingUnit O7 = new ExtendedOperatingUnit("O7", inputs: [mK, mL], outputs: [mH], cost: 47, weight: 1);

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