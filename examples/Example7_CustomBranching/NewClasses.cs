using PGraphInCS;
using PGraphInCS.LinearPNS.Efficient;

// To make minimal changes, the new class is derived from CommonImplementations.BinaryDecisionSubproblem. The base class already contains the main logic (included, excluded, undecided operating units, error detection, leaf subproblem determination).
// However, every subproblem class needs to implement the ISubproblemInitializer interface. This interface contains a single, static method, InitializeRoot, which describes how the root subproblem is generated.
public class HeuristicBinarySubproblem<PNSProblemType> : CommonImplementations.BinaryDecisionSubproblem<PNSProblemType>, ISubproblemInitializer<PNSProblemType, HeuristicBinarySubproblem<PNSProblemType>>
    where PNSProblemType : PNSProblemBase
{
    // As extra, we store a reference to the bounding network. We will only use the operating units in the network, so no need to restrict its type more. If this is not null, then the bounding should ignore it.
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

// We also need a bounding wrapper, where we can call any appropriate bounding method and save its network to the subproblem. Furthermore, it needs to ignore bounding if the subproblem already has a network.
// To be more error prone, we always perform bounding on leaf subproblems.
// The modified bounding method need to cast, since the subproblem only contains a NetworkBase. Optionally, we could give the subproblem class a NetworkType generic parameter as well.
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