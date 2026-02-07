using Google.OrTools.LinearSolver;
using PGraphInCS;
using PGraphInCS.LinearPNS.Efficient;

// We also need a new LP model to optimize for environmental impact. There is no built-in codebase to make this simple, but we can copy the built-in SimpleLinearPNSLPModel class, and modify that.
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
        _modelSolver = Solver.CreateSolver("SCIP");
        MaterialSet materialsToWorkWith = unitsToWorkWith.Inputs().Union(unitsToWorkWith.Outputs());

        // Get the sum of the already included fix costs, as we can use that for sharper cost limit
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

        // Material flow rate constraints
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

        // Operating unit capacity constraints, cost limit constraint, and CO2 production. Since materials don't have variables, material costs and CO2 productions have to be integrated into operating unit coefficients.
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

            // Let's make a proper calculation here: consumed raw materials and leftover intermediates and products all count, if they have CO2 production assigned
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
