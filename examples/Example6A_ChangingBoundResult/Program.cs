using PGraphInCS;
using PGraphInCS.LinearPNS.Efficient;

/*
 * This sample code loads a linear PNS problem, extends it with environmental impact data (CO2) production the quick way, and determines the ranked list of solutions.
 * Here, the CO2 production is calculated for each network but it does not affect the solution process (i.e., cost is still the objective function)
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

/*
 * For solving the problem, cost is still the objective function, and the built-in ABB method is perferctly fine.
 * However, we need the add the CO2 production to the resulting networks and calculate it for each solution.
 * This requires a new network class and a new bounding method.
 */

// The bounding method chooses a simple path: call the built-in linear bounding and transform the result. It is also generic, so it can work with most algorithms.
LinearNetworkWithCO2? boudingWithCO2<SubproblemType>(SubproblemType subproblem)
    where SubproblemType : SubproblemBase<LinearPNSProblem>, ISubpoblemWithIncludedExcludedGet
{
    LinearNetwork? originalResult = CommonImplementations.LinearSubproblemBoundEfficient(subproblem);
    if (originalResult == null) return null;

    double co2production = 0.0;
    // Only calculate CO2 production for leaf suproblems (which actually represent solutions). It is not needed for intermediate subproblems, so don't waste computation time on it.
    if (subproblem.IsLeaf)
    {
        // CO2 production needs to be calculated for the included operating units and the consumed raw materials. The raw material consumption can only be accessed indirectly through the operating units.
        // Let's collect the consumption of each material. This will not be a complete material balance, as CO2 production is only considered for raw materials, which cannot be produced, only consumed. If the problem is extended and CO2 production is considered for other materials as well, this method needs to be revisited.
        Dictionary<MaterialNode, double> materialConsumption = new Dictionary<MaterialNode, double>();
        foreach (var (unit, capacity) in originalResult.UnitCapacities)
        {
            // Let's also check if the CO2 production is given. If not, default to 0.
            if (unit.AdditionalParameters.TryGetValue("CO2", out object? value))
            {
                co2production += capacity * (double)value;
            }
            foreach (var (material, flowrate) in (unit as LinearOperatingUnitNode)!.InputRatios)
            {
                if (!materialConsumption.ContainsKey(material))
                    materialConsumption.Add(material, flowrate * capacity);
                else
                    materialConsumption[material] += flowrate * capacity;
            }
        }
        // Finally, calculate the CO2 production from the collected material consumptions.
        foreach (var (material, consumption) in materialConsumption)
        {
            if (material.AdditionalParameters.TryGetValue("CO2", out object? value))
            {
                co2production += consumption * (double)value;
            }
        }
    }

    return new LinearNetworkWithCO2(originalResult.UnitCapacities, originalResult.ObjectiveValue, co2production);
}

// Now, solving the problem is simple
var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<LinearPNSProblem, LinearNetworkWithCO2>(problem, boudingWithCO2, maxSolutions: -1);
foreach (var network in abb.GetSolutionNetworks())
{
    Console.WriteLine($"cost: {network.ObjectiveValue}, CO2 production: {network.CO2Production}");
    foreach (var (unit, capacity) in network.UnitCapacities)
    {
        Console.WriteLine($"    {capacity}*{unit.Name}");
    }
}