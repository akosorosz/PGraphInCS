
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

public class AlgorithmMSG : AlgorithmBase<PNSProblemBase>
{
    private OperatingUnitSet? _maximalStructure = null;

    public AlgorithmMSG(PNSProblemBase problem, OperatingUnitSet? baseUnitSet = null) : base(problem, baseUnitSet)
    {
    }

    public OperatingUnitSet GetMaximalStructure()
    {
        if (this._maximalStructure == null)
            Run();
        return this._maximalStructure!;
    }

    private void Run()
    {
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
public class AlgorithmSSGRecursive : AlgorithmBase<PNSProblemBase>
{
    private List<OperatingUnitSet>? _solutionStructures = null;

    public AlgorithmSSGRecursive(PNSProblemBase problem, OperatingUnitSet? baseUnitSet = null) : base(problem, baseUnitSet)
    {
    }

    public List<OperatingUnitSet> GetSolutionStructures()
    {
        if (this._solutionStructures == null)
            Run();
        return this._solutionStructures!;
    }

    private void Run()
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
        if (maxparallel == -1) pwset = PowerSet.GetPowerSet(canProduceSelectedMaterialNew).Select(u => new OperatingUnitSet(u));
        else pwset = PowerSet.GetPowerSet(canProduceSelectedMaterialNew, maxparallel - alreadyProducingMaterial.Count).Select(u => new OperatingUnitSet(u));
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

    public BranchAndBoundBase(PNSProblemType problem, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1) : base(problem, baseUnitSet)
    {
        this._maxSolutions = maxSolutions;
        this._timeLimit = timeLimit;
        this._threadCount = threadCount;
    }

    public List<NetworkType> GetSolutionNetworks()
    {
        if (this._solutionNetworks == null)
        {
            Solve();
        }
        return this._solutionNetworks!;
    }

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

public abstract class SubproblemBase<PNSProblemType>
    where PNSProblemType : PNSProblemBase
{
    public PNSProblemType Problem;
    public OperatingUnitSet? BaseUnitSet;

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

public abstract class SubproblemBasedBranchAndBoundBase<PNSProblemType, SubproblemType, NetworkType> : BranchAndBoundBase<PNSProblemType, NetworkType>
    where PNSProblemType : PNSProblemBase
    where SubproblemType : SubproblemBase<PNSProblemType>, ISubproblemInitializer<PNSProblemType, SubproblemType>
    where NetworkType : NetworkBase, IComparable<NetworkType>
{
    private Func<SubproblemType, IEnumerable<SubproblemType>> _rawBranchingFunction;
    private List<Action<SubproblemType>> _branchingExtensions = new();

    protected Func<SubproblemType, NetworkType?> _boundingFunction;

    public SubproblemBasedBranchAndBoundBase(PNSProblemType problem, Func<SubproblemType, IEnumerable<SubproblemType>> branchingFunction, Func<SubproblemType, NetworkType?> boundingFunction, int maxSolutions = 1, OperatingUnitSet? baseUnitSet = null, TimeSpan? timeLimit = null, int threadCount = 1)
        : base(problem, maxSolutions, baseUnitSet, timeLimit, threadCount)
    {
        this._rawBranchingFunction = branchingFunction;
        this._boundingFunction = boundingFunction;
    }

    public void SetBranchingExtensions(List<Action<SubproblemType>> branchingExtensions)
    {
        _branchingExtensions = branchingExtensions;
    }

    protected IEnumerable<SubproblemType> _branchingFunction(SubproblemType subproblem)
    {
        foreach (SubproblemType child in _rawBranchingFunction(subproblem))
        {
            foreach (var extension in _branchingExtensions)
            {
                if (child.IsErrorFree == false) break;
                extension(child);
            }
            yield return child;
        }
    }
}

public class RecursiveBranchAndBoundAlgorithm<PNSProblemType, SubproblemType, NetworkType> : SubproblemBasedBranchAndBoundBase<PNSProblemType, SubproblemType, NetworkType>
    where PNSProblemType : PNSProblemBase
    where SubproblemType : SubproblemBase<PNSProblemType>, ISubproblemInitializer<PNSProblemType,SubproblemType>
    where NetworkType : NetworkBase, IComparable<NetworkType>
{

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

public abstract class OpenListBranchAndBoundBase<PNSProblemType, SubproblemType, NetworkType> : SubproblemBasedBranchAndBoundBase<PNSProblemType, SubproblemType, NetworkType>
    where PNSProblemType : PNSProblemBase
    where SubproblemType : SubproblemBase<PNSProblemType>, ISubproblemInitializer<PNSProblemType, SubproblemType>
    where NetworkType : NetworkBase, IComparable<NetworkType>
{
    private Lock _subproblemListLock = new Lock();

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
