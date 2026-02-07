using PGraphInCS;
using PGraphInCS.LinearPNS.Efficient;

/*
 * This sample code shows an example for creating a custom branching logic. The introduced logic will not be completely new, instead a modification of an existing one.
 * The new branching algorithm will be a heuristic improvement of the built-in binary branching. We modify the subproblem class to include the optimized network. The bounding method will save the generated network back to the subproblem, which then can use the bounding result to guide the search.
 * We also add another heuristic where, if the branching decision fits the parent subproblem's bounding network, the new bound should be exactly the same. As such, it can be ignored.
 */

LinearPNSProblem problem = LinearPNSProblem.FromPGraphStudioFile("sevenunit.pgsx");

// Branching algorithms always take a subproblem as parameter, and return a collection of subproblems. They migth support lazy evaluation. It is suggested to use yield return to enable lazy evaluation in case the branch-and-bound algorithm allows it.
// If there is a network in the subproblem, and it contains operating units that are undecided in the subproblem, we select one of those for branching. If not, we select any undecided operating unit.
// If we select a unit based on the network, and the mutual exclusions do not invalidate the network in the next step, then we don't need bounding for the generated subproblem where the choosen unit is included. We always need new bound in the case it is excluded.
IEnumerable<HeuristicBinarySubproblem<PNSProblemType>> heuristicBranching<PNSProblemType>(HeuristicBinarySubproblem<PNSProblemType> subproblem)
    where PNSProblemType : PNSProblemBase
{
    OperatingUnitNode unit = subproblem.Undecided.First();
    bool runBoudingInIncludedBranch = true;
    if (subproblem.BoundNetwork != null && subproblem.BoundNetwork.Units != null)
    {
        OperatingUnitSet undecidedInNetwork = subproblem.BoundNetwork.Units.Intersect(subproblem.Undecided);
        if (undecidedInNetwork.Count > 0)
        {
            unit = undecidedInNetwork.First();
            if (subproblem.Problem.MutuallyExclusiveUnits[unit].Intersect(subproblem.BoundNetwork.Units).Count == 0)
                runBoudingInIncludedBranch = false;
        }
    }
    
    if (runBoudingInIncludedBranch)
        yield return new HeuristicBinarySubproblem<PNSProblemType>(subproblem.Problem, subproblem.BaseUnitSet, subproblem.Included.Union(unit), subproblem.Excluded.Union(subproblem.Problem.MutuallyExclusiveUnits[unit]));
    else
        yield return new HeuristicBinarySubproblem<PNSProblemType>(subproblem.Problem, subproblem.BaseUnitSet, subproblem.Included.Union(unit), subproblem.Excluded.Union(subproblem.Problem.MutuallyExclusiveUnits[unit]), subproblem.BoundNetwork);

    yield return new HeuristicBinarySubproblem<PNSProblemType>(subproblem.Problem, subproblem.BaseUnitSet, subproblem.Included, subproblem.Excluded.Union(unit));
}

// Solving the problem is just puting the parts together.
var boundWrapper = new BoundWrapper<LinearPNSProblem, LinearNetwork>(CommonImplementations.LinearSubproblemBoundEfficient);
var bnb = new OrderedOpenListBranchAndBoundAlgorithm<LinearPNSProblem, HeuristicBinarySubproblem<LinearPNSProblem>, LinearNetwork>(problem, heuristicBranching, boundWrapper.boundingMethod, maxSolutions: -1);
foreach (var network in bnb.GetSolutionNetworks())
{
    Console.WriteLine($"Cost: {network.ObjectiveValue}");
    foreach (var (unit, capacity) in network.UnitCapacities)
    {
        Console.WriteLine($"    {capacity}*{unit.Name}");
    }
}