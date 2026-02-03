using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PGraphInCS;

/// <summary>
/// Static class containing classes and methods implementing common algorithms.
/// </summary>
public static class CommonImplementations
{
    /// <summary>
    /// Subproblem specifically designed for Algorithm ABB of the P-graph framework (choose a material, decide which units to include to produce it)
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    public class ABBSubproblem<PNSProblemType> : SubproblemBase<PNSProblemType>, ISubpoblemWithIncludedExcludedGetSet, ISubproblemInitializer<PNSProblemType, ABBSubproblem<PNSProblemType>>
    where PNSProblemType : PNSProblemBase
    {
        public MaterialSet ToBeProduced { get; set; }
        public MaterialSet AlreadyProduced { get; set; }
        public OperatingUnitSet Included { get; set; }
        public OperatingUnitSet Excluded { get; set; }
        public ABBSubproblem(PNSProblemType problem, OperatingUnitSet? baseUnitSet, MaterialSet toBeProduced, MaterialSet alreadyProduced, OperatingUnitSet included, OperatingUnitSet excluded) : base(problem, baseUnitSet)
        {
            ToBeProduced = toBeProduced.Clone();
            AlreadyProduced = alreadyProduced.Clone();
            Included = included.Clone();
            Excluded = excluded.Clone();
        }
        public override bool IsLeaf => ToBeProduced.Count == 0;
        public override bool IsErrorFree => Included.Intersect(Excluded).Any() == false && Problem.MaterialsWithParallelProductionLimit.All(m => Problem.Producers[m].Intersect(Included).Count <= Problem.MaxParallelProduction[m]);
        public static ABBSubproblem<PNSProblemType> InitializeRoot(PNSProblemType problem, OperatingUnitSet? baseUnitSet)
        {
            OperatingUnitSet unitToConsider = baseUnitSet != null ? baseUnitSet : problem.OperatingUnits;
            return new ABBSubproblem<PNSProblemType>(problem, baseUnitSet, problem.Products, new MaterialSet(), new OperatingUnitSet(), problem.OperatingUnits.Except(unitToConsider));
        }

        public OperatingUnitSet GetIncludedUnits() => Included;
        public OperatingUnitSet GetExcludedUnits() => Excluded;
        public void SetIncludedUnits(OperatingUnitSet units) => Included = units.Clone();
        public void SetExcludedUnits(OperatingUnitSet units) => Excluded = units.Clone();
    }

    /// <summary>
    /// Branching algorithm specifically designed for Algorithm ABB of the P-graph framework (choose a material, decide which units to include to produce it)
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    /// <param name="subproblem">Subproblem to be branched</param>
    /// <returns>Collection of child subproblems</returns>
    public static IEnumerable<ABBSubproblem<PNSProblemType>> ABBBranching<PNSProblemType>(ABBSubproblem<PNSProblemType> subproblem)
        where PNSProblemType : PNSProblemBase
    {
        MaterialNode selectedMaterial = subproblem.ToBeProduced.First();
        OperatingUnitSet canProduceSelectedMaterial = subproblem.Problem.Producers[selectedMaterial];
        OperatingUnitSet canProduceSelectedMaterialNew = canProduceSelectedMaterial.Except(subproblem.Included);
        OperatingUnitSet alreadyProducingMaterial = canProduceSelectedMaterial.Intersect(subproblem.Included);
        bool ignoreEmptySet = alreadyProducingMaterial.Count == 0;
        int maxparallel = subproblem.Problem.MaxParallelProduction[selectedMaterial];
        if (maxparallel != -1 && alreadyProducingMaterial.Count > maxparallel)
        {
            yield break;
        }
        IEnumerable<OperatingUnitSet> pwset;
        if (maxparallel == -1) pwset = NodePowerSet<OperatingUnitNode>.GetPowerSet(canProduceSelectedMaterialNew);
        else pwset = NodePowerSet<OperatingUnitNode>.GetPowerSet(canProduceSelectedMaterialNew, maxparallel - alreadyProducingMaterial.Count);
        foreach (OperatingUnitSet newUnits in pwset)
        {
            if (newUnits.Count == 0 && ignoreEmptySet) continue;
            OperatingUnitSet units = newUnits.Union(alreadyProducingMaterial);
            if (subproblem.Included.Intersect(canProduceSelectedMaterialNew.Except(units)).Count == 0 &&
                subproblem.Excluded.Intersect(units).Count == 0)
            {
                OperatingUnitSet newIncluded = subproblem.Included.Union(units);
                OperatingUnitSet newExcluded = subproblem.Excluded.Union(canProduceSelectedMaterialNew.Except(units)).Union(subproblem.Problem.MutuallyExclusiveWith(units));
                if (newIncluded.Intersect(newExcluded).Count == 0)
                {
                    yield return new ABBSubproblem<PNSProblemType>(subproblem.Problem, subproblem.BaseUnitSet,
                                    subproblem.ToBeProduced.Union(subproblem.Problem.InputsOf(units))
                                        .Except(subproblem.Problem.RawMaterials.Union(subproblem.AlreadyProduced).Union(selectedMaterial)),
                                    subproblem.AlreadyProduced.Union(selectedMaterial),
                                    newIncluded,
                                    newExcluded);
                }
            }
        }
    }

    /// <summary>
    /// Subproblem specifically designed for branching base on binary decisions (choose an operating unit, either include it or exclude it)
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    public class BinaryDecisionSubproblem<PNSProblemType> : SubproblemBase<PNSProblemType>, ISubpoblemWithIncludedExcludedGetSet, ISubproblemInitializer<PNSProblemType, BinaryDecisionSubproblem<PNSProblemType>>
    where PNSProblemType : PNSProblemBase
    {
        public OperatingUnitSet Included { get; set; }
        public OperatingUnitSet Excluded { get; set; }
        public OperatingUnitSet Undecided { get; set; }
        public BinaryDecisionSubproblem(PNSProblemType problem, OperatingUnitSet? baseUnitSet, OperatingUnitSet included, OperatingUnitSet excluded) : base(problem, baseUnitSet)
        {
            Included = included.Clone();
            Excluded = excluded.Clone();
            Undecided = problem.OperatingUnits.Except(included.Union(excluded));
        }
        public override bool IsLeaf => Undecided.Count == 0;
        public override bool IsErrorFree => Included.Intersect(Excluded).Any() == false && Problem.MaterialsWithParallelProductionLimit.All(m => Problem.Producers[m].Intersect(Included).Count <= Problem.MaxParallelProduction[m]);
        public static BinaryDecisionSubproblem<PNSProblemType> InitializeRoot(PNSProblemType problem, OperatingUnitSet? baseUnitSet)
        {
            OperatingUnitSet unitToConsider = baseUnitSet != null ? baseUnitSet : problem.OperatingUnits;
            return new BinaryDecisionSubproblem<PNSProblemType>(problem, baseUnitSet, new OperatingUnitSet(), problem.OperatingUnits.Except(unitToConsider));
        }

        public OperatingUnitSet GetIncludedUnits() => Included;
        public OperatingUnitSet GetExcludedUnits() => Excluded;
        public void SetIncludedUnits(OperatingUnitSet units)
        {
            Included = units.Clone();
            Undecided = Problem.OperatingUnits.Except(Included.Union(Excluded));
        }
        public void SetExcludedUnits(OperatingUnitSet units)
        {
            Excluded = units.Clone();
            Undecided = Problem.OperatingUnits.Except(Included.Union(Excluded));
        }
    }

    /// <summary>
    /// Branching algorithm specifically designed for branching base on binary decisions (choose an operating unit, either include it or exclude it)
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    /// <param name="subproblem">Subproblem to be branched</param>
    /// <returns>Collection of child subproblems</returns>
    public static IEnumerable<BinaryDecisionSubproblem<PNSProblemType>> BinaryDecisionBranching<PNSProblemType>(BinaryDecisionSubproblem<PNSProblemType> subproblem)
        where PNSProblemType : PNSProblemBase
    {
        OperatingUnitNode unit = subproblem.Undecided.First();
        yield return new BinaryDecisionSubproblem<PNSProblemType>(subproblem.Problem, subproblem.BaseUnitSet, subproblem.Included.Union(unit), subproblem.Excluded.Union(subproblem.Problem.MutuallyExclusiveUnits[unit]));
        yield return new BinaryDecisionSubproblem<PNSProblemType>(subproblem.Problem, subproblem.BaseUnitSet, subproblem.Included, subproblem.Excluded.Union(unit));
    }

    /// <summary>
    /// Reduced Structure Generator (RSG) algorithm to reduce the free part during branching. Can be employed on any subproblem which keeps track of the included and the excluded operating units.
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    /// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
    /// <param name="subproblem">Subproblem (is modified by the method)</param>
    public static void ReducedStructureGenerator<PNSProblemType, SubproblemType>(SubproblemType subproblem)
        where PNSProblemType : PNSProblemBase
        where SubproblemType : SubproblemBase<PNSProblemType>, ISubpoblemWithIncludedExcludedGetSet
    {
        OperatingUnitSet excluded = subproblem.GetExcludedUnits().Clone();
        OperatingUnitSet baseSet = subproblem.Problem.OperatingUnits.Except(excluded);
        AlgorithmMSG msg = new AlgorithmMSG(subproblem.Problem,baseSet);
        OperatingUnitSet rsgUnits = msg.GetMaximalStructure();
        excluded.UnionWith(baseSet.Except(rsgUnits));
        subproblem.SetExcludedUnits(excluded);
    }

    /// <summary>
    /// Neutral extension algorithm to reduce the free part during branching. Can be employed on any subproblem which keeps track of the included and the excluded operating units.
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    /// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
    /// <param name="subproblem">Subproblem (is modified by the method)</param>
    public static void NeutralExtension<PNSProblemType, SubproblemType>(SubproblemType subproblem)
        where PNSProblemType : PNSProblemBase
        where SubproblemType : SubproblemBase<PNSProblemType>, ISubpoblemWithIncludedExcludedGetSet
    {
        OperatingUnitSet included = subproblem.GetIncludedUnits().Clone();
        OperatingUnitSet excluded = subproblem.GetExcludedUnits().Clone();
        OperatingUnitSet notExcluded = subproblem.Problem.OperatingUnits.Except(subproblem.GetExcludedUnits());
        bool change = true;
        while (change)
        {
            change = false;
            MaterialSet notProducedMaterials = included.Inputs().Union(subproblem.Problem.Products).Except(included.Outputs().Union(subproblem.Problem.RawMaterials));
            foreach (MaterialNode material in notProducedMaterials)
            {
                OperatingUnitSet canProduce = notExcluded.Producing(material);
                if (canProduce.Count == 1 && !included.Contains(canProduce.First()))
                {
                    included.Add(canProduce.First());
                    excluded.UnionWith(subproblem.Problem.MutuallyExclusiveUnits[canProduce.First()]);
                    change = true;
                }
            }
        }
        subproblem.SetIncludedUnits(included);
        subproblem.SetExcludedUnits(excluded);
    }

    /// <summary>
    /// Default branching extensions suggested to use with subproblems which keep track of the included and the excluded operating units.
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    /// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
    /// <returns>List of branching extensions</returns>
    public static List<Action<SubproblemType>> DefaultBranchingExtensions<PNSProblemType, SubproblemType>()
        where PNSProblemType : PNSProblemBase
        where SubproblemType : SubproblemBase<PNSProblemType>, ISubpoblemWithIncludedExcludedGetSet
    {
        return [
            ReducedStructureGenerator<PNSProblemType, SubproblemType>,
            NeutralExtension<PNSProblemType, SubproblemType>
        ];
    }

    /// <summary>
    /// Neutral extension algorithm specifically for Algorithm ABB.
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    /// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
    /// <param name="subproblem">Subproblem (is modified by the method)</param>
    public static void NeutralExtensionForABB<PNSProblemType, SubproblemType>(SubproblemType subproblem)
        where PNSProblemType : PNSProblemBase
        where SubproblemType : ABBSubproblem<PNSProblemType>
    {
        bool change = true;
        while (change)
        {
            change = false;
            foreach (MaterialNode material in subproblem.ToBeProduced)
            {
                OperatingUnitSet canProduce = subproblem.Problem.ProducersOf(material).Except(subproblem.Excluded);
                OperatingUnitSet canProduceNew = canProduce.Except(subproblem.Included);
                if (canProduce.Count == 0)
                {
                    return;
                    // This means that the subproblem has no feasbile leaf descendants (this material must be produced, but no operating units left to do so). The subproblem must remain, but no sense to make further changes, so for now just leave it like this for future branching steps to discover.
                    // TODO: Maybe find some better solution for this.
                }
                else if (canProduceNew.Count == 0)
                {
                    subproblem.AlreadyProduced.Add(material);
                    subproblem.ToBeProduced.Remove(material);
                    change = true;
                    break;
                }
                else if (canProduce.Count == 1 && canProduceNew.Count == 1)
                {
                    subproblem.AlreadyProduced.Add(material);
                    subproblem.ToBeProduced.UnionWith(subproblem.Problem.InputsOf(canProduceNew));
                    subproblem.ToBeProduced.ExceptWith(subproblem.Problem.RawMaterials.Union(subproblem.AlreadyProduced));
                    subproblem.Included.UnionWith(canProduceNew);
                    subproblem.Excluded.UnionWith(subproblem.Problem.MutuallyExclusiveWith(canProduceNew));
                    change = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Default branching extensions suggested for Algorithm ABB.
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    /// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
    /// <returns>List of branching extensions</returns>
    public static List<Action<SubproblemType>> DefaultBranchingExtensionsForABB<PNSProblemType, SubproblemType>()
        where PNSProblemType : PNSProblemBase
        where SubproblemType : ABBSubproblem<PNSProblemType>
    {
        return [
            ReducedStructureGenerator<PNSProblemType, SubproblemType>,
            NeutralExtensionForABB<PNSProblemType, SubproblemType>
        ];
    }

    /// <summary>
    /// Default bounding method the linear P-graphs (as used in P-Graph Studio). This method is for the implementation in LinearPNS.Efficient
    /// </summary>
    /// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
    /// <param name="subproblem">Subproblem to perform bouding and feasibility test on</param>
    /// <returns>Network representing the optimized, bounded solution of the subproblem, or null, if the subproblem is infeasible.</returns>
    public static LinearPNS.Efficient.LinearNetwork? LinearSubproblemBoundEfficient<SubproblemType>(SubproblemType subproblem)
        where SubproblemType : SubproblemBase<LinearPNS.Efficient.LinearPNSProblem>, ISubpoblemWithIncludedExcludedGet
    {
        OperatingUnitSet unitsToUseInLp = subproblem.IsLeaf ? subproblem.GetIncludedUnits() : subproblem.Problem.OperatingUnits.Except(subproblem.GetExcludedUnits());
        LinearPNS.Efficient.SimpleLinearPNSLPModel lpmodel = new LinearPNS.Efficient.SimpleLinearPNSLPModel(subproblem.Problem, unitsToUseInLp);
        if (!lpmodel.Solve())
        {
            return null;
        }
        // Eliminate redundant solutions
        if (subproblem.IsLeaf && unitsToUseInLp.Any(u => lpmodel.GetOptimizedCapacity(u) < 0.00001))
        {
            return null;
        }
        double fixedcost = subproblem.GetIncludedUnits().Cast<LinearPNS.Efficient.LinearOperatingUnitNode>().Sum(u => u.FixOperatingCost + u.FixInvestmentCost / u.PayoutPeriod);
        double totalcost = fixedcost + lpmodel.ObjectiveValue();
        return new LinearPNS.Efficient.LinearNetwork(unitsToUseInLp.ToDictionary(u => u, u => lpmodel.GetOptimizedCapacity(u)), totalcost);
    }

    /// <summary>
    /// Default bounding method the linear P-graphs (as used in P-Graph Studio). This method is for the implementation in LinearPNS.Flexible
    /// </summary>
    /// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
    /// <param name="subproblem">Subproblem to perform bouding and feasibility test on</param>
    /// <returns>Network representing the optimized, bounded solution of the subproblem, or null, if the subproblem is infeasible.</returns>
    public static LinearPNS.Flexible.LinearNetwork? LinearSubproblemBoundFlexible<SubproblemType>(SubproblemType subproblem)
        where SubproblemType : SubproblemBase<LinearPNS.Flexible.LinearPNSProblem>, ISubpoblemWithIncludedExcludedGet
    {
        OperatingUnitSet unitsToUseInLp = subproblem.IsLeaf ? subproblem.GetIncludedUnits() : subproblem.Problem.OperatingUnits.Except(subproblem.GetExcludedUnits());
        LinearPNS.Flexible.SimpleLinearPNSLPModel lpmodel = new LinearPNS.Flexible.SimpleLinearPNSLPModel(subproblem.Problem, unitsToUseInLp);
        if (!lpmodel.Solve())
        {
            return null;
        }
        // Eliminate redundant solutions
        if (subproblem.IsLeaf && unitsToUseInLp.Any(u => lpmodel.GetOptimizedCapacity(u) < 0.00001))
        {
            return null;
        }
        double fixedcost = subproblem.GetIncludedUnits().Select(u => subproblem.Problem.OperatingUnitData[u]).Sum(u => u.FixOperatingCost + u.FixInvestmentCost / u.PayoutPeriod);
        double totalcost = fixedcost + lpmodel.ObjectiveValue();
        return new LinearPNS.Flexible.LinearNetwork(unitsToUseInLp.ToDictionary(u => u, u => lpmodel.GetOptimizedCapacity(u)), totalcost);
    }

    /// <summary>
    /// Algorithm ABB with recursive function implementation.
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    /// <typeparam name="NetworkType">Type of the networks representing the solutions</typeparam>
    public class AlgorithmABBRecursive<PNSProblemType, NetworkType> : RecursiveBranchAndBoundAlgorithm<PNSProblemType, ABBSubproblem<PNSProblemType>, NetworkType>
        where PNSProblemType : PNSProblemBase
        where NetworkType : NetworkBase, IComparable<NetworkType>
    {

        public AlgorithmABBRecursive(PNSProblemType problem, Func<ABBSubproblem<PNSProblemType>, NetworkType?> boundingFunction, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null)
            : base(problem, CommonImplementations.ABBBranching, boundingFunction, maxSolutions, baseUnitSet, timeLimit)
        {
            this.SetBranchingExtensions(CommonImplementations.DefaultBranchingExtensionsForABB<PNSProblemType, ABBSubproblem<PNSProblemType>>());
        }
    }

    /// <summary>
    /// Algorithm ABB with ordered open list implementation.
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    /// <typeparam name="NetworkType">Type of the networks representing the solutions</typeparam>
    public class AlgorithmABBOrderedOpenList<PNSProblemType, NetworkType> : OrderedOpenListBranchAndBoundAlgorithm<PNSProblemType, ABBSubproblem<PNSProblemType>, NetworkType>
        where PNSProblemType : PNSProblemBase
        where NetworkType : NetworkBase, IComparable<NetworkType>
    {

        public AlgorithmABBOrderedOpenList(PNSProblemType problem, Func<ABBSubproblem<PNSProblemType>, NetworkType?> boundingFunction, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1)
            : base(problem, CommonImplementations.ABBBranching, boundingFunction, maxSolutions, baseUnitSet, timeLimit, threadCount)
        {
            this.SetBranchingExtensions(CommonImplementations.DefaultBranchingExtensionsForABB<PNSProblemType, ABBSubproblem<PNSProblemType>>());
        }

        public AlgorithmABBOrderedOpenList(PNSProblemType problem, Func<ABBSubproblem<PNSProblemType>, NetworkType?> boundingFunction, Comparison<(ABBSubproblem<PNSProblemType>, NetworkType)> networkComparator, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1)
            : base(problem, CommonImplementations.ABBBranching, boundingFunction, networkComparator, maxSolutions, baseUnitSet, timeLimit, threadCount)
        {
            this.SetBranchingExtensions(CommonImplementations.DefaultBranchingExtensionsForABB<PNSProblemType, ABBSubproblem<PNSProblemType>>());
        }
    }

    /// <summary>
    /// Algorithm ABB with LIFO open list implementation.
    /// </summary>
    /// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
    /// <typeparam name="NetworkType">Type of the networks representing the solutions</typeparam>
    public class AlgorithmABBDepthFirstOpenList<PNSProblemType, NetworkType> : DepthFirstOpenListBranchAndBoundAlgorithm<PNSProblemType, ABBSubproblem<PNSProblemType>, NetworkType>
        where PNSProblemType : PNSProblemBase
        where NetworkType : NetworkBase, IComparable<NetworkType>
    {

        public AlgorithmABBDepthFirstOpenList(PNSProblemType problem, Func<ABBSubproblem<PNSProblemType>, NetworkType?> boundingFunction, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1)
            : base(problem, CommonImplementations.ABBBranching, boundingFunction, maxSolutions, baseUnitSet, timeLimit, threadCount)
        {
            this.SetBranchingExtensions(CommonImplementations.DefaultBranchingExtensionsForABB<PNSProblemType, ABBSubproblem<PNSProblemType>>());
        }
    }

}
