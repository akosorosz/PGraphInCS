using PGraphInCS;
using PGraphInCS.LinearPNS.Efficient;

/*
 * This sample code loads a linear PNS problem, extends it with environmental impact data (CO2) production the quick way, and determines the ranked list of solutions.
 * Here, the CO2 production will be the main objective, while there is an upper bound for the cost of the network.
 */

// First, load the problem
LinearPNSProblem problem = LinearPNSProblem.FromPGraphStudioFile("sevenunit.pgsx");

// Add proportional CO2 production data for raw materials and operating units
problem.Materials["E"].AdditionalParameters.Add("CO2", 1.2);
problem.Materials["G"].AdditionalParameters.Add("CO2", 1.9);
problem.Materials["J"].AdditionalParameters.Add("CO2", 2.5);
problem.Materials["K"].AdditionalParameters.Add("CO2", 0.8);
problem.Materials["L"].AdditionalParameters.Add("CO2", 2.1);

problem.OperatingUnits["O1"].AdditionalParameters.Add("CO2", 3.9);
problem.OperatingUnits["O2"].AdditionalParameters.Add("CO2", 3.2);
problem.OperatingUnits["O3"].AdditionalParameters.Add("CO2", 2.6);
problem.OperatingUnits["O4"].AdditionalParameters.Add("CO2", 2.1);
problem.OperatingUnits["O5"].AdditionalParameters.Add("CO2", 3.1);
problem.OperatingUnits["O6"].AdditionalParameters.Add("CO2", 0.2);
problem.OperatingUnits["O7"].AdditionalParameters.Add("CO2", 5.9);

// PNS problem classes also have an AdditionalParameters dictionary, so no need to make a new class for a single change (especially since then the FromPGraphStudioFile method would not work right away).
problem.AdditionalParameters.Add("costUpperBound", 1000.0);

// To consider CO2 production in the objective function, we need to make a new LP model. The cost limit should also be included in the LP model.

// The bounding method needs to call the new LP model and interpret the result. Again, we can start from the built-in LinearSubproblemBoundEfficient method.
CO2FocusedLinearNetwork? boudingWithCO2<SubproblemType>(SubproblemType subproblem)
    where SubproblemType : SubproblemBase<LinearPNSProblem>, ISubpoblemWithIncludedExcludedGet
{
    // If it is a leaf suproblem, only the included units can be considered. For intermediate suproblems, the model can work with the undecided operating units as well.
    OperatingUnitSet unitsToUseInLp = subproblem.IsLeaf ? subproblem.GetIncludedUnits() : subproblem.Problem.OperatingUnits.Except(subproblem.GetExcludedUnits());
    CO2FocusedLPModel lpmodel = new CO2FocusedLPModel(subproblem.Problem, unitsToUseInLp, subproblem.GetIncludedUnits());
    if (!lpmodel.Solve())
    {
        return null;
    }
    // Eliminate redundant solutions (or don't, if you want to see networks with 0-sized operating units)
    if (subproblem.IsLeaf && unitsToUseInLp.Any(u => lpmodel.GetOptimizedCapacity(u) < 0.00001))
    {
        return null;
    }
    double totalcost = lpmodel.GetCost();
    double co2production = lpmodel.GetCO2Production();
    return new CO2FocusedLinearNetwork(unitsToUseInLp.ToDictionary(u => u, u => lpmodel.GetOptimizedCapacity(u)), co2production, totalcost);
}

// Finally, solving the problem is simple
var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<LinearPNSProblem, CO2FocusedLinearNetwork>(problem, boudingWithCO2, maxSolutions: -1);
foreach (var network in abb.GetSolutionNetworks())
{
    Console.WriteLine($"CO2 production: {network.CO2Production}, cost: {network.Cost}");
    foreach (var (unit, capacity) in network.UnitCapacities)
    {
        Console.WriteLine($"    {capacity}*{unit.Name}");
    }
}