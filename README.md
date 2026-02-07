# PGraphInCS
A flexible implementation of P-Graph algorithms in C#.

Current release version: 1.0.0 (Also available in NuGet under the same name: PGraphInCS)

## Description

The aim of the library is to provide the code-base for quick and flexible implementation of P-graph algorithms, while maintaining acceptable performance. The library contains implementations of the main algorithms of the P-graph framework, i.e., MSG, SSG, and ABB. It provides several configurable base classes and implementations of branch-and-bound algorithms. This allows new algorithms to be developed both for the existing and for extended problems. When creating new algorithms, only the new logic has to be implemented, while the unchanged parts can be used directly from this library. Also contains compatibility with the main problems supported by P-Graph Studio.

### Build the PNS problem

    MaterialNode A = new MaterialNode("A");
    MaterialNode B = new MaterialNode("B");
    MaterialNode C = new MaterialNode("C");
    MaterialNode D = new MaterialNode("D");
    
    OperatingUnitNode O1 = new OperatingUnitNode("O1", inputs: [A], outputs: [C, D]);
    OperatingUnitNode O2 = new OperatingUnitNode("O2", inputs: [B], outputs: [C]);
    OperatingUnitNode O3 = new OperatingUnitNode("O3", inputs: [A, B], outputs: [D]);
    
    SimplePNSProblem problem = new SimplePNSProblem();
    
    problem.AddMaterial(A);
    problem.AddMaterial(B);
    problem.AddMaterial(C);
    problem.AddMaterial(D);
    
    problem.AddOperatingUnit(O1);
    problem.AddOperatingUnit(O2);
    problem.AddOperatingUnit(O3);
    
    problem.SetRawMaterialsAndProducts(rawMaterials: [A, B], products: [D]);
    
    problem.FinalizeData();
    
    problem.OperatingUnits["O1"].AdditionalParameters.Add("cost", 34);
    problem.OperatingUnits["O2"].AdditionalParameters.Add("cost", 76);
    problem.OperatingUnits["O3"].AdditionalParameters.Add("cost", 12);

### Define the parts and parameters of the algorithm

    var bnb = new OrderedOpenListBranchAndBoundAlgorithm   // The overall algorithm logic
        <SimplePNSProblem,                                 // The type of PNS problem
        BinaryDecisionSubproblem<SimplePNSProblem>,        // The subproblem representation
        SimpleNetwork>                                     // The solution representation
        (problem,                                          // The problem itself
        BinaryDecisionBranching,                           // The bracnhing logic
        fixCostBounding,                                   // The bounding logic
        maxSolutions: 5,                                   // The desired solution count (optional)
        baseUnitSet: null,                                 // The limitation on search space (optional)
        timeLimit: TimeSpan.FromHours(2),                  // The runtime limit (optional)
        threadCount: 4);                                   // The thread count for the algorithm (optional)

### Run the algorithm and explore the solutions

    foreach (var solution in bnb.GetSolutionNetworks())
    {
        Console.WriteLine($"{solution.Units} -> {solution.ObjectiveValue}");
    }

### Or use the built-in, already prepared variations

    LinearPNSProblem problem = LinearPNSProblem.FromPGraphStudioFile("sevenunit.pgsx");
    AlgorithmMSG msg = new(problem);
    Console.WriteLine(msg.GetMaximalStructure());
    
    AlgorithmABBOrderedOpenList<LinearPNSProblem, LinearNetwork> abb = new(problem, LinearSubproblemBoundEfficient, maxSolutions: -1);
    foreach (var network in abb.GetSolutionNetworks())
    {
        Console.WriteLine($"{network.Units} -> {network.ObjectiveValue}");
    }

## Examples and tutorials

The examples folder contains sample projects that serve as tutorials. The samples each discuss certain parts and functionalities of the library.

The library does not explain the P-graph framework, nor its main algorithms. It is reasonable to expect that anyone interested in this library is already adequately familiar with the P-graph framework.

Current examples:  
- Example 1 (Basics): This example is the starting point, explaining the basic elements of the library as well as introducing algorithms MSG and SSG.
- Example 2 (Branch and Bound): Explains the library's branch-and-bound capabilities and options. Contains lots of text and explanation, but important to understand the basic operation of the library.
- Three examples showcase the three supported ways to add additional data the the PNS problem. Choose one or combine more the extend your data:
  - Example 3A (The quick way): Explains the quickest way to extend the data, in case there are only some small things to add. Suggested for smaller changes.
  - Example 3B (The efficient way): Explains the efficient way to extend the data, prioritizing type safety, code readibility, and computational efficiency. Suggested for larger changes.
  - Example 3C (The flexible way): Explains the flexible way to extend the data, focusing on reusability of components for multiple problems. Suggested if you expect to solve multiple problems of the same materials and operating units while varying the new data.
- The library provides compatibility with the main model of P-Graph Studio for convenience. It is available in both efficient-style and flexible-style implementation:
  - Example 4A (Linear PNS the efficient way): Showcases how to use the efficient implementation of the linear model.
  - Example 4B (Linear PNS the flexible way): Showcases how to use the flexible implementation of the linear model.
- Example 5 (Custom bounding): Explains the role of the bounding methods in more detail
- More examples are given to show the bounding methods in more levels:
  - Example 6A (Modified bounding result): A linear PNS problem is extended with CO2 production, which is calculated for each solution network. To do this, the bounding method wraps the standard bounding and reinterprets the results.
  - Example 6B (New LP model): Also extends the linear PNS problem with CO2 production, however, now the CO2 production is the objective function, while there is an upper bound for cost. This requires a modified LP model, network representation, and bounding method, resulting in a complex example.
- Example 7 (Custom branching): This sample explains the basics of creating custom branching logic.
