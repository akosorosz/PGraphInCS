/*
 * P-Graph Studio is the most known and used P-graph tool. One might notice that the default data model of this library is severely limited compared to P-Graph Studio.
 * The reason: this library aims to be general. The core algorithms focus on the structural properties and are independent of the underlying feasibility models. On the other hand, P-Graph Studio employs one fixed data model, which is simple, yet general enough for most common use-cases.
 * It is possible to implement the model used by P-Graph Studio into this library. And since P-Graph Studio is the most known P-graph tool, the library already includes these extensions to provide compatibility.
 * Not all feastures of P-Graph Studio are supported, but the library provides compatibility with the most common features.
 * 
 * The additional data for the included model are the following:
 * Each material has:
 *   Price
 *   Flow rate lower bound
 *   Flow rate upper bound
 * Each operating unit has:
 *   Capacity lower bound
 *   Capacity upper bound
 *   Investment cost function (fix part and proportional part)
 *   Operating cost function (fix part and proportional part)
 *   Payout period
 *   A flow rate for each input and output material
 * 
 * The example does not explain the detailed model, as anyone using P-Graph Studio should be familiar with it.
 * 
 * These implementations are found in the LinearPNS namespace, which is further split into two parts, as the library contains both an efficient-style and a flexible-style extension for the P-Graph Studio model.
 * 
 * This example showcases the efficient-style extension.
 */

// Other than including the overall namespace, we also need to include the specific namespace for the efficient Linear PNS implementation.
using PGraphInCS;
using PGraphInCS.LinearPNS.Efficient;

/*
 * Following the form of efficient data extension, the new parameters of materials and operating units are included in dedicated classes: LinearMaterialNode and LinearOperatingUnitNode
 * A dedicated PNS problem class, LinearPNSProblem is also given, which can only be filled with the extended materials and operating units.
 * The flow rates for the inputs and outputs for operating units must be given, all other new data are optional. Note that the default upper bounds are 10000000, which is consistent with P-Graph Studio.
 */

LinearMaterialNode m1 = new LinearMaterialNode("M1", flowRateUpperBound: 23.3, price: 2.4);
LinearMaterialNode m2 = new LinearMaterialNode("M2", flowRateLowerBound: 12.3);
LinearMaterialNode m3 = new LinearMaterialNode("M3");
LinearMaterialNode m4 = new LinearMaterialNode("M4");
LinearOperatingUnitNode o1 = new LinearOperatingUnitNode("O1", inputs: new() { { m1, 1.2 } }, outputs: new() { { m2, 0.7 }, { m3, 0.5 } }, fixOperatingCost: 112.6, proportionalOperatingCost: 9.7, capacityUpperBound: 34.4);
LinearOperatingUnitNode o2 = new LinearOperatingUnitNode("O2", inputs: new() { { m3, 2.5 }, { m4, 1.4 } }, outputs: new() { { m2, 3.4 } }, fixOperatingCost: 65.6, proportionalOperatingCost: 10.7, capacityUpperBound: 27.8);

LinearPNSProblem problem = new LinearPNSProblem();
problem.AddMaterial(m1);
problem.AddMaterial(m2);
problem.AddMaterial(m3);
problem.AddMaterial(m4);
problem.AddOperatingUnit(o1);
problem.AddOperatingUnit(o2);

problem.SetRawMaterialsAndProducts(rawMaterials: [m1, m4], products: [m2]);

problem.FinalizeData();

// Since P-Graph Studio is a commonly used software, the library also contains compatibility methods to read and write P-Graph Studio file formats. It supports both .pgsx and .in files
// Two files are attached for demonstration, both contain the same PNS problem from Example 1, extended with the new data. The .in file also contains mutual exclusions for demonstration.

// For example, you can load a .pgsx file into a new LinearPNSProblem instance
//LinearPNSProblem problem = LinearPNSProblem.FromPGraphStudioFile("sevenunit.pgsx");

// Or, you can load form a .in file
//LinearPNSProblem problem = LinearPNSProblem.FromPgraphSolverInputFile("sevenunit_me.in");

/*
 * The library also contains methods, such as
 * ExportToPGraphStudioFile: export a LinearPNSProblem to a .pgsx file (can optionally export generated solutions as well)
 * ExportToPgraphSolverInputFile: export a LinearPNSProblem to a .in file
 * AddSolutionsToPGraphStudioFile: add generated solutions to an existing .pgsx file (it assumes that the solutions were generated for the exact problem in the file)
 */
//problem.ExportToPGraphStudioFile("export.pgsx");

/*
 * To generate solutions for a linear PNS problem, we can use any of the branch-and-bound algorithms with the appropriate types and a bounding method.
 * The CommonImplementations class contains a default bounding method, LinearSubproblemBoundEfficient, which uses the common linear programming model (implemented in the SimpleLinearPNSLPModel class) and returns a solution network with the type LinearNetwork.
 * Naturally, it is possible to use any other compatible bounding methods.
 * The LinearNetwork class contains the usual information (included operating units and the bounding value / objective value), and extends it by also storing the generated capacities for each included operating unit.
 */
var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<LinearPNSProblem, LinearNetwork>(problem, CommonImplementations.LinearSubproblemBoundEfficient, maxSolutions: -1);
foreach (LinearNetwork network in abb.GetSolutionNetworks())
{
    Console.WriteLine($"Network (cost: {network.ObjectiveValue})");
    foreach (var (unit, capacity) in network.UnitCapacities)
    {
        Console.WriteLine($"    {capacity} * {unit.Name}");
    }
}