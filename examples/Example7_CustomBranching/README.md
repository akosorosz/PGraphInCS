# Example 7: Writing custom branching logic

This sample code shows an example for creating a custom branching logic. The introduced logic will not be completely new, instead a modification of an existing one.
The new branching algorithm will be a heuristic improvement of the built-in binary branching. We modify the subproblem class to include the optimized network. The bounding method will save the generated network back to the subproblem, which then can use the bounding result to guide the search.
We also add another heuristic where, if the branching decision fits the parent subproblem's bounding network, the new bound should be exactly the same. As such, it can be ignored.

    LinearPNSProblem problem = LinearPNSProblem.FromPGraphStudioFile("sevenunit.pgsx");

To make minimal changes, the new class is derived from CommonImplementations.BinaryDecisionSubproblem. The base class already contains the main logic (included, excluded, undecided operating units, error detection, leaf subproblem determination).
However, every subproblem class needs to implement the ISubproblemInitializer interface. This interface contains a single, static method, **InitializeRoot**, which describes how the root subproblem is generated.
As extra, we store a reference to the bounding network. We will only use the operating units in the network, so no need to restrict its type more. If this is not null, then the bounding should ignore it.

    public class HeuristicBinarySubproblem<PNSProblemType> : CommonImplementations.BinaryDecisionSubproblem<PNSProblemType>, ISubproblemInitializer<PNSProblemType, HeuristicBinarySubproblem<PNSProblemType>>
        where PNSProblemType : PNSProblemBase
    {
        public NetworkBase? BoundNetwork { get; set; }
        public HeuristicBinarySubproblem(PNSProblemType problem, OperatingUnitSet? baseUnitSet, OperatingUnitSet included, OperatingUnitSet excluded, NetworkBase? bountNetwork = null) : base(problem, baseUnitSet, included, excluded)
        {
            BoundNetwork = bountNetwork;
        }
    
        public static new HeuristicBinarySubproblem<PNSProblemType> InitializeRoot(PNSProblemType problem, OperatingUnitSet? baseUnitSet)
        {
            OperatingUnitSet unitToConsider = baseUnitSet != null ? baseUnitSet : problem.OperatingUnits;
            return new HeuristicBinarySubproblem<PNSProblemType>(problem, baseUnitSet, new OperatingUnitSet(), problem.OperatingUnits.Except(unitToConsider));
        }
    }

Branching algorithms always take a subproblem as parameter, and return a collection of subproblems. They migth support lazy evaluation. It is suggested to use yield return to enable lazy evaluation in case the branch-and-bound algorithm allows it.
If there is a network in the subproblem, and it contains operating units that are undecided in the subproblem, we select one of those for branching. If not, we select any undecided operating unit.
If we select a unit based on the network, and the mutual exclusions do not invalidate the network in the next step, then we don't need bounding for the generated subproblem where the choosen unit is included. We always need new bound in the case it is excluded.

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

We also need a bounding wrapper, where we can call any appropriate bounding method and save its network to the subproblem. Furthermore, it needs to ignore bounding if the subproblem already has a network.
To be more error prone, we always perform bounding on leaf subproblems.
The modified bounding method need to cast, since the subproblem only contains a NetworkBase. Optionally, we could give the subproblem class a NetworkType generic parameter as well.

    public class BoundWrapper<PNSProblemType, NetworkType>
        where PNSProblemType : PNSProblemBase
        where NetworkType : NetworkBase
    {
        Func<HeuristicBinarySubproblem<PNSProblemType>, NetworkType?> _originalBoudingMethod;
    
        public BoundWrapper(Func<HeuristicBinarySubproblem<PNSProblemType>, NetworkType?> originalBoudingMethod)
        {
            _originalBoudingMethod = originalBoudingMethod;
        }
    
        public NetworkType? boundingMethod(HeuristicBinarySubproblem<PNSProblemType> subproblem)
        {
            if (!subproblem.IsLeaf && subproblem.BoundNetwork != null)
            {
                return subproblem.BoundNetwork as NetworkType;
            }
            else
            {
                NetworkType? network = _originalBoudingMethod(subproblem);
                subproblem.BoundNetwork = network;
                return network;
            }
        }
    }

Solving the problem is just puting the parts together.

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
