
namespace PGraphInCS;

public abstract class AlgorithmBase<PNSProblemType>
{
    protected OperatingUnitSet? _baseUnitSet = null;
    protected PNSProblemType _problem;

    public AlgorithmBase(PNSProblemType problem, OperatingUnitSet? baseUnitSet = null)
    {
        this._problem = problem;
        this._baseUnitSet = baseUnitSet;
    }
}

/// <summary>
/// Implementation of Algorithm MSG of the P-graph framework
/// </summary>
public class AlgorithmMSG : AlgorithmBase<PNSProblemBase>
{
    private OperatingUnitSet? _maximalStructure = null;

    /// <summary>
    /// </summary>
    /// <param name="problem">PNS problem containing operating units and materials</param>
    /// <param name="baseUnitSet">Limits the algorithm to consider only a subset of operating units. Default: null, which means to consider all operating units in the PNS problem.</param>
    public AlgorithmMSG(PNSProblemBase problem, OperatingUnitSet? baseUnitSet = null) : base(problem, baseUnitSet)
    {
    }

    /// <summary>
    /// Returns the operating units in the maximal structure. Runs the maximal structure generation if it hasn't been run before.
    /// </summary>
    /// <returns>Set of operating units in the maximal structure.</returns>
    public OperatingUnitSet GetMaximalStructure()
    {
        if (this._maximalStructure == null)
            Run();
        return this._maximalStructure!;
    }

    /// <summary>
    /// Runs the algorithm. Not necessary to call directly, as GetMaximalStructure() calls it if it was not called directly.
    /// </summary>
    public void Run()
    {
        this._maximalStructure = new OperatingUnitSet();

        // Reduction phase initialization
        OperatingUnitSet unitsToBeRemoved = _problem.ProducersOf(_problem.RawMaterials);
        OperatingUnitSet units;
        if (_baseUnitSet == null)
            units = _problem.OperatingUnits.Except(unitsToBeRemoved);
        else
            units = new OperatingUnitSet(_baseUnitSet.Except(unitsToBeRemoved));
        MaterialSet materials = _problem.InputsOf(units).Union(_problem.OutputsOf(units));
        if (!_problem.Products.IsSubsetOf(materials))
        {
            return; // No solution: some products cannot be produced
        }

        // Reduction phase: iteration
        MaterialSet nonProducedMaterials = materials.Except(_problem.RawMaterials).Except(_problem.OutputsOf(units));
        while (nonProducedMaterials.Count > 0)
        {
            //MaterialNode selectedMaterial = nonProducedMaterials.First();
            //unitsToBeRemoved = Problem.Consumers[selectedMaterial];
            unitsToBeRemoved = _problem.ConsumersOf(nonProducedMaterials);
            units.ExceptWith(unitsToBeRemoved);
            materials = _problem.InputsOf(units).Union(_problem.OutputsOf(units));
            if (!_problem.Products.IsSubsetOf(materials))
            {
                return; // No solution: some products cannot be produced
            }
            MaterialSet newSources = _problem.InputsOf(units).Except(_problem.OutputsOf(units)).Intersect(_problem.OutputsOf(unitsToBeRemoved));
            nonProducedMaterials = nonProducedMaterials.Intersect(materials).Union(newSources);
        }
        OperatingUnitSet allExcludedUnits = _problem.OperatingUnits.Except(units);

        // Composition phase: initialization
        MaterialSet materialsToBeProduced = new MaterialSet(_problem.Products);
        MaterialSet includedMaterials = new MaterialSet();
        OperatingUnitSet includedUnits = new OperatingUnitSet();

        // Composition phase: iteration
        while (materialsToBeProduced.Count > 0)
        {
            includedUnits.UnionWith(_problem.ProducersOf(materialsToBeProduced).Except(allExcludedUnits));
            includedMaterials.UnionWith(materialsToBeProduced);
            materialsToBeProduced = _problem.InputsOf(includedUnits).Except(_problem.RawMaterials.Union(includedMaterials));
        }

        // End, save the solution
        this._maximalStructure = includedUnits;
    }
}

/// <summary>
/// Recursive function-based implementation of Algorithm SSG of the P-graph framework
/// </summary>
public class AlgorithmSSGRecursive : AlgorithmBase<PNSProblemBase>
{
    private List<OperatingUnitSet>? _solutionStructures = null;

    /// <summary>
    /// </summary>
    /// <param name="problem">PNS problem containing operating units and materials</param>
    /// <param name="baseUnitSet">Limits the algorithm to consider only a subset of operating units. Default: null, which means to consider all operating units in the PNS problem.</param>
    public AlgorithmSSGRecursive(PNSProblemBase problem, OperatingUnitSet? baseUnitSet = null) : base(problem, baseUnitSet)
    {
    }

    /// <summary>
    /// Returns the list of solution structures. Runs the solution structure generation if it hasn't been run before.
    /// </summary>
    /// <returns>List of solution structures, each represented by a set of operating units.</returns>
    public List<OperatingUnitSet> GetSolutionStructures()
    {
        if (this._solutionStructures == null)
            Run();
        return this._solutionStructures!;
    }

    /// <summary>
    /// Runs the algorithm. Not necessary to call directly, as GetSolutionStructures() calls it if it was not called directly.
    /// </summary>
    public void Run()
    {
        this._solutionStructures = new List<OperatingUnitSet>();
        AlgorithmMSG msg = new AlgorithmMSG(_problem,_baseUnitSet);
        OperatingUnitSet unitsToConsider = msg.GetMaximalStructure();
        if (unitsToConsider.Count == 0)
        {
            return;
        }
        recursiveSteps(_problem.Products, new MaterialSet(), new OperatingUnitSet(), _problem.OperatingUnits.Except(unitsToConsider));
    }

    private void recursiveSteps(MaterialSet toBeProduced, MaterialSet alreadyProduced, OperatingUnitSet included, OperatingUnitSet excluded)
    {
        if (included.Intersect(excluded).Count() > 0)
        {
            return;
        }
        if (toBeProduced.Count == 0)
        {
            this._solutionStructures!.Add(included);
            return;
        }
        MaterialNode selectedMaterial = toBeProduced.First();
        OperatingUnitSet canProduceSelectedMaterial = _problem.Producers[selectedMaterial];
        OperatingUnitSet canProduceSelectedMaterialNew = canProduceSelectedMaterial.Except(included);
        OperatingUnitSet alreadyProducingMaterial = canProduceSelectedMaterial.Intersect(included);
        bool ignoreEmptySet = alreadyProducingMaterial.Count == 0;
        int maxparallel = _problem.MaxParallelProduction[selectedMaterial];
        if (maxparallel != -1 && alreadyProducingMaterial.Count > maxparallel)
        {
            return;
        }
        IEnumerable<OperatingUnitSet> pwset;
        if (maxparallel == -1) pwset = NodePowerSet<OperatingUnitNode>.GetPowerSet(canProduceSelectedMaterialNew);
        else pwset = NodePowerSet<OperatingUnitNode>.GetPowerSet(canProduceSelectedMaterialNew, maxparallel - alreadyProducingMaterial.Count);
        foreach (OperatingUnitSet newUnits in pwset)
        {
            if (newUnits.Count == 0 && ignoreEmptySet) continue;
            OperatingUnitSet units = newUnits.Union(alreadyProducingMaterial);
            if (included.Intersect(canProduceSelectedMaterialNew.Except(units)).Count == 0 &&
                excluded.Intersect(units).Count == 0)
            {
                OperatingUnitSet newIncluded = included.Union(units);
                OperatingUnitSet newExcluded = excluded.Union(canProduceSelectedMaterialNew.Except(units)).Union(_problem.MutuallyExclusiveWith(units));
                if (newIncluded.Intersect(newExcluded).Count == 0)
                {
                    recursiveSteps(toBeProduced.Union(_problem.InputsOf(units))
                                        .Except(_problem.RawMaterials.Union(alreadyProduced).Union(selectedMaterial)),
                                    alreadyProduced.Union(selectedMaterial),
                                    newIncluded,
                                    newExcluded);
                }
            }
        }
    }
}

/// <summary>
/// Base class for all networks to represent solutions of branch-and-bound algorithms. Contains the set of operating units which define the structure of the solution.
/// </summary>
public abstract class NetworkBase
{
    public OperatingUnitSet? Units { get; } = null;
    public NetworkBase()
    {
    }
    public NetworkBase(OperatingUnitSet units)
    {
        this.Units = units;
    }
}

/// <summary>
/// The simplest network to represent solutions of branch-and-bound algorithms. Contains the set of oeprating units and an objective value to be the base of comparision and ordering.
/// </summary>
public class SimpleNetwork : NetworkBase, IComparable<SimpleNetwork>
{
    public double ObjectiveValue { get; }
    public SimpleNetwork(double objectiveValue) : base()
    {
        ObjectiveValue = objectiveValue;
    }

    public SimpleNetwork(OperatingUnitSet units, double objectiveValue) : base(units)
    {
        ObjectiveValue = objectiveValue;
    }

    public int CompareTo(SimpleNetwork? other) =>
        other == null ? 1 : this.ObjectiveValue.CompareTo(other.ObjectiveValue);
}

/// <summary>
/// Base class for all branch-and-bound algorithms. Defines the basic parameters as well as the collection of ordered n-best solutions. Other than the collection of solutions, all other parameters are only applied if the derived classes support them.
/// </summary>
/// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
/// <typeparam name="NetworkType">Type of the networks representing the solutions</typeparam>
public abstract class BranchAndBoundBase<PNSProblemType, NetworkType> : AlgorithmBase<PNSProblemType>
    where PNSProblemType : PNSProblemBase
    where NetworkType : NetworkBase, IComparable<NetworkType>
{
    protected List<NetworkType>? _solutionNetworks = null;
    protected int _maxSolutions = 1;
    protected TimeSpan? _timeLimit = null;
    protected DateTime _startTime = DateTime.MinValue;

    protected int _threadCount = 1;
    private Lock _solutionUpdateLock = new Lock();

    /// <summary>
    /// </summary>
    /// <param name="problem">PNS problem containing operating units and materials</param>
    /// <param name="maxSolutions">Number of n-best solutions to generate. Default value: 1. The value -1 will result in generating all feasible solutions.</param>
    /// <param name="baseUnitSet">Limits the algorithm to consider only a subset of operating units. Default: null, which means to consider all operating units in the PNS problem.</param>
    /// <param name="timeLimit">Time limit for generating the solutions</param>
    /// <param name="threadCount">Number of threads to use while running the algorithm</param>
    public BranchAndBoundBase(PNSProblemType problem, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1) : base(problem, baseUnitSet)
    {
        this._maxSolutions = maxSolutions;
        this._timeLimit = timeLimit;
        this._threadCount = threadCount;
    }

    /// <summary>
    /// Returns the ordered list of n-best solutions. Runs the branch-and-bound algorithm if it hasn't been run before.
    /// </summary>
    /// <returns>Ordered list of n-best solutions, each represented by the given type of network.</returns>
    public List<NetworkType> GetSolutionNetworks()
    {
        if (this._solutionNetworks == null)
        {
            Solve();
        }
        return this._solutionNetworks!;
    }

    /// <summary>
    /// Runs the algorithm. Not necessary to call directly, as GetSolutionNetworks() calls it if it was not called directly.
    /// </summary>
    public void Solve()
    {
        this._solutionNetworks = new List<NetworkType>();
        _startTime = DateTime.Now;
        Run();
    }

    protected abstract void Run();
    protected void SaveSolutionAndUpdate(NetworkType network)
    {
        int insertindex = 0;
        lock (_solutionUpdateLock)
        {
            while (insertindex < _solutionNetworks!.Count && _solutionNetworks![insertindex].CompareTo(network) <= 0)
                insertindex++;
            _solutionNetworks.Insert(insertindex, network);
            if (_maxSolutions != -1 && _solutionNetworks.Count > _maxSolutions) // for now, we do not keep same value solutions as extra
                _solutionNetworks.RemoveAt(_solutionNetworks.Count - 1);
        }
    }
}

/// <summary>
/// Basic class for subproblems to use in subproblem-based branch-and-bound algorithms.
/// </summary>
/// <typeparam name="PNSProblemType">Type of PNS problem</typeparam>
public abstract class SubproblemBase<PNSProblemType>
    where PNSProblemType : PNSProblemBase
{
    public PNSProblemType Problem { get; set; }
    public OperatingUnitSet? BaseUnitSet { get; set; }

    protected SubproblemBase(PNSProblemType problem, OperatingUnitSet? baseUnitSet)
    {
        this.Problem = problem;
        BaseUnitSet = baseUnitSet?.Clone();
    }
    public abstract bool IsLeaf { get; }
    public abstract bool IsErrorFree { get; }
}
public interface ISubproblemInitializer<PNSProblemType, SubproblemType>
    where PNSProblemType : PNSProblemBase
    where SubproblemType : SubproblemBase<PNSProblemType>
{
    static abstract SubproblemType InitializeRoot(PNSProblemType problem, OperatingUnitSet? baseUnitSet);
}
public interface ISubpoblemWithIncludedExcludedGet
{
    OperatingUnitSet GetIncludedUnits();
    OperatingUnitSet GetExcludedUnits();
};
public interface ISubpoblemWithIncludedExcludedSet
{
    void SetIncludedUnits(OperatingUnitSet units);
    void SetExcludedUnits(OperatingUnitSet units);
};
public interface ISubpoblemWithIncludedExcludedGetSet : ISubpoblemWithIncludedExcludedGet, ISubpoblemWithIncludedExcludedSet
{
}

/// <summary>
/// Basic class for all subproblem-based branch-and-bound algorithms. Defines the types of the PNS problem, the subproblem representation and the solution network representation. Provides standardized handling of the branching and bounding algorithms, as well as options for extending any branching algorithms with additional logic.
/// </summary>
/// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
/// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
/// <typeparam name="NetworkType">Type of the networks representing the solutions</typeparam>
public abstract class SubproblemBasedBranchAndBoundBase<PNSProblemType, SubproblemType, NetworkType> : BranchAndBoundBase<PNSProblemType, NetworkType>
    where PNSProblemType : PNSProblemBase
    where SubproblemType : SubproblemBase<PNSProblemType>, ISubproblemInitializer<PNSProblemType, SubproblemType>
    where NetworkType : NetworkBase, IComparable<NetworkType>
{
    private Func<SubproblemType, IEnumerable<SubproblemType>> _rawBranchingFunction;
    private List<Func<SubproblemType,bool>> _branchingExtensions = new();

    protected Func<SubproblemType, NetworkType?> _boundingFunction;

    /// <summary>
    /// </summary>
    /// <param name="problem">PNS problem containing operating units and materials</param>
    /// <param name="branchingFunction">Branching function defining how the children of a subproblem are generated. The branching function takes a subproblem and return the collection of children subproblems. Can support lazy evaluation.</param>
    /// <param name="boundingFunction">Bounding function defining how the value and feasibility of a subproblem is evaluated. The bounding function takes a subproblem and returns a network containing the bounding data, of null if the subproblem is infeasible.</param>
    /// <param name="maxSolutions">Number of n-best solutions to generate. Default value: 1. The value -1 will result in generating all feasible solutions.</param>
    /// <param name="baseUnitSet">Limits the algorithm to consider only a subset of operating units. Default: null, which means to consider all operating units in the PNS problem.</param>
    /// <param name="timeLimit">Time limit for generating the solutions</param>
    /// <param name="threadCount">Number of threads to use while running the algorithm</param>
    public SubproblemBasedBranchAndBoundBase(PNSProblemType problem, Func<SubproblemType, IEnumerable<SubproblemType>> branchingFunction, Func<SubproblemType, NetworkType?> boundingFunction, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1)
        : base(problem, maxSolutions, baseUnitSet, timeLimit, threadCount)
    {
        this._rawBranchingFunction = branchingFunction;
        this._boundingFunction = boundingFunction;
    }

    /// <summary>
    /// Method to set the applied extensions to the subproblem branching.
    /// Each provided method gets a subproblem as an argument and modifies it. The return value indicates feasbility: false, if the extension logic found the subproblem infeasible, true, otherwise.
    /// It is important to guarantee that the applied branching extensions do not break any assumptions of the raw branching method.
    /// </summary>
    /// <param name="branchingExtensions"></param>
    public void SetBranchingExtensions(List<Func<SubproblemType,bool>> branchingExtensions)
    {
        _branchingExtensions = branchingExtensions;
    }

    protected IEnumerable<SubproblemType> _branchingFunction(SubproblemType subproblem)
    {
        foreach (SubproblemType child in _rawBranchingFunction(subproblem))
        {
            bool isOk = true;
            foreach (var extension in _branchingExtensions)
            {
                if (child.IsErrorFree == false)
                {
                    isOk = false;
                    break;
                }
                if (extension(child) == false)
                {
                    isOk = false;
                    break;
                }
            }
            if (isOk) yield return child;
        }
    }
}

/// <summary>
/// Generic recursive implementation of branch-and-bound algorithms. Does not support multi-threaded operation.
/// </summary>
/// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
/// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
/// <typeparam name="NetworkType">Type of the networks representing the solutions</typeparam>
public class RecursiveBranchAndBoundAlgorithm<PNSProblemType, SubproblemType, NetworkType> : SubproblemBasedBranchAndBoundBase<PNSProblemType, SubproblemType, NetworkType>
    where PNSProblemType : PNSProblemBase
    where SubproblemType : SubproblemBase<PNSProblemType>, ISubproblemInitializer<PNSProblemType,SubproblemType>
    where NetworkType : NetworkBase, IComparable<NetworkType>
{
    /// <summary>
    /// </summary>
    /// <param name="problem">PNS problem containing operating units and materials</param>
    /// <param name="branchingFunction">Branching function defining how the children of a subproblem are generated. The branching function takes a subproblem and return the collection of children subproblems. Can support lazy evaluation.</param>
    /// <param name="boundingFunction">Bounding function defining how the value and feasibility of a subproblem is evaluated. The bounding function takes a subproblem and returns a network containing the bounding data, of null if the subproblem is infeasible.</param>
    /// <param name="maxSolutions">Number of n-best solutions to generate. Default value: 1. The value -1 will result in generating all feasible solutions.</param>
    /// <param name="baseUnitSet">Limits the algorithm to consider only a subset of operating units. Default: null, which means to consider all operating units in the PNS problem.</param>
    /// <param name="timeLimit">Time limit for generating the solutions</param>
    public RecursiveBranchAndBoundAlgorithm(PNSProblemType problem, Func<SubproblemType, IEnumerable<SubproblemType>> branchingFunction, Func<SubproblemType, NetworkType?> boundingFunction, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null)
        : base(problem, branchingFunction, boundingFunction, maxSolutions, baseUnitSet, timeLimit)
    {
    }

    protected override void Run()
    {
        this._solutionNetworks!.Clear();
        AlgorithmMSG msg = new AlgorithmMSG(_problem,_baseUnitSet);
        OperatingUnitSet unitsToConsider = msg.GetMaximalStructure();
        if (unitsToConsider.Count == 0) { return; }
        SubproblemType rootSubProblem = SubproblemType.InitializeRoot(_problem, unitsToConsider);
        recursiveSteps(rootSubProblem);
    }

    private void recursiveSteps(SubproblemType subproblem)
    {
        if (DateTime.Now - _startTime >= _timeLimit) { return; }
        if (subproblem.IsErrorFree == false) { return; }
        NetworkType? boundNetwork = _boundingFunction(subproblem);
        if (boundNetwork == null) { return; }
        if (subproblem.IsLeaf)
        {
            SaveSolutionAndUpdate(boundNetwork);
            return;
        }
        if (_maxSolutions != -1 && _solutionNetworks!.Count >= _maxSolutions && boundNetwork.CompareTo(_solutionNetworks.Last()) >= 0)
        {
            return;
        }
        foreach (var child in _branchingFunction(subproblem))
        {
            recursiveSteps(child);
        }
    }
}

/// <summary>
/// Generic open-list-based implementation of branch-and-bound algorithms. Supports multi-threaded operation. Derived classes need to define how subproblems are inserted and removed from the open list.
/// </summary>
/// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
/// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
/// <typeparam name="NetworkType">Type of the networks representing the solutions</typeparam>
public abstract class OpenListBranchAndBoundBase<PNSProblemType, SubproblemType, NetworkType> : SubproblemBasedBranchAndBoundBase<PNSProblemType, SubproblemType, NetworkType>
    where PNSProblemType : PNSProblemBase
    where SubproblemType : SubproblemBase<PNSProblemType>, ISubproblemInitializer<PNSProblemType, SubproblemType>
    where NetworkType : NetworkBase, IComparable<NetworkType>
{
    private Lock _subproblemListLock = new Lock();

    /// <summary>
    /// </summary>
    /// <param name="problem">PNS problem containing operating units and materials</param>
    /// <param name="branchingFunction">Branching function defining how the children of a subproblem are generated. The branching function takes a subproblem and return the collection of children subproblems. Can support lazy evaluation.</param>
    /// <param name="boundingFunction">Bounding function defining how the value and feasibility of a subproblem is evaluated. The bounding function takes a subproblem and returns a network containing the bounding data, of null if the subproblem is infeasible.</param>
    /// <param name="maxSolutions">Number of n-best solutions to generate. Default value: 1. The value -1 will result in generating all feasible solutions.</param>
    /// <param name="baseUnitSet">Limits the algorithm to consider only a subset of operating units. Default: null, which means to consider all operating units in the PNS problem.</param>
    /// <param name="timeLimit">Time limit for generating the solutions</param>
    /// <param name="threadCount">Number of threads to use while running the algorithm</param>
    public OpenListBranchAndBoundBase(PNSProblemType problem, Func<SubproblemType, IEnumerable<SubproblemType>> branchingFunction, Func<SubproblemType, NetworkType?> boundingFunction, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1)
        : base(problem, branchingFunction, boundingFunction, maxSolutions, baseUnitSet, timeLimit, threadCount)
    {
    }
    protected abstract (SubproblemType, NetworkType) PopNextOpenSubproblemNode(LinkedList<(SubproblemType, NetworkType)> openList);
    protected abstract void AddNodeToOpenList(LinkedList<(SubproblemType, NetworkType)> openList, (SubproblemType, NetworkType) node);

    private (SubproblemType, NetworkType)? _popNextOpenSubproblemNodeIfAny(LinkedList<(SubproblemType, NetworkType)> openList)
    {
        (SubproblemType, NetworkType)? res = null;
        lock (_subproblemListLock)
        {
            if (openList.Count != 0)
            {
                res = PopNextOpenSubproblemNode(openList);
            }
        }
        return res;
    }
    private void _addNodeToOpenList(LinkedList<(SubproblemType, NetworkType)> openList, (SubproblemType, NetworkType) node)
    {
        lock (_subproblemListLock)
        {
            AddNodeToOpenList(openList, node);
        }
    }

    protected virtual void RunLoopSingleThread(LinkedList<(SubproblemType subproblem, NetworkType network)> openSubproblems)
    {
        while (openSubproblems.Count > 0)
        {
            if (DateTime.Now - _startTime >= _timeLimit) { return; }
            var (subproblem, network) = PopNextOpenSubproblemNode(openSubproblems);
            foreach (var child in _branchingFunction(subproblem))
            {
                if (child.IsErrorFree == false) { continue; }
                NetworkType? boundNetwork = _boundingFunction(child);
                if (boundNetwork == null) { continue; }
                if (child.IsLeaf)
                {
                    SaveSolutionAndUpdate(boundNetwork);
                    continue;
                }
                if (_maxSolutions != -1 && _solutionNetworks!.Count >= _maxSolutions && boundNetwork.CompareTo(_solutionNetworks.Last()) >= 0)
                {
                    continue;
                }

                var newNode = (child, boundNetwork);
                AddNodeToOpenList(openSubproblems, newNode);
            }
        }
    }

    protected virtual void RunLoopMultipleThread(LinkedList<(SubproblemType subproblem, NetworkType network)> openSubproblems)
    {
        // TODO
        int threadsWorking = 0;
        
        EventWaitHandle controlHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        bool shouldEnd = false;

        var threadFunc = () =>
        {
            Interlocked.Increment(ref threadsWorking);
            bool working = true;
            while (shouldEnd == false)
            {
                bool wasWorking = working;
                if (DateTime.Now - _startTime >= _timeLimit)
                {
                    if (wasWorking == true) Interlocked.Decrement(ref threadsWorking);
                    working = false;
                    shouldEnd = true;
                    controlHandle.Set();
                    return;
                }
                (SubproblemType, NetworkType)? nextSubproblem = null;
                lock (_subproblemListLock)
                {
                    nextSubproblem = _popNextOpenSubproblemNodeIfAny(openSubproblems);
                }
                working = (nextSubproblem != null);
                if (!working)
                {
                    if (wasWorking == true) Interlocked.Decrement(ref threadsWorking);
                    //Console.WriteLine($"Thread {System.Environment.CurrentManagedThreadId} has no work");
                    if (threadsWorking == 0)
                    {
                        shouldEnd = true;
                        controlHandle.Set();
                        return;
                    }
                    else
                    {
                        controlHandle.WaitOne();
                        continue;
                    }
                }
                else
                {
                    if (wasWorking == false) Interlocked.Increment(ref threadsWorking);
                    var (subproblem, network) = nextSubproblem!.Value;
                    //Console.WriteLine($"Thread {System.Environment.CurrentManagedThreadId} got new problem");
                    foreach (var child in _branchingFunction(subproblem))
                    {
                        if (child.IsErrorFree == false) { continue; }
                        NetworkType? boundNetwork = _boundingFunction(child);
                        if (boundNetwork == null) { continue; }
                        if (child.IsLeaf)
                        {
                            SaveSolutionAndUpdate(boundNetwork);
                            continue;
                        }
                        if (_maxSolutions != -1 && _solutionNetworks!.Count >= _maxSolutions && boundNetwork.CompareTo(_solutionNetworks.Last()) >= 0)
                        {
                            continue;
                        }

                        var newNode = (child, boundNetwork);
                        _addNodeToOpenList(openSubproblems, newNode);
                        controlHandle.Set();
                    }
                }
            }
            controlHandle.Set();
        };

        Thread[] threads = new Thread[_threadCount - 1];
        for (int i = 0; i < _threadCount - 1; i++)
        {
            threads[i] = new Thread(() => threadFunc());
            threads[i].Start();
        }
        threadFunc();
        for (int i = 0; i < _threadCount - 1; i++)
        {
            threads[i].Join();
        }

    }

    protected override void Run()
    {
        this._solutionNetworks!.Clear();
        AlgorithmMSG msg = new AlgorithmMSG(_problem, _baseUnitSet);
        OperatingUnitSet unitsToConsider = msg.GetMaximalStructure();
        if (unitsToConsider.Count == 0) { return; }
        SubproblemType rootSubProblem = SubproblemType.InitializeRoot(_problem, unitsToConsider);

        LinkedList<(SubproblemType subproblem, NetworkType network)> openSubproblems = new();
        {
            if (rootSubProblem.IsErrorFree == false) { return; }
            NetworkType? boundNetwork = _boundingFunction(rootSubProblem);
            if (boundNetwork == null) { return; }
            if (rootSubProblem.IsLeaf)
            {
                SaveSolutionAndUpdate(boundNetwork);
                return;
            }
            openSubproblems.AddLast((rootSubProblem, boundNetwork));
        }
        if (_threadCount == 1)
        {
            RunLoopSingleThread(openSubproblems);
        }
        else
        {
            RunLoopMultipleThread(openSubproblems);
        }
    }
}
public static class DefaultMethods
{
    public static int BasicItem1Comparator<T1, T2>((T1, T2) pair1, (T1, T2) pair2)
        where T1 : IComparable<T1> =>
        pair1.Item1.CompareTo(pair2.Item1);
    public static int BasicItem2Comparator<T1, T2>((T1, T2) pair1, (T1, T2) pair2)
        where T2 : IComparable<T2> =>
        pair1.Item2.CompareTo(pair2.Item2);
}

/// <summary>
/// Open-list-based implementation of a branch-and-bound algorithm, where the list is ordered by default or customizable comparator, and the first subproblem in the ordered list is examined next. Supports multi-threaded operation.
/// </summary>
/// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
/// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
/// <typeparam name="NetworkType">Type of the networks representing the solutions</typeparam>
public class OrderedOpenListBranchAndBoundAlgorithm<PNSProblemType, SubproblemType, NetworkType> : OpenListBranchAndBoundBase<PNSProblemType, SubproblemType, NetworkType>
    where PNSProblemType : PNSProblemBase
    where SubproblemType : SubproblemBase<PNSProblemType>, ISubproblemInitializer<PNSProblemType, SubproblemType>
    where NetworkType : NetworkBase, IComparable<NetworkType>
{
    private Comparison<(SubproblemType,NetworkType)> _networkComparator = DefaultMethods.BasicItem2Comparator;

    public OrderedOpenListBranchAndBoundAlgorithm(PNSProblemType problem, Func<SubproblemType, IEnumerable<SubproblemType>> branchingFunction, Func<SubproblemType, NetworkType?> boundingFunction, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1)
        : base(problem, branchingFunction, boundingFunction, maxSolutions, baseUnitSet, timeLimit, threadCount)
    {
    }

    public OrderedOpenListBranchAndBoundAlgorithm(PNSProblemType problem, Func<SubproblemType, IEnumerable<SubproblemType>> branchingFunction, Func<SubproblemType, NetworkType?> boundingFunction, Comparison<(SubproblemType, NetworkType)> networkComparator, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1)
        : base(problem, branchingFunction, boundingFunction, maxSolutions, baseUnitSet, timeLimit, threadCount)
    {
        this._networkComparator = networkComparator;
    }

    protected override (SubproblemType, NetworkType) PopNextOpenSubproblemNode(LinkedList<(SubproblemType, NetworkType)> openList)
    {
        var node = openList.First!.Value;
        openList.RemoveFirst();
        return node;
    }

    protected override void AddNodeToOpenList(LinkedList<(SubproblemType, NetworkType)> openList, (SubproblemType, NetworkType) node)
    {
        var currentNode = openList.First;
        while (currentNode != null && _networkComparator(currentNode.Value, node) < 0)
        {
            currentNode = currentNode.Next;
        }
        if (currentNode == null) { openList.AddLast(node); }
        else { openList.AddBefore(currentNode, node); }
    }
}

/// <summary>
/// Open-list-based implementation of a branch-and-bound algorithm, where the open-list operates as a LIFO dataset. Supports multi-threaded operation.
/// </summary>
/// <typeparam name="PNSProblemType">Type of the PNS problem</typeparam>
/// <typeparam name="SubproblemType">Type to represent subproblems</typeparam>
/// <typeparam name="NetworkType">Type of the networks representing the solutions</typeparam>
public class DepthFirstOpenListBranchAndBoundAlgorithm<PNSProblemType, SubproblemType, NetworkType> : OpenListBranchAndBoundBase<PNSProblemType, SubproblemType, NetworkType>
    where PNSProblemType : PNSProblemBase
    where SubproblemType : SubproblemBase<PNSProblemType>, ISubproblemInitializer<PNSProblemType, SubproblemType>
    where NetworkType : NetworkBase, IComparable<NetworkType>
{

    public DepthFirstOpenListBranchAndBoundAlgorithm(PNSProblemType problem, Func<SubproblemType, IEnumerable<SubproblemType>> branchingFunction, Func<SubproblemType, NetworkType?> boundingFunction, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1)
        : base(problem, branchingFunction, boundingFunction, maxSolutions, baseUnitSet, timeLimit, threadCount)
    {
    }

    protected override (SubproblemType, NetworkType) PopNextOpenSubproblemNode(LinkedList<(SubproblemType, NetworkType)> openList)
    {
        var node = openList.First!.Value;
        openList.RemoveFirst();
        return node;
    }

    protected override void AddNodeToOpenList(LinkedList<(SubproblemType, NetworkType)> openList, (SubproblemType, NetworkType) node)
    {
        openList.AddFirst(node);
    }
}
