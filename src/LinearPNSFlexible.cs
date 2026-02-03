using System.Xml;
using Google.OrTools.LinearSolver;

namespace PGraphInCS.LinearPNS.Flexible;

/// <summary>
/// Class to store additional material data: flow rate limit and unit price. Used for the linear model employed by P-Graph Studio.
/// </summary>
public class LinearMaterialData
{
    public double FlowRateLowerBound { get; set; } = 0;
    public double FlowRateUpperBound { get; set; } = 10000000;
    public double Price { get; set; } = 0;
}

/// <summary>
/// Class to store additional operating unit data: capacity limits and cost functions. Used for the linear model employed by P-Graph Studio.
/// </summary>
public class LinearOperatingUnitData
{
    public double CapacityLowerBound { get; set; } = 0;
    public double CapacityUpperBound { get; set; } = 10000000;
    public double FixOperatingCost { get; set; } = 0;
    public double ProportionalOperatingCost { get; set; } = 0;
    public double FixInvestmentCost { get; set; } = 0;
    public double ProportionalInvestmentCost { get; set; } = 0;
    public double PayoutPeriod { get; set; } = 10;
}

/// <summary>
/// Special PNS problem class for the compatibility with the linear model of P-Graph Studio.
/// </summary>
public class LinearPNSProblem : SimplePNSProblem
{
    public Dictionary<MaterialNode, LinearMaterialData> MaterialData { get; } = new();
    public Dictionary<OperatingUnitNode, LinearOperatingUnitData> OperatingUnitData { get; } = new();
    public Dictionary<OperatingUnitNode, Dictionary<MaterialNode, double>> InputRatios { get; } = new();
    public Dictionary<OperatingUnitNode, Dictionary<MaterialNode, double>> OutputRatios { get; } = new();

    public override void FinalizeData()
    {
        base.FinalizeData();
        foreach (MaterialNode material in Materials)
        {
            if (!MaterialData.ContainsKey(material))
            {
                MaterialData.Add(material, new LinearMaterialData());
            }
        }
        foreach (OperatingUnitNode unit in OperatingUnits)
        {
            if (!OperatingUnitData.ContainsKey(unit))
            {
                OperatingUnitData.Add(unit, new LinearOperatingUnitData());
            }
            if (!InputRatios.ContainsKey(unit))
            {
                InputRatios.Add(unit, new Dictionary<MaterialNode, double>());
            }
            if (!OutputRatios.ContainsKey(unit))
            {
                OutputRatios.Add(unit, new Dictionary<MaterialNode, double>());
            }
        }
    }

    /// <summary>
    /// Load the problem from P-Graph Studio file (.pgsx)
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
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

        Dictionary<string, MaterialNode> materialIdToNode = new();
        Dictionary<string, OperatingUnitNode> unitIdToNode = new();
        Dictionary<string, MaterialNode> materialNameToNode = new();
        Dictionary<string, OperatingUnitNode> unitNameToNode = new();

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

            MaterialNode material = new(name);
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

            graph.MaterialData.Add(material, new LinearMaterialData
            {
                FlowRateLowerBound = minFlow,
                FlowRateUpperBound = maxFlow,
                Price = price
            });
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

            OperatingUnitNode unit = new(name);
            units.Add(unit);

            if (id != "") { unitIdToNode.Add(id, unit); }
            if (name != "") { unitNameToNode.Add(name, unit); }

            graph.AddOperatingUnit(unit);

            graph.OperatingUnitData.Add(unit, new LinearOperatingUnitData
            {
                CapacityLowerBound = minCap,
                CapacityUpperBound = maxCap,
                FixOperatingCost = opCostFix,
                ProportionalOperatingCost = opCostProp,
                FixInvestmentCost = invCostFix,
                ProportionalInvestmentCost = invCostProp,
                PayoutPeriod = payoutPeriod
            });
            graph.InputRatios.Add(unit, new Dictionary<MaterialNode, double>());
            graph.OutputRatios.Add(unit, new Dictionary<MaterialNode, double>());
        }

        foreach (XmlNode edgeNode in edgesNode.ChildNodes)
        {
            string beginId = edgeNode.Attributes?["BeginID"]?.Value ?? "";
            string endId = edgeNode.Attributes?["EndID"]?.Value ?? "";
            double flowRate = double.Parse(getAttributeValue(edgeNode, "Rate", "Edge", "FlowRate"));
            if (unitIdToNode.ContainsKey(beginId))
            {
                unitIdToNode[beginId].AddOutput(materialIdToNode[endId]);
                graph.OutputRatios[unitIdToNode[beginId]].Add(materialIdToNode[endId], flowRate);
            }
            else
            {
                unitIdToNode[endId].AddInput(materialIdToNode[beginId]);
                graph.InputRatios[unitIdToNode[endId]].Add(materialIdToNode[beginId], flowRate);
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

    /// <summary>
    /// Loads the problem from P-Graph Studio's input file used by its underlying solver (.in)
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static LinearPNSProblem FromPgraphSolverInputFile(string filename)
    {
        LinearPNSProblem graph = new LinearPNSProblem();
        using (StreamReader reader = new StreamReader(filename))
        {
            InFileReadState readState = InFileReadState.Nothing;
            Dictionary<string, string> defaultValues = new();

            Dictionary<string, MaterialNode> materialNameToNode = new();
            Dictionary<string, OperatingUnitNode> unitNameToNode = new();

            MaterialSet rawMaterials = new MaterialSet();
            MaterialSet products = new MaterialSet();
            MaterialSet intermediates = new MaterialSet();
            MaterialSet allMaterials = new MaterialSet();
            OperatingUnitSet units = new OperatingUnitSet();

            Func<Dictionary<string, string>, string, string, string> getValueOrDefault = (values, key, defaultKey) =>
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
                                materialProperties.Add("type", part);
                            }
                        }
                    }

                    string materialType = getValueOrDefault(materialProperties, "type", "material_type");
                    double minFlow = double.Parse(getValueOrDefault(materialProperties, "flow_rate_lower_bound", "material_flow_rate_lower_bound"));
                    double maxFlow = double.Parse(getValueOrDefault(materialProperties, "flow_rate_upper_bound", "material_flow_rate_upper_bound"));
                    double price = double.Parse(getValueOrDefault(materialProperties, "price", "material_price"));

                    MaterialNode material = new(materialName);
                    allMaterials.Add(material);
                    if (materialType == "raw_material")
                        rawMaterials.Add(material);
                    else if (materialType == "intermediate")
                        intermediates.Add(material);
                    else if (materialType == "product")
                        products.Add(material);

                    if (materialName != "") { materialNameToNode.Add(materialName, material); }

                    graph.AddMaterial(material);


                    graph.MaterialData.Add(material, new LinearMaterialData
                    {
                        FlowRateLowerBound = minFlow,
                        FlowRateUpperBound = maxFlow,
                        Price = price
                    });
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

                    OperatingUnitNode unit = new(unitName);
                    units.Add(unit);

                    if (unitName != "") { unitNameToNode.Add(unitName, unit); }

                    graph.AddOperatingUnit(unit);

                    graph.OperatingUnitData.Add(unit, new LinearOperatingUnitData
                    {
                        CapacityLowerBound = minCap,
                        CapacityUpperBound = maxCap,
                        FixOperatingCost = fixCost,
                        ProportionalOperatingCost = propCost
                    });
                    graph.InputRatios.Add(unit, new Dictionary<MaterialNode, double>());
                    graph.OutputRatios.Add(unit, new Dictionary<MaterialNode, double>());
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
                                unitNameToNode[unitName].AddInput(materialNameToNode[parts3[0]]);
                                graph.InputRatios[unitNameToNode[unitName]].Add(materialNameToNode[parts3[0]], 1.0);
                            }
                            else
                            {
                                unitNameToNode[unitName].AddInput(materialNameToNode[parts3[1]]);
                                graph.InputRatios[unitNameToNode[unitName]].Add(materialNameToNode[parts3[1]], double.Parse(parts3[0]));
                            }
                        }
                        foreach (string output in parts2[1].Split(" + "))
                        {
                            string[] parts3 = output.Split(" ");
                            if (parts3.Length == 1)
                            {
                                unitNameToNode[unitName].AddOutput(materialNameToNode[parts3[0]]);
                                graph.OutputRatios[unitNameToNode[unitName]].Add(materialNameToNode[parts3[0]], 1.0);
                            }
                            else
                            {
                                unitNameToNode[unitName].AddOutput(materialNameToNode[parts3[1]]);
                                graph.OutputRatios[unitNameToNode[unitName]].Add(materialNameToNode[parts3[1]], double.Parse(parts3[0]));
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

    /// <summary>
    /// Exports problem to P-Graph Studio file (.pgsx). Can also export solutions (the solutions should be from the same problem).
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="solutions"></param>
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
        foreach (MaterialNode material in Materials)
        {
            idmax = Math.Max(idmax, material.Id);

            string materialTypeCode = "1";
            if (RawMaterials.Contains(material)) materialTypeCode = "0";
            else if (Products.Contains(material)) materialTypeCode = "2";
            XmlElement materialElement = document.CreateElement("Material");
            materialElement.SetAttribute("ID", material.Id.ToString());
            materialElement.SetAttribute("Name", material.Name.Replace(" ", "_"));
            materialElement.SetAttribute("Type", materialTypeCode);

            XmlElement parameterListElement = document.CreateElement("ParameterList");
            parameterListElement.AppendChild(createParameterElement("price", "Price: ", getValueOrDefault(MaterialData[material].Price.ToString(), "Material", "Price")));
            parameterListElement.AppendChild(createParameterElement("reqflow", "Required flow: ", getValueOrDefault(MaterialData[material].FlowRateLowerBound.ToString(), "Material", "FlowRateLowerBound")));
            parameterListElement.AppendChild(createParameterElement("maxflow", "Maximum flow: ", getValueOrDefault(MaterialData[material].FlowRateUpperBound.ToString(), "Material", "FlowRateUpperBound")));
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
        foreach (OperatingUnitNode unit in OperatingUnits)
        {
            idmax = Math.Max(idmax, unit.Id);

            XmlElement unitElement = document.CreateElement("OperatingUnit");
            unitElement.SetAttribute("ID", unit.Id.ToString());
            unitElement.SetAttribute("Name", unit.Name.Replace(" ", "_"));
            unitElement.SetAttribute("Title", "");

            XmlElement parameterListElement = document.CreateElement("ParameterList");
            parameterListElement.AppendChild(createParameterElement("caplower", "Capacity, lower bound: ", getValueOrDefault(OperatingUnitData[unit].CapacityLowerBound.ToString(), "OperatingUnit", "CapacityLowerBound")));
            parameterListElement.AppendChild(createParameterElement("capupper", "Capacity, upper bound: ", getValueOrDefault(OperatingUnitData[unit].CapacityUpperBound.ToString(), "OperatingUnit", "CapacityUpperBound")));
            parameterListElement.AppendChild(createParameterElement("investcostfix", "Investment cost, fix: ", getValueOrDefault(OperatingUnitData[unit].FixInvestmentCost.ToString(), "OperatingUnit", "InvestmentFixCost")));
            parameterListElement.AppendChild(createParameterElement("investcostprop", "Investment cost, proportional: ", getValueOrDefault(OperatingUnitData[unit].ProportionalInvestmentCost.ToString(), "OperatingUnit", "InvestmentPropCost")));
            parameterListElement.AppendChild(createParameterElement("opercostfix", "Operating cost, fix: ", getValueOrDefault(OperatingUnitData[unit].FixOperatingCost.ToString(), "OperatingUnit", "OperatingFixCost")));
            parameterListElement.AppendChild(createParameterElement("opercostprop", "Operating cost, proportional: ", getValueOrDefault(OperatingUnitData[unit].ProportionalOperatingCost.ToString(), "OperatingUnit", "OperatingPropCost")));
            parameterListElement.AppendChild(createParameterElement("workinghour", "Working hours per year: ", "-1"));
            parameterListElement.AppendChild(createParameterElement("payoutperiod", "Payout Period: ", getValueOrDefault(OperatingUnitData[unit].PayoutPeriod.ToString(), "OperatingUnit", "PayoutPeriod")));
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
        foreach (OperatingUnitNode unit in OperatingUnits)
        {
            foreach (var (material, ratio) in InputRatios[unit])
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

            foreach (var (material, ratio) in OutputRatios[unit])
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
            Func<MaterialNode, double, double, string, XmlElement> createSolutionMaterialElement = (linearMaterial, flow, cost, MU) =>
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

                Dictionary<MaterialNode, double> materialFlows = new();

                foreach (var (unit, capacity) in network.UnitCapacities)
                {
                    XmlElement unitElement = document.CreateElement("OperatingUnit");
                    unitElement.SetAttribute("Name", unit.Name.Replace(" ", "_"));
                    unitElement.SetAttribute("Size", capacity.ToString());
                    unitElement.SetAttribute("Cost", ((OperatingUnitData[unit].FixInvestmentCost + OperatingUnitData[unit].ProportionalInvestmentCost * capacity) / OperatingUnitData[unit].PayoutPeriod + OperatingUnitData[unit].FixOperatingCost + OperatingUnitData[unit].ProportionalOperatingCost * capacity).ToString());
                    unitElement.SetAttribute("MU", "t/y");
                    XmlElement inputsElement = document.CreateElement("Input");
                    foreach (var (material, ratio) in InputRatios[unit])
                    {
                        var flow = ratio * capacity;
                        if (materialFlows.ContainsKey(material))
                            materialFlows[material] -= flow;
                        else
                            materialFlows.Add(material, -flow);
                        inputsElement.AppendChild(createSolutionMaterialElement(material, flow, 0.0, "t/y"));
                    }
                    unitElement.AppendChild(inputsElement);
                    XmlElement outputsElement = document.CreateElement("Output");
                    foreach (var (material, ratio) in OutputRatios[unit])
                    {
                        var flow = ratio * capacity;
                        if (materialFlows.ContainsKey(material))
                            materialFlows[material] += flow;
                        else
                            materialFlows.Add(material, flow);
                        outputsElement.AppendChild(createSolutionMaterialElement(material, flow, 0.0, "t/y"));
                    }
                    unitElement.AppendChild(outputsElement);
                    solutionUnitsElement.AppendChild(unitElement);
                }

                foreach (var (material, flow) in materialFlows)
                {
                    solutionMaterialsElement.AppendChild(createSolutionMaterialElement(material, flow, -flow * MaterialData[material].Price, "t/y"));
                }

                solutionsElement.AppendChild(solutionElement);
                solutionIndex++;
            }
        }


        // write to file
        document.Save(filename);
    }

    /// <summary>
    /// Exports problem to P-Graph Studio's input file used by its underlying solver (.in)
    /// </summary>
    /// <param name="filename"></param>
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
            foreach (MaterialNode material in Materials)
            {
                string line = material.Name.Replace(" ", "_") + ": ";
                if (RawMaterials.Contains(material)) line += "raw_material";
                else if (Products.Contains(material)) line += "product";
                else line += "intermediate";
                if (MaterialData[material].FlowRateLowerBound != 0)
                {
                    line += ", ";
                    line += "flow_rate_lower_bound=" + MaterialData[material].FlowRateLowerBound;
                }
                if (MaterialData[material].FlowRateUpperBound != 10000000)
                {
                    line += ", ";
                    line += "flow_rate_upper_bound=" + MaterialData[material].FlowRateUpperBound;
                }
                if (MaterialData[material].Price != 0)
                {
                    line += ", ";
                    line += "price=" + MaterialData[material].Price;
                }
                writer.WriteLine(line);
            }
            writer.WriteLine();
            writer.WriteLine("operating_units:");
            foreach (OperatingUnitNode unit in OperatingUnits)
            {
                string line = unit.Name.Replace(" ", "_");
                bool first = true;
                if (OperatingUnitData[unit].CapacityLowerBound != 0)
                {
                    if (first) line += ": ";
                    else line += ", ";
                    first = false;
                    line += "capacity_lower_bound=" + OperatingUnitData[unit].CapacityLowerBound;
                }
                if (OperatingUnitData[unit].CapacityUpperBound != 10000000)
                {
                    if (first) line += ": ";
                    else line += ", ";
                    first = false;
                    line += "capacity_upper_bound=" + OperatingUnitData[unit].CapacityUpperBound;
                }
                if (OperatingUnitData[unit].FixOperatingCost != 0 || OperatingUnitData[unit].FixInvestmentCost != 0)
                {
                    if (first) line += ": ";
                    else line += ", ";
                    first = false;
                    line += "fix_cost=" + (OperatingUnitData[unit].FixOperatingCost + OperatingUnitData[unit].FixInvestmentCost / OperatingUnitData[unit].PayoutPeriod);
                }
                if (OperatingUnitData[unit].ProportionalOperatingCost != 0 || OperatingUnitData[unit].ProportionalInvestmentCost != 0)
                {
                    if (first) line += ": ";
                    else line += ", ";
                    first = false;
                    line += "proportional_cost=" + (OperatingUnitData[unit].ProportionalOperatingCost + OperatingUnitData[unit].ProportionalInvestmentCost / OperatingUnitData[unit].PayoutPeriod);
                }
                writer.WriteLine(line);
            }
            writer.WriteLine();
            writer.WriteLine("material_to_operating_unit_flow_rates:");
            foreach (OperatingUnitNode unit in OperatingUnits)
            {
                string line = unit.Name.Replace(" ", "_");
                line += ":";
                bool first = true;
                foreach (var (material, ratio) in InputRatios[unit])
                {
                    if (!first) line += " +";
                    first = false;
                    if (ratio != 1) line += " " + ratio;
                    line += " " + material.Name.Replace(" ", "_");
                }
                line += " =>";
                first = true;
                foreach (var (material, ratio) in OutputRatios[unit])
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

    /// <summary>
    /// Add solutions to a P-Graph Studio file (.pgsx). Solutions should only be added the a file containing the same problem.
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="solutions"></param>
    /// <exception cref="ArgumentException"></exception>
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

        Func<MaterialNode, double, double, string, XmlElement> createSolutionMaterialElement = (linearMaterial, flow, cost, MU) =>
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

            Dictionary<MaterialNode, double> materialFlows = new();

            foreach (var (unit, capacity) in network.UnitCapacities)
            {
                XmlElement unitElement = document.CreateElement("OperatingUnit");
                unitElement.SetAttribute("Name", unit.Name.Replace(" ", "_"));
                unitElement.SetAttribute("Size", capacity.ToString());
                unitElement.SetAttribute("Cost", ((OperatingUnitData[unit].FixInvestmentCost + OperatingUnitData[unit].ProportionalInvestmentCost * capacity) / OperatingUnitData[unit].PayoutPeriod + OperatingUnitData[unit].FixOperatingCost + OperatingUnitData[unit].ProportionalOperatingCost * capacity).ToString());
                unitElement.SetAttribute("MU", "t/y");
                XmlElement inputsElement = document.CreateElement("Input");
                foreach (var (material, ratio) in InputRatios[unit])
                {
                    var flow = ratio * capacity;
                    if (materialFlows.ContainsKey(material))
                        materialFlows[material] -= flow;
                    else
                        materialFlows.Add(material, -flow);
                    inputsElement.AppendChild(createSolutionMaterialElement(material, flow, 0.0, "t/y"));
                }
                unitElement.AppendChild(inputsElement);
                XmlElement outputsElement = document.CreateElement("Output");
                foreach (var (material, ratio) in OutputRatios[unit])
                {
                    var flow = ratio * capacity;
                    if (materialFlows.ContainsKey(material))
                        materialFlows[material] += flow;
                    else
                        materialFlows.Add(material, flow);
                    outputsElement.AppendChild(createSolutionMaterialElement(material, flow, 0.0, "t/y"));
                }
                unitElement.AppendChild(outputsElement);
                solutionUnitsElement.AppendChild(unitElement);
            }

            foreach (var (material, flow) in materialFlows)
            {
                solutionMaterialsElement.AppendChild(createSolutionMaterialElement(material, flow, -flow * MaterialData[material].Price, "t/y"));
            }

            solutionsElement.AppendChild(solutionElement);
            solutionIndex++;
        }

        document.Save(filename);
    }
}

/// <summary>
/// Special network class to represent solutions of the linear P-graph (for consistency with P-Graph Studio)
/// </summary>
public class LinearNetwork : SimpleNetwork
{
    public Dictionary<OperatingUnitNode, double> UnitCapacities { get; } = new();

    public LinearNetwork(Dictionary<OperatingUnitNode, double> capacities, double objectiveValue) :
        base(new OperatingUnitSet(capacities.Keys), objectiveValue)
    {
        UnitCapacities = capacities;
    }
}

/// <summary>
/// Linear programming model for the bounding of subproblems. Implements the model used by P-Graph Studio.
/// </summary>
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
        foreach (var material in materialsToWorkWith)
        {
            if (problem.RawMaterials.Contains(material))
            {
                _materialConstraints.Add(material, _modelSolver.MakeConstraint(-problem.MaterialData[material].FlowRateUpperBound, -problem.MaterialData[material].FlowRateLowerBound, "m_" + material.Name));
            }
            else
            {
                _materialConstraints.Add(material, _modelSolver.MakeConstraint(problem.MaterialData[material].FlowRateLowerBound, problem.MaterialData[material].FlowRateUpperBound, "m_" + material.Name));
            }
        }

        _unitSizeVars = new();
        foreach (var unit in unitsToWorkWith)
        {
            var unitVar = _modelSolver.MakeNumVar(problem.OperatingUnitData[unit].CapacityLowerBound, problem.OperatingUnitData[unit].CapacityUpperBound, "x_" + unit.Name);
            _unitSizeVars.Add(unit, unitVar);
            double realUnitCost = problem.OperatingUnitData[unit].ProportionalOperatingCost + problem.OperatingUnitData[unit].ProportionalInvestmentCost / problem.OperatingUnitData[unit].PayoutPeriod;
            foreach (var (material, ratio) in problem.InputRatios[unit])
            {
                _materialConstraints[material].SetCoefficient(unitVar, -ratio);
                realUnitCost += ratio * problem.MaterialData[material].Price;
            }
            foreach (var (material, ratio) in problem.OutputRatios[unit])
            {
                _materialConstraints[material].SetCoefficient(unitVar, ratio);
                realUnitCost -= ratio * problem.MaterialData[material].Price;
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
        if (!_solved)
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
