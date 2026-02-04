# Example 2: the branch-and-bound system

This sample code explains how to configure and execute branch-and-bound algorithms to perform optimization.

Branch-and-bound algorithms in the library follow a class hierarchy which defines the essential logic of the algorithm.
At the same time, all parts (e.g., branching logic, bounding logic) can be cofigured separately.

Let's start with the same PNS problem introduced in Example 1. The initialization is now separated into a method, so the sample code can get into the new things right away.

    SimplePNSProblem problem = getSampleProblem();

To optimize the problem, you first need to make a few decisions on what algorithm to run:
- What is the branching subproblem to use?
- What is the basic branching logic (how to generate child subproblems from a subproblem)?
- Do you need extended branching logic?
- How to represent a solution network?
- What is the bounding logic (how to generate a partial or full solution network from a subproblem)?
- What overall algorithm to use?

The overall algorithm is defined by the class you instantiate, while the others are generic parameters and/or method parameters.

The library contains 3 main overall algorithms in 2 groups:
- Recursive method call-based operation, which generates child subproblems and recursively calls the main logic on each of them. This is inherently a depth-first search, and does not support multi-threaded operation. The current version of the library implements this via the class:
  - **RecursiveBranchAndBoundAlgorithm**
- Open subproblem list-based operation, where generated child subproblems are added to a subproblem list and when a new subproblem is examined, it is taken from this list. The logic of the list can be configured, and supports multi-threaded operation. The current version of the library has two implementations:
  - **DepthFirstOpenListBranchAndBoundAlgorithm**: the subproblem list implements a LIFO storage
  - **OrderedOpenListBranchAndBoundAlgorithm**: the subproblem are ordered based on a default or configurable comparator, and always the first (lowest value / best) subproblem is evaluated next. Implements A* algorithm with proper bounding logic.
  - More can be created by subclassing OpenListBranchAndBoundBase and implementing the two necessary methods
- Furthermore, the CommonImplementations static class contains some derived classes for more specific use-cases:
  - **CommonImplementations.AlgorithmABBRecursive**: Derived from RecursiveBranchAndBoundAlgorithm, the branching logic is fixed to ABB logic
  - **CommonImplementations.AlgorithmABBDepthFirstOpenList**: Derived from DepthFirstOpenListBranchAndBoundAlgorithm, the branching logic is fixed to ABB logic
  - **CommonImplementations.AlgorithmABBOrderedOpenList**: Derived from OrderedOpenListBranchAndBoundAlgorithm, the branching logic is fixed to ABB logic

The branching logic consists of two parts: the representation of the subproblem and the logic to generate child subproblems.
For now, we will not go into details about writing branching logic, that is a topic for a later example.
The CommonImplementations static class contains premade classes that can be used and cover the most common use-cases.

Two premade branching methods exists:
- ABB-logic (select a material, decide which producing units to include) via the CommonImplementations.ABBSubproblem class and the CommonImplementations.ABBBranching method
- Binary logic (select an operating unit, decide to include or exclude) via the CommonImplementations.BinaryDecisionSubproblem class and the CommonImplementations.BinaryDecisionBranching method

The bounding logic also consists of two parts: the representation of the partial or final solution network and the method to determine the bound from the subproblem and create the network.
All solution networks must derive from the NetworkBase class and thus must contain the set of operating units which define the network.

For now, we will not make a new network definition, that is for a later example.
The library contains a SimpleNetwork class, which is the simplest representation of a network: it contains the set of operating units and an objective value, which is the result of the bouding. For intermediate subproblems, the objective value is the bounding approximation. For leaf subproblems, the objective value is the exact value of the respective solution.
We must however define a function which implements the bounding logic. This method takes a subproblem as an argument and returns a network. If the subproblem is infeasible, the method should return null.

Currently, since we have no real numerical data for our PNS problem, we define a simple objective function as the number of included operating units. 
The respective bouding function needs to be defined. Here, all solutions will be feasible, and the bounding will not differentiate between intermediate and leaf subproblems.
Since we will choose to use the included ABB branching and the SimpleNetwork class, the bounding method needs to receive an ABBSubproblem (for the SimplePNSProblem problem type) and return a SimpleNetwork.

    SimpleNetwork? unitCountBounding(CommonImplementations.ABBSubproblem<SimplePNSProblem> subproblem)
    {
        return new SimpleNetwork(subproblem.Included, subproblem.Included.Count);
    }

When everything is ready, instantiate the selected algorithm with the desired parameters
This can be done via the generic algorithms and providing the branching logic

    var abb = new OrderedOpenListBranchAndBoundAlgorithm<SimplePNSProblem, CommonImplementations.ABBSubproblem<SimplePNSProblem>, SimpleNetwork>(problem, CommonImplementations.ABBBranching, unitCountBounding);

Or, if the premade configurations are appropriate, just use one of those

    var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<SimplePNSProblem, SimpleNetwork>(problem, unitCountBounding);

Optional parameters for the branch-and-bound algoritmhs:
- **int maxSolutions**: The returned number of n-best solutions (upper bound). Default value is 1. Set to -1 to return all solutions.
- **OperatingUnitSet? baseUnitSet**: Limits the operation to the given set of operating units (just like MSG and SSG). Default value is null (use all operating units).
- **TimeSpan? timeLimit**: Sets the time limit for the algorithm. If the given time span expires, the algorithm stops and returns the n-best netowrks found so far. Default value is null (no time limit).
- **int threadCount**: Sets the thread count for algorithms which allow multi-threaded operation. Default value: 1. Among the included methods, recursive branch-and-bound does not allow multi-threaded operation, while open-list branch-and-bound does.

So, a common call to generate the best 5 networks in 3 threads would be:

    var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<SimplePNSProblem, SimpleNetwork>(problem, unitCountBounding, maxSolutions: 5, threadCount: 3);

It is possible to add branching extension methods to provide additional functionality to any compatible branching logic. These are general algorithms which can reduce the search space by modifying the subproblem.
The premade ABB implementations (AlgorithmABBRecursive, AlgorithmABBDepthFirstOpenList, and AlgorithmABBOrderedOpenList) set their own branching extensions. For other methods, set them as required.

    abb.SetBranchingExtensions([CommonImplementations.ReducedStructureGenerator<SimplePNSProblem, CommonImplementations.ABBSubproblem<SimplePNSProblem>>]);

The DefaultBranchingExtensions() or DefaultBranchingExtensionsForABB() methods provide a default set of suggested extensions (the first is more general, the latter specifically targets ABB branching).

    abb.SetBranchingExtensions(CommonImplementations.DefaultBranchingExtensionsForABB<SimplePNSProblem, CommonImplementations.ABBSubproblem<SimplePNSProblem>>());

The Solve() methods runs the algorithm. It is not necessary to call directly, but possible.

    abb.Solve();

The GetSolutionNetworks() methods returns the ordered list of solution networks. Calls the Solve() method if is has not been called yet.

    foreach (SimpleNetwork solution in abb.GetSolutionNetworks())
    {
        Console.WriteLine($"Units: {solution.Units}, objective value: {solution.ObjectiveValue}");
    }
