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
 * This example showcases the flexible-style extension.
 */

// Other than including the overall namespace, we also need to include the specific namespace for the flexible Linear PNS implementation.
using PGraphInCS;
using PGraphInCS.LinearPNS.Flexible;

/*
 * Following the form of flexible data extension, the material and operating unit classes are not changed.
 * Instead, a dedicated PNS problem class, LinearPNSProblem is created, which organizes the new data into 4 Dictionaries.
 * All additional data are optional, input/output flow rates default to 1. Note that the default upper bounds are 10000000, which is consistent with P-Graph Studio.
 */

MaterialNode m1 = new MaterialNode("M1");
MaterialNode m2 = new MaterialNode("M2");
MaterialNode m3 = new MaterialNode("M3");
MaterialNode m4 = new MaterialNode("M4");
OperatingUnitNode o1 = new OperatingUnitNode("O1", inputs: [m1], outputs: [m2, m3]);
OperatingUnitNode o2 = new OperatingUnitNode("O2", inputs: [m3, m4], outputs: [m2]);

LinearPNSProblem problem = new LinearPNSProblem();
problem.AddMaterial(m1);
problem.AddMaterial(m2);
problem.AddMaterial(m3);
problem.AddMaterial(m4);
problem.AddOperatingUnit(o1);
problem.AddOperatingUnit(o2);

problem.MaterialData[m1] = new LinearMaterialData { FlowRateUpperBound = 23.3, Price = 2.4 };
problem.MaterialData[m2] = new LinearMaterialData { FlowRateLowerBound = 12.3 };
problem.OperatingUnitData[o1] = new LinearOperatingUnitData { FixOperatingCost = 112.6, ProportionalOperatingCost = 9.7, CapacityUpperBound = 34.4 };
problem.OperatingUnitData[o2] = new LinearOperatingUnitData { FixOperatingCost = 65.6, ProportionalOperatingCost = 10.7, CapacityUpperBound = 27.8 };
problem.InputRatios.Add(o1, new() { { m1, 1.2 } });
problem.OutputRatios.Add(o1, new() { { m2, 0.7 }, { m3, 0.5 } });
problem.InputRatios.Add(o2, new() { { m3, 2.5 }, { m4, 1.4 } });
problem.OutputRatios.Add(o2, new() { { m2, 3.4 } });

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
 * The CommonImplementations class contains a default bounding method, LinearSubproblemBoundFlexible, which uses the common linear programming model (implemented in the SimpleLinearPNSLPModel class) and returns a solution network with the type LinearNetwork.
 * Naturally, it is possible to use any other compatible bounding methods.
 * The LinearNetwork class contains the usual information (included operating units and the bounding value / objective value), and extends it by also storing the generated capacities for each included operating unit.
 */
var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<LinearPNSProblem, LinearNetwork>(problem, CommonImplementations.LinearSubproblemBoundFlexible, maxSolutions: -1);
foreach (LinearNetwork network in abb.GetSolutionNetworks())
{
    Console.WriteLine($"Network (cost: {network.ObjectiveValue})");
    foreach (var (unit, capacity) in network.UnitCapacities)
    {
        Console.WriteLine($"    {capacity} * {unit.Name}");
    }
}