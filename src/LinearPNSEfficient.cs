using System.Xml;
using Google.OrTools.LinearSolver;
using static PGraphInCS.CommonImplementations;

namespace PGraphInCS.LinearPNS.Efficient;

public class LinearMaterialNode : MaterialNode
{
    public double FlowRateLowerBound { get; set; }
    public double FlowRateUpperBound { get; set; }
    public double Price { get; set; }
    public LinearMaterialNode(String name,
                double flowRateLowerBound = 0,
                double flowRateUpperBound = 10000000,
                double price = 0) : base(name)
    {
        this.FlowRateLowerBound = flowRateLowerBound;
        this.FlowRateUpperBound = flowRateUpperBound;
        this.Price = price;
    }
}

public class LinearOperatingUnitNode : OperatingUnitNode
{
    public double CapacityLowerBound { get; set; }
    public double CapacityUpperBound { get; set; }
    public double FixOperatingCost { get; set; }
    public double ProportionalOperatingCost { get; set; }
    public double FixInvestmentCost { get; set; }
    public double ProportionalInvestmentCost { get; set; }
    public double PayoutPeriod { get; set; }
    public Dictionary<MaterialNode, double> InputRatios { get; } = new();
    public Dictionary<MaterialNode, double> OutputRatios { get; } = new();
    public LinearOperatingUnitNode(String name, Dictionary<MaterialNode, double>? inputs = null, Dictionary<MaterialNode, double>? outputs = null,
                double capacityLowerBound = 0,
                double capacityUpperBound = 10000000,
                double fixOperatingCost = 0,
                double proportionalOperatingCost = 0,
                double fixInvestmentCost = 0,
                double proportionalInvestmentCost = 0,
                double payoutPeriod = 10) : base(name, null, null)
    {
        this.CapacityLowerBound = capacityLowerBound;
        this.CapacityUpperBound = capacityUpperBound;
        this.FixOperatingCost = fixOperatingCost;
        this.ProportionalOperatingCost = proportionalOperatingCost;
        this.FixInvestmentCost = fixInvestmentCost;
        this.ProportionalInvestmentCost = proportionalInvestmentCost;
        this.PayoutPeriod = payoutPeriod;
        if (inputs != null)
        {
            foreach (var (node, ratio) in inputs)
            {
                AddInput(node, ratio);
            }
        }
        if (outputs != null)
        {
            foreach (var (node, ratio) in outputs)
            {
                AddOutput(node, ratio);
            }
        }
    }
    public override void AddInput(MaterialNode node)
    {
        base.AddInput(node);
        InputRatios[node] = 1.0;
    }
    public override void AddOutput(MaterialNode node)
    {
        base.AddOutput(node);
        OutputRatios[node] = 1.0;
    }
    public void AddInput(MaterialNode node, double ratio = 1.0)
    {
        base.AddInput(node);
        InputRatios.Add(node, ratio);
    }

    public void AddOutput(MaterialNode node, double ratio = 1.0)
    {
        base.AddOutput(node);
        OutputRatios.Add(node, ratio);
    }
}

public class LinearPNSProblem : PNSProblem<LinearMaterialNode, LinearOperatingUnitNode>
{
    public static LinearPNSProblem FromPGraphStudioFile(string filename)
    {
        LinearPNSProblem graph = new LinearPNSProblem();
        XmlDocument xmlDocument = new XmlDocument();
        using (StreamReader sr = new StreamReader(filename))
        {
            xmlDocument.Load(sr);
        }
        if (xmlDocument.DocumentElement == null) { throw new ArgumentException("Invalid xml data"); }
        var defaultsNode = xmlDocument.DocumentElement.SelectSingleNode("Default");
        var materialsNode = xmlDocument.DocumentElement.SelectSingleNode("Materials");
        var unitsNode = xmlDocument.DocumentElement.SelectSingleNode("OperatingUnits");
        var edgesNode = xmlDocument.DocumentElement.SelectSingleNode("Edges");
        var mutexsNode = xmlDocument.DocumentElement.SelectSingleNode("MutualExclusions");

        if (defaultsNode == null) { throw new ArgumentException("Invalid xml data"); }
        if (materialsNode == null) { throw new ArgumentException("Invalid xml data"); }
        if (unitsNode == null) { throw new ArgumentException("Invalid xml data"); }
        if (edgesNode == null) { throw new ArgumentException("Invalid xml data"); }
        if (mutexsNode == null) { throw new ArgumentException("Invalid xml data"); }

        Dictionary<string, Dictionary<string, string>> defaultValues = new();
        foreach (XmlNode section in defaultsNode.ChildNodes)
        {
            Dictionary<string, string> values = new();
            foreach (XmlNode node in section.ChildNodes)
            {
                values.Add(node.Name, node.InnerText);
            }
            defaultValues.Add(section.Name, values);
        }

        Func<string, string, string, string> checkIfDefault = (value, section, option) =>
        {
            if (value != "-1") { return value; }
            if (defaultValues.TryGetValue(section, out var sectionDict))
            {
                if (sectionDict.TryGetValue(option, out var optionValue))
                {
                    return optionValue;
                }
            }
            throw new ArgumentException("Value should be default, but no default value given in file");
        };

        Func<XmlNode, string, string, string, string> getAttributeValue = (node, attribute, defaultSection, defaultOption) =>
        {
            string value = node.Attributes?[attribute]?.Value ?? "-1";
            return checkIfDefault(value, defaultSection, defaultOption);
        };

        Func<XmlNode, string, string, string, string> getParameterValue = (node, parameter, defaultSection, defaultOption) =>
        {
            string value = node.SelectSingleNode($"ParameterList/Parameter[@Name='{parameter}']")?.Attributes?["Value"]?.Value ?? "-1";
            return checkIfDefault(value, defaultSection, defaultOption);
        };

        Dictionary<string, LinearMaterialNode> materialIdToNode = new();
        Dictionary<string, LinearOperatingUnitNode> unitIdToNode = new();
        Dictionary<string, LinearMaterialNode> materialNameToNode = new();
        Dictionary<string, LinearOperatingUnitNode> unitNameToNode = new();

        MaterialSet rawMaterials = new MaterialSet();
        MaterialSet products = new MaterialSet();
        MaterialSet intermediates = new MaterialSet();
        MaterialSet allMaterials = new MaterialSet();
        foreach (XmlNode materialNode in materialsNode.ChildNodes)
        {
            string name = materialNode.Attributes?["Name"]?.Value ?? "";
            string id = materialNode.Attributes?["ID"]?.Value ?? "";
            string type = getAttributeValue(materialNode, "Type", "Material", "Type");
            double price = double.Parse(getParameterValue(materialNode, "price", "Material", "Price"));
            double minFlow = double.Parse(getParameterValue(materialNode, "reqflow", "Material", "FlowRateLowerBound"));
            double maxFlow = double.Parse(getParameterValue(materialNode, "maxflow", "Material", "FlowRateUpperBound"));

            LinearMaterialNode material = new(name, flowRateLowerBound: minFlow, flowRateUpperBound: maxFlow, price: price);
            allMaterials.Add(material);
            if (type == "0")
                rawMaterials.Add(material);
            else if (type == "1")
                intermediates.Add(material);
            else if (type == "2")
                products.Add(material);

            if (id != "") { materialIdToNode.Add(id, material); }
            if (name != "") { materialNameToNode.Add(name, material); }

            graph.AddMaterial(material);
        }

        OperatingUnitSet units = new OperatingUnitSet();
        foreach (XmlNode unitNode in unitsNode.ChildNodes)
        {
            string name = unitNode.Attributes?["Name"]?.Value ?? "";
            string id = unitNode.Attributes?["ID"]?.Value ?? "";
            double minCap = double.Parse(getParameterValue(unitNode, "caplower", "OperatingUnit", "CapacityLowerBound"));
            double maxCap = double.Parse(getParameterValue(unitNode, "capupper", "OperatingUnit", "CapacityUpperBound"));
            double opCostFix = double.Parse(getParameterValue(unitNode, "opercostfix", "OperatingUnit", "OperatingFixCost"));
            double opCostProp = double.Parse(getParameterValue(unitNode, "opercostprop", "OperatingUnit", "OperatingPropCost"));
            double invCostFix = double.Parse(getParameterValue(unitNode, "investcostfix", "OperatingUnit", "InvestmentFixCost"));
            double invCostProp = double.Parse(getParameterValue(unitNode, "investcostprop", "OperatingUnit", "InvestmentPropCost"));
            double payoutPeriod = double.Parse(getParameterValue(unitNode, "payoutperiod", "OperatingUnit", "PayoutPeriod"));

            LinearOperatingUnitNode unit = new(name, inputs: null, outputs: null, capacityLowerBound: minCap, capacityUpperBound: maxCap, fixOperatingCost: opCostFix, proportionalOperatingCost: opCostProp, fixInvestmentCost: invCostFix, proportionalInvestmentCost: invCostProp, payoutPeriod: payoutPeriod);
            units.Add(unit);

            if (id != "") { unitIdToNode.Add(id, unit); }
            if (name != "") { unitNameToNode.Add(name, unit); }

            graph.AddOperatingUnit(unit);
        }

        foreach (XmlNode edgeNode in edgesNode.ChildNodes)
        {
            string beginId = edgeNode.Attributes?["BeginID"]?.Value ?? "";
            string endId = edgeNode.Attributes?["EndID"]?.Value ?? "";
            double flowRate = double.Parse(getAttributeValue(edgeNode, "Rate", "Edge", "FlowRate"));
            if (unitIdToNode.ContainsKey(beginId))
            {
                unitIdToNode[beginId].AddOutput(materialIdToNode[endId], flowRate);
            }
            else
            {
                unitIdToNode[endId].AddInput(materialIdToNode[beginId], flowRate);
            }
        }

        foreach (XmlNode mutexNode in mutexsNode.ChildNodes)
        {
            XmlNode? unitList = mutexNode.SelectSingleNode("OperatingUnits");
            if (unitList != null)
            {
                OperatingUnitSet mutexUnits = new();
                foreach (XmlNode unitNode in unitList.ChildNodes)
                {
                    string unitName = unitNode.InnerText;
                    if (unitNameToNode.TryGetValue(unitName, out var unit))
                    {
                        mutexUnits.Add(unit);
                    }
                }
                if (mutexUnits.Count > 0)
                {
                    graph.AddMutuallyExclusiveSet(mutexUnits);
                }
            }
        }

        graph.SetRawMaterialsAndProducts(rawMaterials, products);
        graph.FinalizeData();

        return graph;
    }

    private enum InFileReadState
    {
        Nothing = 0,
        Defaults,
        Materials,
        Units,
        Edges,
        MutualExclusions
    }

    public static LinearPNSProblem FromPgraphSolverInputFile(string filename)
    {
        LinearPNSProblem graph = new LinearPNSProblem();
        using (StreamReader reader = new StreamReader(filename))
        {
            InFileReadState readState = InFileReadState.Nothing;
            Dictionary<string, string> defaultValues = new();

            Dictionary<string, LinearMaterialNode> materialNameToNode = new();
            Dictionary<string, LinearOperatingUnitNode> unitNameToNode = new();

            MaterialSet rawMaterials = new MaterialSet();
            MaterialSet products = new MaterialSet();
            MaterialSet intermediates = new MaterialSet();
            MaterialSet allMaterials = new MaterialSet();
            OperatingUnitSet units = new OperatingUnitSet();

            Func<Dictionary<string,string>, string, string, string> getValueOrDefault = (values, key, defaultKey) =>
            {
                if (values.TryGetValue(key, out var value))
                {
                    return value;
                }
                if (defaultValues.TryGetValue(defaultKey, out var value2))
                {
                    return value2;
                }
                throw new ArgumentException("Value should be default, but no default value given in file");
            };

            string? line = reader.ReadLine();
            while (line != null)
            {
                line = line.Trim();
                if (line == "")
                {
                    readState = InFileReadState.Nothing;
                }
                else if (readState == InFileReadState.Nothing)
                {
                    if (line == "defaults:")
                    {
                        readState = InFileReadState.Defaults;
                    }
                    else if (line == "materials:")
                    {
                        readState = InFileReadState.Materials;
                    }
                    else if (line == "operating_units:")
                    {
                        readState = InFileReadState.Units;
                    }
                    else if (line == "material_to_operating_unit_flow_rates:")
                    {
                        readState = InFileReadState.Edges;
                    }
                    else if (line == "mutually_exlcusive_sets_of_operating_units:")
                    {
                        readState = InFileReadState.MutualExclusions;
                    }
                }
                else if (readState == InFileReadState.Defaults)
                {
                    string[] parts = line.Split('=');
                    defaultValues.Add(parts[0], parts[1]);
                }
                else if (readState == InFileReadState.Materials)
                {
                    string[] parts1 = line.Split(": ");
                    string materialName = parts1[0];
                    Dictionary<string, string> materialProperties = new();
                    if (parts1.Length > 1)
                    {
                        string[] parts2 = parts1[1].Split(", ");
                        foreach (string part in parts2)
                        {
                            if (part.Contains('='))
                            {
                                string[] parts3 = part.Split("=");
                                materialProperties.Add(parts3[0], parts3[1]);
                            }
                            else
                            {
                                materialProperties.Add("type",part);
                            }
                        }
                    }

                    string materialType = getValueOrDefault(materialProperties, "type", "material_type");
                    double minFlow = double.Parse(getValueOrDefault(materialProperties, "flow_rate_lower_bound", "material_flow_rate_lower_bound"));
                    double maxFlow = double.Parse(getValueOrDefault(materialProperties, "flow_rate_upper_bound", "material_flow_rate_upper_bound"));
                    double price = double.Parse(getValueOrDefault(materialProperties, "price", "material_price"));

                    LinearMaterialNode material = new(materialName, flowRateLowerBound: minFlow, flowRateUpperBound: maxFlow, price: price);
                    allMaterials.Add(material);
                    if (materialType == "raw_material")
                        rawMaterials.Add(material);
                    else if (materialType == "intermediate")
                        intermediates.Add(material);
                    else if (materialType == "product")
                        products.Add(material);

                    if (materialName != "") { materialNameToNode.Add(materialName, material); }

                    graph.AddMaterial(material);
                }
                else if (readState == InFileReadState.Units)
                {
                    string[] parts1 = line.Split(": ");
                    string unitName = parts1[0];
                    Dictionary<string, string> unitProperties = new();
                    if (parts1.Length > 1)
                    {
                        string[] parts2 = parts1[1].Split(", ");
                        foreach (string part in parts2)
                        {
                            if (part.Contains('='))
                            {
                                string[] parts3 = part.Split("=");
                                unitProperties.Add(parts3[0], parts3[1]);
                            }
                        }
                    }

                    double minCap = double.Parse(getValueOrDefault(unitProperties, "capacity_lower_bound", "operating_unit_capacity_lower_bound"));
                    double maxCap = double.Parse(getValueOrDefault(unitProperties, "capacity_upper_bound", "operating_unit_capacity_upper_bound"));
                    double fixCost = double.Parse(getValueOrDefault(unitProperties, "fix_cost", "proportional_cost"));
                    double propCost = double.Parse(getValueOrDefault(unitProperties, "proportional_cost", "operating_unit_proportional_cost"));

                    LinearOperatingUnitNode unit = new(unitName, inputs: null, outputs: null, capacityLowerBound: minCap, capacityUpperBound: maxCap, fixOperatingCost: fixCost, proportionalOperatingCost: propCost);
                    units.Add(unit);

                    if (unitName != "") { unitNameToNode.Add(unitName, unit); }

                    graph.AddOperatingUnit(unit);
                }
                else if (readState == InFileReadState.Edges)
                {
                    string[] parts1 = line.Split(": ");
                    string unitName = parts1[0];
                    if (parts1.Length > 1)
                    {
                        string[] parts2 = parts1[1].Split(" => ");
                        foreach (string input in parts2[0].Split(" + "))
                        {
                            string[] parts3 = input.Split(" ");
                            if (parts3.Length == 1)
                            {
                                unitNameToNode[unitName].AddInput(materialNameToNode[parts3[0]], 1.0);
                            }
                            else
                            {
                                unitNameToNode[unitName].AddInput(materialNameToNode[parts3[1]], double.Parse(parts3[0]));
                            }
                        }
                        foreach (string output in parts2[1].Split(" + "))
                        {
                            string[] parts3 = output.Split(" ");
                            if (parts3.Length == 1)
                            {
                                unitNameToNode[unitName].AddOutput(materialNameToNode[parts3[0]], 1.0);
                            }
                            else
                            {
                                unitNameToNode[unitName].AddOutput(materialNameToNode[parts3[1]], double.Parse(parts3[0]));
                            }
                        }
                    }
                }
                else if (readState == InFileReadState.MutualExclusions)
                {
                    string[] parts = line.Split(": ");
                    if (parts.Length > 1)
                    {
                        OperatingUnitSet mutexUnits = new OperatingUnitSet();
                        foreach (string unitName in parts[1].Split(", "))
                        {
                            mutexUnits.Add(unitNameToNode[unitName]);
                        }
                        graph.AddMutuallyExclusiveSet(mutexUnits);
                    }
                }

                line = reader.ReadLine();
            }

            graph.SetRawMaterialsAndProducts(rawMaterials, products);
            graph.FinalizeData();
        }
        return graph;
    }

    public void ExportToPGraphStudioFile(string filename, IEnumerable<LinearNetwork>? solutions = null)
    {
        // initialization
        XmlDocument document = new XmlDocument();
        XmlDeclaration declaration = document.CreateXmlDeclaration("1.0", "utf-16", null);
        document.AppendChild(declaration);
        XmlElement root = document.CreateElement("PGraph");
        root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
        root.SetAttribute("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
        root.SetAttribute("Type", "PNS");
        root.SetAttribute("Visible", "true");
        root.SetAttribute("PeriodExtension", "false");
        document.AppendChild(root);

        // frame
        XmlElement defaultElement = document.CreateElement("Default");
        root.AppendChild(defaultElement);
        XmlElement materialsElement = document.CreateElement("Materials");
        root.AppendChild(materialsElement);
        XmlElement edgesElement = document.CreateElement("Edges");
        root.AppendChild(edgesElement);
        XmlElement unitsElement = document.CreateElement("OperatingUnits");
        root.AppendChild(unitsElement);
        XmlElement mutualExclusionsElement = document.CreateElement("MutualExclusions");
        root.AppendChild(mutualExclusionsElement);
        XmlElement solutionsElement = document.CreateElement("Solutions");
        root.AppendChild(solutionsElement);
        root.AppendChild(document.CreateElement("Periods"));
        XmlElement multiexceptElement = document.CreateElement("MultiExcept");
        multiexceptElement.AppendChild(document.CreateElement("ExceptMats"));
        multiexceptElement.AppendChild(document.CreateElement("ExceptOps"));
        root.AppendChild(multiexceptElement);
        root.AppendChild(document.CreateElement("Storages"));

        Func<string, string, XmlElement> createSingleValueElement = (tag, value) =>
        {
            XmlElement element = document.CreateElement(tag);
            element.InnerText = value;
            return element;
        };

        // default values
        Dictionary<string, Dictionary<string, string>> defaultValues = new()
        {
            ["Material"] = new()
            {
                ["FlowRateLowerBound"] = "0",
                ["FlowRateUpperBound"] = "10000000",
                ["Price"] = "0",
                ["Type"] = "1",
                ["Deadline"] = "31536000",
                ["EarliestAvability"] = "0",
                ["StorageStrategy"] = "default"
            },
            ["OperatingUnit"] = new()
            {
                ["OperatingFixCost"] = "0",
                ["InvestmentFixCost"] = "0",
                ["OpUnitFixCost"] = "0",
                ["OperatingPropCost"] = "0",
                ["InvestmentPropCost"] = "0",
                ["OpUnitPropCost"] = "0",
                ["CapacityLowerBound"] = "0",
                ["CapacityUpperBound"] = "10000000",
                ["PayoutPeriod"] = "10",
                ["WorkingHoursPerYear"] = "8000",
                ["FixOperTime"] = "0",
                ["PropOperTime"] = "0",
                ["EarliestAvability"] = "0",
                ["LatestAvability"] = "31536000",
                ["RelaxMode"] = "strong"
            },
            ["Edge"] = new()
            {
                ["FlowRate"] = "1"
            },
            ["Quantity"] = new()
            {
                ["default_mes"] = "ton (t)",
                ["time_mu"] = "y",
                ["quant_type"] = "Mass",
                ["money_mu"] = "EUR"
            },
            ["SolverParameter"] = new()
            {
                ["MakespanCoefficient"] = "0",
                ["CostCoefficient"] = "0",
                ["TimeCoefficient"] = "0",
                ["TotalCostLowerBound"] = "-1000000000",
                ["TotalCostUpperBound"] = "1000000000"
            }
        };
        foreach (var (section, values) in defaultValues)
        {
            XmlElement defaultSection = document.CreateElement(section);
            foreach (var (option, value) in values)
            {
                defaultSection.AppendChild(createSingleValueElement(option, value));
            }
            defaultElement.AppendChild(defaultSection);
        }

        Func<string, string, string, XmlElement> createParameterElement = (name, prefix, value) =>
        {
            XmlElement element = document.CreateElement("Parameter");
            element.SetAttribute("Name", name);
            element.SetAttribute("Prefix", prefix);
            element.SetAttribute("Value", value);
            element.SetAttribute("Visible", "false");
            return element;
        };

        Func<string, string, string, string> getValueOrDefault = (value, section, option) =>
        {
            if (value == defaultValues[section][option])
                return "-1";
            else
                return value;
        };

        int idmax = 0;

        // materials
        foreach (LinearMaterialNode material in Materials)
        {
            idmax = Math.Max(idmax, material.Id);

            string materialTypeCode = "1";
            if (RawMaterials.Contains(material)) materialTypeCode = "0";
            else if (Products.Contains(material)) materialTypeCode = "2";
            XmlElement materialElement = document.CreateElement("Material");
            materialElement.SetAttribute("ID", material.Id.ToString());
            materialElement.SetAttribute("Name", material.Name.Replace(" ","_"));
            materialElement.SetAttribute("Type", materialTypeCode);

            XmlElement parameterListElement = document.CreateElement("ParameterList");
            parameterListElement.AppendChild(createParameterElement("price", "Price: ", getValueOrDefault(material.Price.ToString(),"Material","Price")));
            parameterListElement.AppendChild(createParameterElement("reqflow", "Required flow: ", getValueOrDefault(material.FlowRateLowerBound.ToString(), "Material", "FlowRateLowerBound")));
            parameterListElement.AppendChild(createParameterElement("maxflow", "Maximum flow: ", getValueOrDefault(material.FlowRateUpperBound.ToString(), "Material", "FlowRateUpperBound")));
            parameterListElement.AppendChild(createParameterElement("quantitytype", "Quantity type: ", "Mass"));
            parameterListElement.AppendChild(createParameterElement("measurementunit", "Measurement unit: ", "ton (t)"));
            materialElement.AppendChild(parameterListElement);

            XmlElement coordsElement = document.CreateElement("Coords");
            coordsElement.AppendChild(createSingleValueElement("X", "0"));
            coordsElement.AppendChild(createSingleValueElement("Y", "0"));
            materialElement.AppendChild(coordsElement);

            XmlElement labelElement = document.CreateElement("Label");
            labelElement.SetAttribute("Text", material.Name.Replace(" ", "_"));
            XmlElement labelOffsetElement = document.CreateElement("Offset");
            labelOffsetElement.AppendChild(createSingleValueElement("X", "103"));
            labelOffsetElement.AppendChild(createSingleValueElement("Y", "-100"));
            labelElement.AppendChild(labelOffsetElement);
            labelElement.AppendChild(createSingleValueElement("FontSize", "-1"));
            labelElement.AppendChild(createSingleValueElement("Color", "-16777216"));
            materialElement.AppendChild(labelElement);

            XmlElement commentElement = document.CreateElement("Comment");
            commentElement.SetAttribute("Text", "");
            XmlElement commentOffsetElement = document.CreateElement("Offset");
            commentOffsetElement.AppendChild(createSingleValueElement("X", "103"));
            commentOffsetElement.AppendChild(createSingleValueElement("Y", "20"));
            commentElement.AppendChild(commentOffsetElement);
            commentElement.AppendChild(createSingleValueElement("FontSize", "-1"));
            commentElement.AppendChild(createSingleValueElement("Color", "-16777216"));
            materialElement.AppendChild(commentElement);

            XmlElement parametersElement = document.CreateElement("Parameters");
            parametersElement.SetAttribute("Text", "");
            XmlElement parametersOffsetElement = document.CreateElement("Offset");
            parametersOffsetElement.AppendChild(createSingleValueElement("X", "103"));
            parametersOffsetElement.AppendChild(createSingleValueElement("Y", "140"));
            parametersElement.AppendChild(parametersOffsetElement);
            parametersElement.AppendChild(createSingleValueElement("FontSize", "-1"));
            parametersElement.AppendChild(createSingleValueElement("Color", "-16777216"));
            materialElement.AppendChild(parametersElement);

            materialElement.AppendChild(createSingleValueElement("Color", "-16777216"));

            materialsElement.AppendChild(materialElement);
        }
        
        // operating units
        foreach (LinearOperatingUnitNode unit in OperatingUnits)
        {
            idmax = Math.Max(idmax, unit.Id);

            XmlElement unitElement = document.CreateElement("OperatingUnit");
            unitElement.SetAttribute("ID", unit.Id.ToString());
            unitElement.SetAttribute("Name", unit.Name.Replace(" ", "_"));
            unitElement.SetAttribute("Title", "");

            XmlElement parameterListElement = document.CreateElement("ParameterList");
            parameterListElement.AppendChild(createParameterElement("caplower", "Capacity, lower bound: ", getValueOrDefault(unit.CapacityLowerBound.ToString(), "OperatingUnit", "CapacityLowerBound")));
            parameterListElement.AppendChild(createParameterElement("capupper", "Capacity, upper bound: ", getValueOrDefault(unit.CapacityUpperBound.ToString(), "OperatingUnit", "CapacityUpperBound")));
            parameterListElement.AppendChild(createParameterElement("investcostfix", "Investment cost, fix: ", getValueOrDefault(unit.FixInvestmentCost.ToString(), "OperatingUnit", "InvestmentFixCost")));
            parameterListElement.AppendChild(createParameterElement("investcostprop", "Investment cost, proportional: ", getValueOrDefault(unit.ProportionalInvestmentCost.ToString(), "OperatingUnit", "InvestmentPropCost")));
            parameterListElement.AppendChild(createParameterElement("opercostfix", "Operating cost, fix: ", getValueOrDefault(unit.FixOperatingCost.ToString(), "OperatingUnit", "OperatingFixCost")));
            parameterListElement.AppendChild(createParameterElement("opercostprop", "Operating cost, proportional: ", getValueOrDefault(unit.ProportionalOperatingCost.ToString(), "OperatingUnit", "OperatingPropCost")));
            parameterListElement.AppendChild(createParameterElement("workinghour", "Working hours per year: ", "-1"));
            parameterListElement.AppendChild(createParameterElement("payoutperiod", "Payout Period: ", getValueOrDefault(unit.PayoutPeriod.ToString(), "OperatingUnit", "PayoutPeriod")));
            unitElement.AppendChild(parameterListElement);

            XmlElement coordsElement = document.CreateElement("Coords");
            coordsElement.AppendChild(createSingleValueElement("X", "0"));
            coordsElement.AppendChild(createSingleValueElement("Y", "0"));
            unitElement.AppendChild(coordsElement);

            XmlElement labelElement = document.CreateElement("Label");
            labelElement.SetAttribute("Text", unit.Name.Replace(" ", "_"));
            XmlElement labelOffsetElement = document.CreateElement("Offset");
            labelOffsetElement.AppendChild(createSingleValueElement("X", "378"));
            labelOffsetElement.AppendChild(createSingleValueElement("Y", "-50"));
            labelElement.AppendChild(labelOffsetElement);
            labelElement.AppendChild(createSingleValueElement("FontSize", "-1"));
            labelElement.AppendChild(createSingleValueElement("Color", "-16777216"));
            unitElement.AppendChild(labelElement);

            XmlElement commentElement = document.CreateElement("Comment");
            commentElement.SetAttribute("Text", "");
            XmlElement commentOffsetElement = document.CreateElement("Offset");
            commentOffsetElement.AppendChild(createSingleValueElement("X", "378"));
            commentOffsetElement.AppendChild(createSingleValueElement("Y", "70"));
            commentElement.AppendChild(commentOffsetElement);
            commentElement.AppendChild(createSingleValueElement("FontSize", "-1"));
            commentElement.AppendChild(createSingleValueElement("Color", "-16777216"));
            unitElement.AppendChild(commentElement);

            XmlElement parametersElement = document.CreateElement("Parameters");
            parametersElement.SetAttribute("Text", "");
            XmlElement parametersOffsetElement = document.CreateElement("Offset");
            parametersOffsetElement.AppendChild(createSingleValueElement("X", "378"));
            parametersOffsetElement.AppendChild(createSingleValueElement("Y", "190"));
            parametersElement.AppendChild(parametersOffsetElement);
            parametersElement.AppendChild(createSingleValueElement("FontSize", "-1"));
            parametersElement.AppendChild(createSingleValueElement("Color", "-16777216"));
            unitElement.AppendChild(parametersElement);

            unitElement.AppendChild(createSingleValueElement("Color", "-16777216"));

            unitsElement.AppendChild(unitElement);
        }

        // edges
        foreach (LinearOperatingUnitNode unit in OperatingUnits)
        {
            foreach (var (material, ratio) in unit.InputRatios)
            {
                int edgeid = idmax + 1;
                idmax++;
                XmlElement edgeElement = document.CreateElement("Edge");
                edgeElement.SetAttribute("ID", edgeid.ToString());
                edgeElement.SetAttribute("BeginID", material.Id.ToString());
                edgeElement.SetAttribute("EndID", unit.Id.ToString());
                edgeElement.SetAttribute("Rate", getValueOrDefault(ratio.ToString(), "Edge", "FlowRate"));
                edgeElement.SetAttribute("Title", $"{ratio.ToString()} t/y");
                edgeElement.SetAttribute("ArrowOnCenter", "true");
                edgeElement.SetAttribute("ArrowPosition", "50");

                edgeElement.AppendChild(document.CreateElement("Nodes"));

                XmlElement labelElement = document.CreateElement("Label");
                labelElement.SetAttribute("Text", $"{ratio.ToString()} t/y");
                XmlElement labelOffsetElement = document.CreateElement("Offset");
                labelOffsetElement.AppendChild(createSingleValueElement("X", "5"));
                labelOffsetElement.AppendChild(createSingleValueElement("Y", "0"));
                labelElement.AppendChild(labelOffsetElement);
                labelElement.AppendChild(createSingleValueElement("FontSize", "-1"));
                labelElement.AppendChild(createSingleValueElement("Color", "-16777216"));
                edgeElement.AppendChild(labelElement);

                edgeElement.AppendChild(createSingleValueElement("Color", "-16777216"));
                edgeElement.AppendChild(createSingleValueElement("LongFormat", "false"));
                edgeElement.AppendChild(document.CreateElement("Comment"));
                edgeElement.AppendChild(createSingleValueElement("CommentVisible", "false"));

                edgesElement.AppendChild(edgeElement);
            }

            foreach (var (material, ratio) in unit.OutputRatios)
            {
                int edgeid = idmax + 1;
                idmax++;
                XmlElement edgeElement = document.CreateElement("Edge");
                edgeElement.SetAttribute("ID", edgeid.ToString());
                edgeElement.SetAttribute("BeginID", unit.Id.ToString());
                edgeElement.SetAttribute("EndID", material.Id.ToString());
                edgeElement.SetAttribute("Rate", getValueOrDefault(ratio.ToString(), "Edge", "FlowRate"));
                edgeElement.SetAttribute("Title", $"{ratio.ToString()} t/y");
                edgeElement.SetAttribute("ArrowOnCenter", "true");
                edgeElement.SetAttribute("ArrowPosition", "50");

                edgeElement.AppendChild(document.CreateElement("Nodes"));

                XmlElement labelElement = document.CreateElement("Label");
                labelElement.SetAttribute("Text", $"{ratio.ToString()} t/y");
                XmlElement labelOffsetElement = document.CreateElement("Offset");
                labelOffsetElement.AppendChild(createSingleValueElement("X", "5"));
                labelOffsetElement.AppendChild(createSingleValueElement("Y", "0"));
                labelElement.AppendChild(labelOffsetElement);
                labelElement.AppendChild(createSingleValueElement("FontSize", "-1"));
                labelElement.AppendChild(createSingleValueElement("Color", "-16777216"));
                edgeElement.AppendChild(labelElement);

                edgeElement.AppendChild(createSingleValueElement("Color", "-16777216"));
                edgeElement.AppendChild(createSingleValueElement("LongFormat", "false"));
                edgeElement.AppendChild(document.CreateElement("Comment"));
                edgeElement.AppendChild(createSingleValueElement("CommentVisible", "false"));

                edgesElement.AppendChild(edgeElement);
            }
        }

        // mutual exlusions
        foreach (OperatingUnitSet units in MutuallyExclusiveSets)
        {
            int setid = idmax + 1;
            idmax++;
            XmlElement setElement = document.CreateElement("MutualExclusion");
            setElement.SetAttribute("ID", idmax.ToString());
            setElement.SetAttribute("Name", $"Mutex_{idmax}");

            XmlElement mutexUnitsElement = document.CreateElement("OperatingUnits");
            foreach (OperatingUnitNode unit in units)
            {
                mutexUnitsElement.AppendChild(createSingleValueElement("OperatingUnit", unit.Name.Replace(" ", "_")));
            }
            setElement.AppendChild(mutexUnitsElement);

            mutualExclusionsElement.AppendChild(setElement);
        }

        if (solutions != null)
        {
            Func<LinearMaterialNode, double, double, string, XmlElement> createSolutionMaterialElement = (linearMaterial, flow, cost, MU) =>
            {
                XmlElement materialElement = document.CreateElement("Material");
                materialElement.SetAttribute("Name", linearMaterial.Name.Replace(" ", "_"));
                materialElement.SetAttribute("Flow", flow.ToString());
                materialElement.SetAttribute("Cost", cost.ToString());
                materialElement.SetAttribute("MU", MU);
                return materialElement;
            };

            int solutionIndex = 0;
            foreach (LinearNetwork network in solutions)
            {
                XmlElement solutionElement = document.CreateElement("Solution");
                solutionElement.SetAttribute("Index", solutionIndex.ToString());
                solutionElement.SetAttribute("Title", $"Feasible structure #{solutionIndex+1}");
                solutionElement.SetAttribute("OptimalValue", network.ObjectiveValue.ToString());
                solutionElement.SetAttribute("TotalTime", "0");
                solutionElement.SetAttribute("TotalMakespan", "0");
                solutionElement.SetAttribute("ObjectiveValue", "0");
                solutionElement.SetAttribute("AlgorithmUsed", "OTHER");

                XmlElement solutionMaterialsElement = document.CreateElement("Materials");
                solutionElement.AppendChild(solutionMaterialsElement);

                XmlElement solutionUnitsElement = document.CreateElement("OperatingUnits");
                solutionElement.AppendChild(solutionUnitsElement);

                Dictionary<LinearMaterialNode, double> materialFlows = new();

                foreach (var (unit, capacity) in network.UnitCapacities)
                {
                    LinearOperatingUnitNode linearUnit = (unit as LinearOperatingUnitNode)!;
                    XmlElement unitElement = document.CreateElement("OperatingUnit");
                    unitElement.SetAttribute("Name", linearUnit.Name.Replace(" ", "_"));
                    unitElement.SetAttribute("Size", capacity.ToString());
                    unitElement.SetAttribute("Cost", ((linearUnit.FixInvestmentCost + linearUnit.ProportionalInvestmentCost * capacity) / linearUnit.PayoutPeriod + linearUnit.FixOperatingCost + linearUnit.ProportionalOperatingCost * capacity).ToString());
                    unitElement.SetAttribute("MU", "t/y");
                    XmlElement inputsElement = document.CreateElement("Input");
                    foreach (var (material, ratio) in linearUnit.InputRatios)
                    {
                        LinearMaterialNode linearMaterial = (material as LinearMaterialNode)!;
                        var flow = ratio * capacity;
                        if (materialFlows.ContainsKey(linearMaterial))
                            materialFlows[linearMaterial] -= flow;
                        else
                            materialFlows.Add(linearMaterial, -flow);
                        inputsElement.AppendChild(createSolutionMaterialElement(linearMaterial, flow, 0.0, "t/y"));
                    }
                    unitElement.AppendChild(inputsElement);
                    XmlElement outputsElement = document.CreateElement("Output");
                    foreach (var (material, ratio) in linearUnit.OutputRatios)
                    {
                        LinearMaterialNode linearMaterial = (material as LinearMaterialNode)!;
                        var flow = ratio * capacity;
                        if (materialFlows.ContainsKey(linearMaterial))
                            materialFlows[linearMaterial] += flow;
                        else
                            materialFlows.Add(linearMaterial, flow);
                        outputsElement.AppendChild(createSolutionMaterialElement(linearMaterial, flow, 0.0, "t/y"));
                    }
                    unitElement.AppendChild(outputsElement);
                    solutionUnitsElement.AppendChild(unitElement);
                }

                foreach (var (linearMaterial, flow) in materialFlows)
                {
                    solutionMaterialsElement.AppendChild(createSolutionMaterialElement(linearMaterial, flow, -flow * linearMaterial.Price, "t/y"));
                }

                solutionsElement.AppendChild(solutionElement);
                solutionIndex++;
            }
        }


        // write to file
        document.Save(filename);
    }

    public void ExportToPgraphSolverInputFile(string filename)
    {
        using (StreamWriter writer = new StreamWriter(filename))
        {
            writer.WriteLine("file_type=PNS_problem_v1");
            writer.WriteLine($"file_name={filename.Replace(" ", "_").Replace(".", "_")}");
            writer.WriteLine();
            writer.WriteLine("measurement_units:");
            writer.WriteLine("mass_unit=t");
            writer.WriteLine("time_unit=m");
            writer.WriteLine("money_unit=EUR");
            writer.WriteLine();
            writer.WriteLine("defaults:");
            writer.WriteLine("material_type=intermediate");
            writer.WriteLine("material_flow_rate_lower_bound=0");
            writer.WriteLine("material_flow_rate_upper_bound=10000000");
            writer.WriteLine("material_price=0");
            writer.WriteLine("operating_unit_capacity_lower_bound=0");
            writer.WriteLine("operating_unit_capacity_upper_bound=10000000");
            writer.WriteLine("operating_unit_fix_cost=0");
            writer.WriteLine("operating_unit_proportional_cost=0");
            writer.WriteLine();
            writer.WriteLine("materials:");
            foreach (LinearMaterialNode material in Materials)
            {
                string line = material.Name.Replace(" ", "_") + ": ";
                if (RawMaterials.Contains(material)) line += "raw_material";
                else if (Products.Contains(material)) line += "product";
                else line += "intermediate";
                if (material.FlowRateLowerBound != 0)
                {
                    line += ", ";
                    line += "flow_rate_lower_bound=" + material.FlowRateLowerBound;
                }
                if (material.FlowRateUpperBound != 10000000)
                {
                    line += ", ";
                    line += "flow_rate_upper_bound=" + material.FlowRateUpperBound;
                }
                if (material.Price != 0)
                {
                    line += ", ";
                    line += "price=" + material.Price;
                }
                writer.WriteLine(line);
            }
            writer.WriteLine();
            writer.WriteLine("operating_units:");
            foreach (LinearOperatingUnitNode unit in OperatingUnits)
            {
                string line = unit.Name.Replace(" ", "_");
                bool first = true;
                if (unit.CapacityLowerBound != 0)
                {
                    if (first) line += ": ";
                    else line += ", ";
                    first = false;
                    line += "capacity_lower_bound=" + unit.CapacityLowerBound;
                }
                if (unit.CapacityUpperBound != 10000000)
                {
                    if (first) line += ": ";
                    else line += ", ";
                    first = false;
                    line += "capacity_upper_bound=" + unit.CapacityUpperBound;
                }
                if (unit.FixOperatingCost != 0 || unit.FixInvestmentCost != 0)
                {
                    if (first) line += ": ";
                    else line += ", ";
                    first = false;
                    line += "fix_cost=" + (unit.FixOperatingCost + unit.FixInvestmentCost / unit.PayoutPeriod);
                }
                if (unit.ProportionalOperatingCost != 0 || unit.ProportionalInvestmentCost != 0)
                {
                    if (first) line += ": ";
                    else line += ", ";
                    first = false;
                    line += "proportional_cost=" + (unit.ProportionalOperatingCost + unit.ProportionalInvestmentCost / unit.PayoutPeriod);
                }
                writer.WriteLine(line);
            }
            writer.WriteLine();
            writer.WriteLine("material_to_operating_unit_flow_rates:");
            foreach (LinearOperatingUnitNode unit in OperatingUnits)
            {
                string line = unit.Name.Replace(" ", "_");
                line += ":";
                bool first = true;
                foreach (var (material,ratio) in unit.InputRatios)
                {
                    if (!first) line += " +";
                    first = false;
                    if (ratio != 1) line += " " + ratio;
                    line += " " + material.Name.Replace(" ", "_");
                }
                line += " =>";
                first = true;
                foreach (var (material, ratio) in unit.OutputRatios)
                {
                    if (!first) line += " +";
                    first = false;
                    if (ratio != 1) line += " " + ratio;
                    line += " " + material.Name.Replace(" ", "_");
                }
                writer.WriteLine(line);
            }
            writer.WriteLine();
            if (MutuallyExclusiveSets.Count > 0)
            {
                writer.WriteLine("mutually_exlcusive_sets_of_operating_units:");
                int me_index = 0;
                foreach (OperatingUnitSet units in MutuallyExclusiveSets)
                {
                    me_index++;
                    writer.WriteLine($"ME_{me_index}: {string.Join(", ", units.Select(u => u.Name))}");
                }
                writer.WriteLine();
            }
        }
    }

    public void AddSolutionsToPGraphStudioFile(string filename, IEnumerable<LinearNetwork> solutions)
    {
        LinearPNSProblem graph = new LinearPNSProblem();
        XmlDocument document = new XmlDocument();
        using (StreamReader sr = new StreamReader(filename))
        {
            document.Load(sr);
        }
        if (document.DocumentElement == null) { throw new ArgumentException("Invalid xml data"); }
        var defaultsElement = document.DocumentElement.SelectSingleNode("Default");
        var materialsElement = document.DocumentElement.SelectSingleNode("Materials");
        var unitsElement = document.DocumentElement.SelectSingleNode("OperatingUnits");
        var solutionsElement = document.DocumentElement.SelectSingleNode("Solutions");

        if (defaultsElement == null) { throw new ArgumentException("Invalid xml data"); }
        if (materialsElement == null) { throw new ArgumentException("Invalid xml data"); }
        if (unitsElement == null) { throw new ArgumentException("Invalid xml data"); }
        if (solutionsElement == null) { throw new ArgumentException("Invalid xml data"); }

        solutionsElement.RemoveAll();

        Func<LinearMaterialNode, double, double, string, XmlElement> createSolutionMaterialElement = (linearMaterial, flow, cost, MU) =>
        {
            XmlElement materialElement = document.CreateElement("Material");
            materialElement.SetAttribute("Name", linearMaterial.Name.Replace(" ", "_"));
            materialElement.SetAttribute("Flow", flow.ToString());
            materialElement.SetAttribute("Cost", cost.ToString());
            materialElement.SetAttribute("MU", MU);
            return materialElement;
        };

        int solutionIndex = 0;
        foreach (LinearNetwork network in solutions)
        {
            XmlElement solutionElement = document.CreateElement("Solution");
            solutionElement.SetAttribute("Index", solutionIndex.ToString());
            solutionElement.SetAttribute("Title", $"Feasible structure #{solutionIndex + 1}");
            solutionElement.SetAttribute("OptimalValue", network.ObjectiveValue.ToString());
            solutionElement.SetAttribute("TotalTime", "0");
            solutionElement.SetAttribute("TotalMakespan", "0");
            solutionElement.SetAttribute("ObjectiveValue", "0");
            solutionElement.SetAttribute("AlgorithmUsed", "OTHER");

            XmlElement solutionMaterialsElement = document.CreateElement("Materials");
            solutionElement.AppendChild(solutionMaterialsElement);

            XmlElement solutionUnitsElement = document.CreateElement("OperatingUnits");
            solutionElement.AppendChild(solutionUnitsElement);

            Dictionary<LinearMaterialNode, double> materialFlows = new();

            foreach (var (unit, capacity) in network.UnitCapacities)
            {
                LinearOperatingUnitNode linearUnit = (unit as LinearOperatingUnitNode)!;
                XmlElement unitElement = document.CreateElement("OperatingUnit");
                unitElement.SetAttribute("Name", linearUnit.Name.Replace(" ", "_"));
                unitElement.SetAttribute("Size", capacity.ToString());
                unitElement.SetAttribute("Cost", ((linearUnit.FixInvestmentCost + linearUnit.ProportionalInvestmentCost * capacity) / linearUnit.PayoutPeriod + linearUnit.FixOperatingCost + linearUnit.ProportionalOperatingCost * capacity).ToString());
                unitElement.SetAttribute("MU", "t/y");
                XmlElement inputsElement = document.CreateElement("Input");
                foreach (var (material, ratio) in linearUnit.InputRatios)
                {
                    LinearMaterialNode linearMaterial = (material as LinearMaterialNode)!;
                    var flow = ratio * capacity;
                    if (materialFlows.ContainsKey(linearMaterial))
                        materialFlows[linearMaterial] -= flow;
                    else
                        materialFlows.Add(linearMaterial, -flow);
                    inputsElement.AppendChild(createSolutionMaterialElement(linearMaterial, flow, 0.0, "t/y"));
                }
                unitElement.AppendChild(inputsElement);
                XmlElement outputsElement = document.CreateElement("Output");
                foreach (var (material, ratio) in linearUnit.OutputRatios)
                {
                    LinearMaterialNode linearMaterial = (material as LinearMaterialNode)!;
                    var flow = ratio * capacity;
                    if (materialFlows.ContainsKey(linearMaterial))
                        materialFlows[linearMaterial] += flow;
                    else
                        materialFlows.Add(linearMaterial, flow);
                    outputsElement.AppendChild(createSolutionMaterialElement(linearMaterial, flow, 0.0, "t/y"));
                }
                unitElement.AppendChild(outputsElement);
                solutionUnitsElement.AppendChild(unitElement);
            }

            foreach (var (linearMaterial, flow) in materialFlows)
            {
                solutionMaterialsElement.AppendChild(createSolutionMaterialElement(linearMaterial, flow, -flow * linearMaterial.Price, "t/y"));
            }

            solutionsElement.AppendChild(solutionElement);
            solutionIndex++;
        }

        document.Save(filename);
    }
}

public class LinearNetwork : SimpleNetwork
{
    public Dictionary<OperatingUnitNode, double> UnitCapacities { get; } = new();

    public LinearNetwork(Dictionary<OperatingUnitNode,double> capacities, double objectiveValue) :
        base(new OperatingUnitSet(capacities.Keys), objectiveValue)
    {
        UnitCapacities = capacities;
    }
}

public class SimpleLinearPNSLPModel
{
    //Variable[] _unitSizeVars;
    //Constraint[] _materialConstraints;
    Dictionary<OperatingUnitNode, Variable> _unitSizeVars;
    Dictionary<MaterialNode, Constraint> _materialConstraints;
    Objective _objective;
    Solver _modelSolver;

    bool _solved = false;

    public SimpleLinearPNSLPModel(LinearPNSProblem problem, OperatingUnitSet? baseUnitSet = null)
    {
        _modelSolver = Solver.CreateSolver("SCIP");
        OperatingUnitSet unitsToWorkWith = new OperatingUnitSet(problem.OperatingUnits);
        if (baseUnitSet != null)
        {
            unitsToWorkWith.IntersectWith(baseUnitSet);
        }
        MaterialSet materialsToWorkWith = unitsToWorkWith.Inputs().Union(unitsToWorkWith.Outputs());

        _objective = _modelSolver.Objective();
        _objective.SetMinimization();

        _materialConstraints = new();
        foreach (var material in materialsToWorkWith.Cast<LinearMaterialNode>())
        {
            if (problem.RawMaterials.Contains(material))
            {
                _materialConstraints.Add(material, _modelSolver.MakeConstraint(-material.FlowRateUpperBound, -material.FlowRateLowerBound, "m_" + material.Name));
            }
            else
            {
                _materialConstraints.Add(material, _modelSolver.MakeConstraint(material.FlowRateLowerBound, material.FlowRateUpperBound, "m_" + material.Name));
            }
        }

        _unitSizeVars = new();
        foreach (var unit in unitsToWorkWith.Cast<LinearOperatingUnitNode>())
        {
            var unitVar = _modelSolver.MakeNumVar(unit.CapacityLowerBound, unit.CapacityUpperBound, "x_" + unit.Name);
            _unitSizeVars.Add(unit, unitVar);
            double realUnitCost = unit.ProportionalOperatingCost + unit.ProportionalInvestmentCost / unit.PayoutPeriod;
            foreach (var (material, ratio) in unit.InputRatios)
            {
                _materialConstraints[material].SetCoefficient(unitVar, -ratio);
                realUnitCost += ratio * (material as LinearMaterialNode)!.Price;
            }
            foreach (var (material, ratio) in unit.OutputRatios)
            {
                _materialConstraints[material].SetCoefficient(unitVar, ratio);
                realUnitCost -= ratio * (material as LinearMaterialNode)!.Price;
            }
            _objective.SetCoefficient(unitVar, realUnitCost);
        }
    }

    public bool Solve()
    {
        Solver.ResultStatus resultStatus = _modelSolver.Solve();
        _solved = true;
        return resultStatus == Solver.ResultStatus.OPTIMAL;
    }

    public double ObjectiveValue()
    {
        if ( !_solved )
        {
            Solve();
        }
        return _objective.Value();
    }

    public double GetOptimizedCapacity(OperatingUnitNode unit)
    {
        if (!_solved)
        {
            Solve();
        }
        return _unitSizeVars[unit].SolutionValue();
    }
}
