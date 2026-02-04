using PGraphInCS;

/*
 * The library starts from the very basics of P-graphs: materials and operating units. 
 * Materials only store their name and an automatically generated Id.
 * Operating units stote their name, an automatically generated Id, and the their inputs and outputs (materials).
 * The basic model does not include any data related to feasibility models, as those can change depending on the actual problem to be solved.
 */

// To start, first create the PNS problem, the materials, and the operating units.
SimplePNSProblem problem = new();

MaterialNode mA = new MaterialNode("A");
MaterialNode mB = new MaterialNode("B");
MaterialNode mC = new MaterialNode("C");
MaterialNode mD = new MaterialNode("D");
MaterialNode mE = new MaterialNode("E");
MaterialNode mF = new MaterialNode("F");
MaterialNode mG = new MaterialNode("G");
MaterialNode mH = new MaterialNode("H");
MaterialNode mJ = new MaterialNode("J");
MaterialNode mK = new MaterialNode("K");
MaterialNode mL = new MaterialNode("L");

OperatingUnitNode O1 = new OperatingUnitNode("O1", inputs: [mC], outputs: [mA, mF]);
OperatingUnitNode O2 = new OperatingUnitNode("O2", inputs: [mD], outputs: [mA, mB]);
OperatingUnitNode O3 = new OperatingUnitNode("O3", inputs: [mE, mF], outputs: [mC]);
OperatingUnitNode O4 = new OperatingUnitNode("O4", inputs: [mF, mG], outputs: [mC, mD]);
OperatingUnitNode O5 = new OperatingUnitNode("O5", inputs: [mG, mH], outputs: [mD]);
OperatingUnitNode O6 = new OperatingUnitNode("O6", inputs: [mJ], outputs: [mF]);
OperatingUnitNode O7 = new OperatingUnitNode("O7", inputs: [mK, mL], outputs: [mH]);

// Then, assign the materials and operating units to the PNS problem.
problem.AddMaterial(mA);
problem.AddMaterial(mB);
problem.AddMaterial(mC);
problem.AddMaterial(mD);
problem.AddMaterial(mE);
problem.AddMaterial(mF);
problem.AddMaterial(mG);
problem.AddMaterial(mH);
problem.AddMaterial(mJ);
problem.AddMaterial(mK);
problem.AddMaterial(mL);

problem.AddOperatingUnit(O1);
problem.AddOperatingUnit(O2);
problem.AddOperatingUnit(O3);
problem.AddOperatingUnit(O4);
problem.AddOperatingUnit(O5);
problem.AddOperatingUnit(O6);
problem.AddOperatingUnit(O7);


// The PNS problem needs to define its raw materials and products.
// It is important that this is stored in the PNS problem, not in the materials, as a material is not a raw material or product by nature, only in terms of a given PNS problem.
problem.SetRawMaterialsAndProducts(rawMaterials: new MaterialSet { mE, mG, mJ, mK, mL }, products: new MaterialSet { mA });


// It is important to always call the FinalizeData methods, as it performs additional work necessary for the algorithms.
problem.FinalizeData();


// Algorithm MSG can be performed through its designated class. The constructor needs the problem, and optionally a set of operating units to limit the operation to (see later in the example at Algorithm SSG).
var msg = new AlgorithmMSG(problem);
// The maxial structure is returned as a set of operating units
OperatingUnitSet msgUnits = msg.GetMaximalStructure();
Console.WriteLine("Maximal structure:");
// Printing an OperatingUnitSet or MaterialSet is simple. However, note that the default ToString() method does not guarantee any particular order of the items.
Console.WriteLine(msgUnits);
// If guaranteed order by id or name is required, use ToStringSortedById() or ToStringSortedByName(). In the current example all three methods result in the same order, but feel free to experiment and see the difference.
//Console.WriteLine(msgUnits.ToStringSortedById());
//Console.WriteLine(msgUnits.ToStringSortedByName());


Console.WriteLine();


// Algorithm SSG  generates solution structures. As with MSG, the constructor expects the problem, and optionally a set of operating units.
// Algorithm MSG is used Algorithm SSG as a first step, so it is not necessary to call it beforehand.
var ssg = new AlgorithmSSGRecursive(problem);
// The second parameter limits the algorithm to a given set of operating units, essentially considering all other operating units as excluded.
//var ssg = new AlgorithmSSGRecursive(problem, msgUnits);
Console.WriteLine("Solution structures:");
// The solution structures are returned as a list of operating unit sets
foreach (OperatingUnitSet structure in ssg.GetSolutionStructures())
{
    Console.WriteLine(structure);
}


Console.WriteLine();


// Although the basic PNS problem does not involve any feasibility data, it does consider structural constraints: mutually exclusive set and parallel production limits.
// These restrictions are considered by Algorithm SSG and all branch-and-bound algorithms included in the library. New algorithms are also advised to take these into account.

// It is possible to mark sets of operating units as mutually exclusive, meaning that only one of them can exists any solution. Any number of mutually exclusive set can be defined.
problem.AddMutuallyExclusiveSet(new OperatingUnitSet { O6, O7 });

// MaterialSet and OperatingUnitSet also provide functionality to find certain items by name or by id.
// FindByName and FindById return null if the item is not found, while the indexers throw an exception.
//problem.AddMutuallyExclusiveSet(new OperatingUnitSet { problem.OperatingUnits["O6"], problem.OperatingUnits.FindByName("O7")! });
//problem.AddMutuallyExclusiveSet(new OperatingUnitSet { problem.OperatingUnits[17], problem.OperatingUnits.FindById(18)! });

// Parallel production limit (i.e., at most how many operating units can produce a material in any solution) can be set by giving the material node variable, or just the material name.
problem.SetMaxParallelProduction(mC, 2); // This does not limit anything in the current case, since there are only two operating units producing material C anyway.
problem.SetMaxParallelProduction("A", 1); // A parallel production limit of 1 has the same effect as a mutual exclusion, but defined with a different meaning, and handled differently in code.

// Don't forget to call FinalizeData() !
problem.FinalizeData();

// Let's call Algorithm SSG again (need to make a new instance), and see the difference.
var ssg2 = new AlgorithmSSGRecursive(problem);
Console.WriteLine("Solution structures after applying restrictions:");
// The solution structures are returned as a list of operating unit sets
foreach (OperatingUnitSet structure in ssg2.GetSolutionStructures())
{
    Console.WriteLine(structure);
}