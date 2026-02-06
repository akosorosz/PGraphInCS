# Example 5: More detail about bounding functions

The concept of providing custom bounding methods has been introduced before. However, the bounding method does more than just determining an objective value.
The bounding method has 3 responsibilities:
- Determine feasibility: if the subproblem is infeasbile, the method should return null (structural feasibility is usually guaranteed by the branching, while the bounding examines non-structural feasbility, but you can deviate from this if necessary)
- Determine objective value: for leaf subproblems, the method should return the exact value of the objective function
- Determine bound: for intermediate subproblems, the method should return a lower bound which guarantees that no descendant subproblem has a lower objective value / bound

Note that it is not required to use a single number as the objective value. If the problem requires you can define the objective as a complex value with custom comparison logic.


In this example we continue the data extension from Examples 3A, 3B, and 3C, where all operating units had a fix cost assigned.
Here, we also assign a weight to each operating unit. Furthermore, a weight limit is introduced to the PNS problem, and the total weight of the operating units cannot exceed this limit.
For this code, we use the efficient extension of the operating units, while storing the weight limit in the PNS problem.

First, define the new classes

We need to store the new data for the operating units

    public class ExtendedOperatingUnit : OperatingUnitNode
    {
        public int Cost { get; set; } = 0;
        public int Weight { get; set; } = 0;
        public ExtendedOperatingUnit(string name, MaterialSet? inputs = null, MaterialSet? outputs = null, int cost = 0, int weight = 0) :
            base(name, inputs, outputs)
        {
            Cost = cost;
            Weight = weight;
        }
    }

The PNS problem needs to contain the weight limit

    public class WeightLimitPNSProblem : PNSProblem<MaterialNode, ExtendedOperatingUnit>
    {
        public int WeightLimit { get; set; } = 10000000;
    }

And we want to store the total weight in the network
We want to keep total cost as the main objective, but in case the cost is the same, we prefer networks with lower total weight, so we define a custom comparator

    public class WeightedNetwork : SimpleNetwork, IComparable<WeightedNetwork>
    {
        public int TotalWeight { get; set; }
        public WeightedNetwork(double objectiveValue, int totalWeight) : base(objectiveValue)
        {
            TotalWeight = totalWeight;
        }
    
        public WeightedNetwork(OperatingUnitSet units, double objectiveValue, int totalWeight) : base(units, objectiveValue)
        {
            TotalWeight = totalWeight;
        }
    
        public int CompareTo(WeightedNetwork? other)
        {
            if (other == null) return 1;
            if (this.ObjectiveValue.CompareTo(other.ObjectiveValue) != 0) return this.ObjectiveValue.CompareTo(other.ObjectiveValue);
            return this.TotalWeight.CompareTo(other.TotalWeight);
        }
    }

Let's use the same PNS problem introduced in Example 1, extended with the new data (the costs are different, to showcase the network ordering later)

    WeightLimitPNSProblem problem = getSampleProblem();

Set the weight limit

    problem.WeightLimit = 20;

Let's write the bounding method. It will do two things:
- Calculate the total cost of the included units
- Calculate the total weight of the included units, and mark the subproblem as infeasible if the weight is too high

Let's also make it generic, to make it compatible with any branching algorithm

    WeightedNetwork? boundMethod<SubproblemType>(SubproblemType subproblem)
        where SubproblemType : SubproblemBase<WeightLimitPNSProblem>, ISubpoblemWithIncludedExcludedGet
    {
        OperatingUnitSet includedUnits = subproblem.GetIncludedUnits();
        int totalWeigth = includedUnits.Cast<ExtendedOperatingUnit>().Sum(u => u.Weight);
        if (totalWeigth > subproblem.Problem.WeightLimit) return null;
        
        int totalCost = includedUnits.Cast<ExtendedOperatingUnit>().Sum(u => u.Cost);
        return new WeightedNetwork(includedUnits, totalCost, totalWeigth);
    }

Now, let's solve the problem with ABB, as that's the simpler to use

    var abb = new CommonImplementations.AlgorithmABBOrderedOpenList<WeightLimitPNSProblem, WeightedNetwork>(problem, boundMethod, maxSolutions: -1);
    foreach (WeightedNetwork network in abb.GetSolutionNetworks())
    {
        Console.WriteLine($"{network.Units,25} -- cost: {network.ObjectiveValue,3}, weight: {network.TotalWeight}");
    }