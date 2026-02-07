# Example 6A: Modify bounding result for linear PNS

This sample code loads a linear PNS problem, extends it with environmental impact data (CO2) production the quick way, and determines the ranked list of solutions.
Here, the CO2 production is calculated for each network but it does not affect the solution process (i.e., cost is still the objective function)

First, load the problem

    LinearPNSProblem problem = LinearPNSProblem.FromPGraphStudioFile("sevenunit.pgsx");

Add proportional CO2 production data for raw materials and operating units

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

For solving the problem, cost is still the objective function, and the built-in ABB method is perferctly fine.
However, we need the add the CO2 production to the resulting networks and calculate it for each solution.
This requires a new network class and a new bounding method.

The network is an extension of the built-in LinearNetwork

    public class LinearNetworkWithCO2 : LinearNetwork
    {
        public double CO2Production { get; set; }
        public LinearNetworkWithCO2(Dictionary<OperatingUnitNode, double> capacities, double objectiveValue, double co2Production) : base(capacities, objectiveValue)
        {
            CO2Production = co2Production;
        }
    }

The bounding method chooses a simple path: call the built-in linear bounding and transform the result. It is also generic, so it can work with most algorithms.
CO2 production needs to be calculated for the included operating units and the consumed raw materials. The raw material consumption can only be accessed indirectly through the operating units.

The method only calculates CO2 production for leaf suproblems (which actually represent solutions). It is not needed for intermediate subproblems, so don't waste computation time on it.
The method collects the consumption of each material into an auxiliary dictionary. This will not be a complete material balance, as CO2 production is only considered for raw materials, which cannot be produced, only consumed. If the problem is extended and CO2 production is considered for other materials as well, this method needs to be revisited.
The method also performs safety checks for the CO2 production data for materials and operating units. If not given, it defaults to 0.

    LinearNetworkWithCO2? boudingWithCO2<SubproblemType>(SubproblemType subproblem)
        where SubproblemType : SubproblemBase<LinearPNSProblem>, ISubpoblemWithIncludedExcludedGet
    {
        LinearNetwork? originalResult = CommonImplementations.LinearSubproblemBoundEfficient(subproblem);
        if (originalResult == null) return null;
    
        double co2production = 0.0;
        if (subproblem.IsLeaf)
        {
            Dictionary<MaterialNode, double> materialConsumption = new Dictionary<MaterialNode, double>();
            foreach (var (unit, capacity) in originalResult.UnitCapacities)
            {
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

Now, solving the problem is simple

    var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<LinearPNSProblem, LinearNetworkWithCO2>(problem, boudingWithCO2, maxSolutions: -1);
    foreach (var network in abb.GetSolutionNetworks())
    {
        Console.WriteLine($"cost: {network.ObjectiveValue}, CO2 production: {network.CO2Production}");
        foreach (var (unit, capacity) in network.UnitCapacities)
        {
            Console.WriteLine($"    {capacity}*{unit.Name}");
        }
    }