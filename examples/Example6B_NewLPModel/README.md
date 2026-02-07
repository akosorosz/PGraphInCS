# Example 6B: Creating a new LP model for linear PNS

This sample code loads a linear PNS problem, extends it with environmental impact data (CO2) production the quick way, and determines the ranked list of solutions.
Here, the CO2 production will be the main objective, while there is an upper bound for the cost of the network.

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

PNS problem classes also have an AdditionalParameters dictionary, so no need to make a new class for a single change (especially since then the FromPGraphStudioFile method would not work right away).

    problem.AdditionalParameters.Add("costUpperBound", 1000.0);

To consider CO2 production in the objective function, we need to make a new LP model. The cost limit should also be included in the LP model.
There is no built-in codebase to make this simple, but we can copy the built-in SimpleLinearPNSLPModel class, and modify that.

    public class CO2FocusedLPModel
    {
        Dictionary<OperatingUnitNode, Variable> _unitSizeVars;
        Dictionary<MaterialNode, Constraint> _materialConstraints;
        Constraint _costConstraint;
        Objective _objective;
        Solver _modelSolver;
        double _includedFixCosts;
    
        bool _solved = false;
    
        public CO2FocusedLPModel(LinearPNSProblem problem, OperatingUnitSet unitsToWorkWith, OperatingUnitSet unitsAlreadyIncluded)
        {
            //...
        }
    
        public bool Solve()
        {
            Solver.ResultStatus resultStatus = _modelSolver.Solve();
            _solved = true;
            return resultStatus == Solver.ResultStatus.OPTIMAL;
        }
    
        public double GetCO2Production()
        {
            if (!_solved)
            {
                Solve();
            }
            return _objective.Value();
        }
    
        public double GetCost()
        {
            if (!_solved)
            {
                Solve();
            }
            return _includedFixCosts + _unitSizeVars.Values.Sum(unitvar => _costConstraint.GetCoefficient(unitvar) * unitvar.SolutionValue());
        }
    
        public double GetOptimizedCapacity(OperatingUnitNode unit)
        {
            if (!_solved)
            {
                Solve();
            }
            return _unitSizeVars[unit].SolutionValue();
        }
    }

Building the model takes several steps. First, there are the initializations. Here we also get the sum of the already included fix costs, as we can use that for sharper cost limit

    _modelSolver = Solver.CreateSolver("SCIP");
    MaterialSet materialsToWorkWith = unitsToWorkWith.Inputs().Union(unitsToWorkWith.Outputs());
    
    _includedFixCosts = unitsAlreadyIncluded.Cast<LinearOperatingUnitNode>().Sum(u => u.FixOperatingCost + u.FixInvestmentCost / u.PayoutPeriod);
    
    if (problem.AdditionalParameters.TryGetValue("costUpperBound", out var value))
    {
        _costConstraint = _modelSolver.MakeConstraint(0, (double)value - _includedFixCosts, "cost");
    }
    else
    {
        _costConstraint = _modelSolver.MakeConstraint("cost");
    }
    
    _objective = _modelSolver.Objective();
    _objective.SetMinimization();
    
We have a constraint for each material which need the proper lower and upper bounds. There are no variables for the materials in this model.
    
    _materialConstraints = new();
    foreach (var material in materialsToWorkWith.Cast<LinearMaterialNode>())
    {
        if (problem.RawMaterials.Contains(material))
        {
            _materialConstraints.Add(material, _modelSolver.MakeConstraint(-material.FlowRateUpperBound, -material.FlowRateLowerBound, "m_" + material.Name));
        }
        else
        {
            _materialConstraints.Add(material, _modelSolver.MakeConstraint(material.FlowRateLowerBound, material.FlowRateUpperBound, "m_" + material.Name));
        }
    }
    
Operating unit capacity constraints, cost limit constraint, and CO2 production are next. Since materials don't have variables, material costs and CO2 productions have to be integrated into operating unit coefficients.
Let's make a proper calculation of CO2 production here: consumed raw materials and leftover intermediates and products all count, if they have CO2 production assigned. We need to watch out for the signs.
    
    _unitSizeVars = new();
    foreach (var unit in unitsToWorkWith.Cast<LinearOperatingUnitNode>())
    {
        var unitVar = _modelSolver.MakeNumVar(unit.CapacityLowerBound, unit.CapacityUpperBound, "x_" + unit.Name);
        _unitSizeVars.Add(unit, unitVar);
        double realUnitCost = unit.ProportionalOperatingCost + unit.ProportionalInvestmentCost / unit.PayoutPeriod;
        foreach (var (material, ratio) in unit.InputRatios)
        {
            _materialConstraints[material].SetCoefficient(unitVar, -ratio);
            realUnitCost += ratio * (material as LinearMaterialNode)!.Price;
        }
        foreach (var (material, ratio) in unit.OutputRatios)
        {
            _materialConstraints[material].SetCoefficient(unitVar, ratio);
            realUnitCost -= ratio * (material as LinearMaterialNode)!.Price;
        }
        _costConstraint.SetCoefficient(unitVar, realUnitCost);
    
        double realUnitCO2 = (double)unit.AdditionalParameters.GetValueOrDefault("CO2", 0.0);
        foreach (var (material, ratio) in unit.InputRatios)
        {
            if (problem.RawMaterials.Contains(material))
                realUnitCO2 += ratio * (double)material.AdditionalParameters.GetValueOrDefault("CO2", 0.0);
            else
                realUnitCO2 -= ratio * (double)material.AdditionalParameters.GetValueOrDefault("CO2", 0.0);
        }
        foreach (var (material, ratio) in unit.OutputRatios)
        {
            realUnitCO2 += ratio * (double)material.AdditionalParameters.GetValueOrDefault("CO2", 0.0);
        }
        _objective.SetCoefficient(unitVar, realUnitCO2);
    }

We also need a new network class to represent our full or partial solutions. There is no need to base the new network class on LinearNetwork, so let's see an example for building it from scratch.
We expect operating units with capacities in the constructor. The base class (NetworkBase) contains the set of operating units, so we need to set that.
Ordering of these networks by default should be based on CO2 production first, cost second.

    public class CO2FocusedLinearNetwork : NetworkBase, IComparable<CO2FocusedLinearNetwork>
    {
        public Dictionary<OperatingUnitNode, double> UnitCapacities { get; } = new();
        public double CO2Production { get; set; }
        public double Cost { get; set; }
    
        public CO2FocusedLinearNetwork(Dictionary<OperatingUnitNode, double> capacities, double co2Production, double cost) :
            base(new OperatingUnitSet(capacities.Keys))
        {
            UnitCapacities = capacities;
            CO2Production = co2Production;
            Cost = cost;
        }

        public int CompareTo(CO2FocusedLinearNetwork? other)
        {
            if (other == null) return 1;
            if (this.CO2Production.CompareTo(other.CO2Production) != 0) return this.CO2Production.CompareTo(other.CO2Production);
            return this.Cost.CompareTo(other.Cost);
        }
    }

The bounding method needs to call the new LP model and interpret the result. Again, we can start from the built-in LinearSubproblemBoundEfficient method.
If a subproblem is a leaf suproblem, only the included units can be considered. For intermediate suproblems, the model can work with the undecided operating units as well.
We can also eliminate redundant solutions (or don't, if we want to see networks with 0-sized operating units)

    CO2FocusedLinearNetwork? boudingWithCO2<SubproblemType>(SubproblemType subproblem)
        where SubproblemType : SubproblemBase<LinearPNSProblem>, ISubpoblemWithIncludedExcludedGet
    {
        OperatingUnitSet unitsToUseInLp = subproblem.IsLeaf ? subproblem.GetIncludedUnits() : subproblem.Problem.OperatingUnits.Except(subproblem.GetExcludedUnits());
        CO2FocusedLPModel lpmodel = new CO2FocusedLPModel(subproblem.Problem, unitsToUseInLp, subproblem.GetIncludedUnits());
        if (!lpmodel.Solve())
        {
            return null;
        }
        if (subproblem.IsLeaf && unitsToUseInLp.Any(u => lpmodel.GetOptimizedCapacity(u) < 0.00001))
        {
            return null;
        }
        double totalcost = lpmodel.GetCost();
        double co2production = lpmodel.GetCO2Production();
        return new CO2FocusedLinearNetwork(unitsToUseInLp.ToDictionary(u => u, u => lpmodel.GetOptimizedCapacity(u)), co2production, totalcost);
    }

Finally, solving the problem is simple

    var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<LinearPNSProblem, CO2FocusedLinearNetwork>(problem, boudingWithCO2, maxSolutions: -1);
    foreach (var network in abb.GetSolutionNetworks())
    {
        Console.WriteLine($"CO2 production: {network.CO2Production}, cost: {network.Cost}");
        foreach (var (unit, capacity) in network.UnitCapacities)
        {
            Console.WriteLine($"    {capacity}*{unit.Name}");
        }
    }