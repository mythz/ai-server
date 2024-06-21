using System.Text.Json;
using System.Text.Json.Nodes;
using AiServer.ServiceModel;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.Text;
using JsonObject = System.Text.Json.Nodes.JsonObject;

namespace AiServer.Tests;

public class ComfyUITests
{
    [Test]
    [Ignore("Integration test")]
    public void Can_call_comfyui_with_original_workflow_json()
    {
        // Take the original workflow JSON from "files/workflow_simple_generation.json"
        var rawWorkflow = System.IO.File.ReadAllText("files/workflow_simple_generation.json");
        
        // Convert the workflow JSON to API JSON
        var apiJson = rawWorkflow.ConvertWorkflowToApi();
        
        // Assert that the API JSON is not null
        Assert.That(apiJson, Is.Not.Null);
        
        // Assert that the API JSON is not empty
        Assert.That(apiJson, Is.Not.Empty);
        
        // Call the ComfyUI API with the API JSON
        var response = "http://localhost:7860/prompt".PostJsonToUrl(apiJson);
        
        // Assert that the response is not null
        Assert.That(response, Is.Not.Null);
        
    }

    [Test]
    [Ignore("Integration test")]
    public void Can_convert_original_workflow_json()
    {
        // Take the original workflow JSON from "files/workflow_simple_generation.json"
        var rawWorkflow = System.IO.File.ReadAllText("files/workflow_simple_generation.json");
        
        // Convert the workflow JSON to API JSON
        var apiJson = rawWorkflow.ConvertWorkflowToApi();
        
        File.WriteAllText("files/api_simple_generation.json", apiJson);
        
        // Assert that the API JSON is not null
        Assert.That(apiJson, Is.Not.Null);
        
        // Assert that the API JSON is not empty
        Assert.That(apiJson, Is.Not.Empty);
        
        // Convert to JObject
        var apiData = JsonNode.Parse(apiJson);
        Assert.That(apiData, Is.Not.Null);
        Assert.That(apiData["prompt"], Is.Not.Null);
        Assert.That(apiData["prompt"].AsObject().Count, Is.GreaterThan(0));
        Assert.That(apiData["prompt"].AsObject().Count, Is.EqualTo(7));
        var prompt = apiData["prompt"].AsObject();
        Assert.That(prompt, Is.Not.Null);
        Assert.That(prompt.ContainsKey("10"), Is.True);
        Assert.That(prompt["10"], Is.Not.Null);
        var imageNode = prompt["10"].AsObject();
        Assert.That(imageNode, Is.Not.Null);
        Assert.That(imageNode.ContainsKey("class_type"), Is.True);
        Assert.That(imageNode["class_type"].GetValue<string>(), Is.EqualTo("PreviewImage"));
        Assert.That(imageNode.ContainsKey("inputs"), Is.True);
        var inputs = imageNode["inputs"].AsObject();
        Assert.That(inputs, Is.Not.Null);
        Assert.That(inputs.ContainsKey("images"), Is.True);
        var images = inputs["images"].AsArray();
        Assert.That(images, Is.Not.Null);
        Assert.That(images.Count, Is.EqualTo(2));
        Assert.That(images[0].GetValue<string>(), Is.EqualTo("8"));
        Assert.That(images[1].GetValue<int>(), Is.EqualTo(0));
    }

    [Test]
    [Ignore("Integration test")]
    public void Can_fetch_all_models_from_ComfyUI()
    {
        var response = "http://localhost:7860/engines/list".GetJsonFromUrl();
        
        // Assert that the response is not null
        Assert.That(response, Is.Not.Null);
        /*
         *  [
             {
               "description": "Stability-AI Stable Diffusion v1.6",
               "id": "stable-diffusion-v1-6",
               "name": "Stable Diffusion v1.6",
               "type": "PICTURE"
             },
             {
               "description": "Stability-AI Stable Diffusion XL v1.0",
               "id": "stable-diffusion-xl-1024-v1-0",
               "name": "Stable Diffusion XL v1.0",
               "type": "PICTURE"
             }
           ]
         */
        var models = response.FromJson<List<StableDiffusionEngine>>();
        Assert.That(models, Is.Not.Null);
        Assert.That(models.Count, Is.GreaterThan(0));
    }

    [Test]
    [Ignore("Integration test")]
    public void Can_get_agent_pull_to_download_model()
    {
        var modelName = "zavychromaxl"; // friendly named model
        var response = "http://localhost:7860/engines/list".GetJsonFromUrl();
        
        var models = response.FromJson<List<StableDiffusionEngine>>();
        // Assert.That(models, Is.Not.Null);
        // Assert.That(models.Any(x => x.Name.Contains(modelName)), Is.False);
        
        var downloadRes = $"http://localhost:7860/agent/pull?name={modelName}".GetJsonFromUrl();
        Assert.That(downloadRes, Is.Not.Null);
        Assert.That(downloadRes, Is.Not.Empty);
        
        response = "http://localhost:7860/engines/list".GetJsonFromUrl();
        models = response.FromJson<List<StableDiffusionEngine>>();
        Assert.That(models, Is.Not.Null);
        Assert.That(models.Any(x => x.Name.Contains(modelName)), Is.True);
    }

    [Test]
    [Ignore("Integration test")]
    public void Can_use_StableDiffusionTextToImage_to_template_workflow()
    {
        // Load template
        var template = System.IO.File.ReadAllText("files/workflow_template_txt2img.json");
        
        // Init test DTO
        var testDto = new StableDiffusionTextToImage
        {
            CfgScale = 2,
            Seed = Random.Shared.NextInt64(),
            Height = 1024,
            Width = 1024,
            Engine = "zavychromaxl_v80.safetensors",
            Sampler = "euler_ancestral",
            Samples = 1,
            Steps = 20,
            TextPrompts = new List<TextPrompt>
            {
                new() { Text = "A beautiful sunset over the ocean", Weight = 1 }
            }
        };
        
        var dict = testDto.ToStringDictionary();
        dict["TextPrompts"] = testDto.TextPrompts.Select(x => x.Text).Join(",");
        
        // Convert template to JSON
        var jsonTemplate = ComfyUiExtensions.ReplacePlaceholdersInJson(template,dict);
        
        // Assert that the JSON template is not null
        Assert.That(jsonTemplate, Is.Not.Null);
        // Assert that values are present in the JSON after templating
        Assert.That(jsonTemplate.Contains("A beautiful sunset over the ocean"), Is.True);
        // Parse and check nodes
        var populatedWorkflow = JsonNode.Parse(jsonTemplate);
        Assert.That(populatedWorkflow, Is.Not.Null);
        Assert.That(populatedWorkflow["nodes"], Is.Not.Null);
        Assert.That(populatedWorkflow["nodes"].AsArray().Count, Is.GreaterThan(0));
        Assert.That(populatedWorkflow["nodes"].AsArray().Count, Is.EqualTo(7));
        var nodes = populatedWorkflow["nodes"].AsArray();
        Assert.That(nodes, Is.Not.Null);
        Assert.That(nodes[0].GetValueKind(), Is.EqualTo(JsonValueKind.Object));
        
        var apiJson = jsonTemplate.ConvertWorkflowToApi();
        
        // Request to ComfyUI
        var response = "http://localhost:7860/prompt".PostJsonToUrl(apiJson);
        
        // Poll for results using /history/{id}
        var historyId = JsonNode.Parse(response)["prompt_id"].ToString();
        
        // Assert that the response is not null
        Assert.That(response, Is.Not.Null);
        Assert.That(historyId, Is.Not.Null);

        var resultFileName = "";
        /*
         * "outputs": {
             "10": {
               "images": [
                 {
                   "filename": "ComfyUI_temp_iveeg_00009_.png",
                   "subfolder": "",
                   "type": "temp"
                 }
               ]
             }
           },
         */
        
        // Poll for results
        while (true)
        {
            var poll = "http://localhost:7860/history/{0}".Fmt(historyId).GetJsonFromUrl();
            var pollDict = JsonNode.Parse(poll);
            // check if pollDict is empty
            if (pollDict.AsObject().Count == 0)
            {
                Thread.Sleep(1000);
                continue;
            }
            if (pollDict[historyId]["status"]["completed"].GetValue<bool>())
            {
                var result = pollDict[historyId]["outputs"].AsObject().First().Value["images"].AsArray().First().AsObject()["filename"].ToString();
                Assert.That(result, Is.Not.Null);
                Assert.That(result, Is.Not.Empty);
                resultFileName = result;
                break;
            }
            Thread.Sleep(1000);
        }
        
        // Save the result to a file
        var bytes = "http://localhost:7860/view?filename={0}&type=temp".Fmt(resultFileName).GetBytesFromUrl();
        Assert.That(bytes, Is.Not.Null);
        Assert.That(bytes.Length, Is.GreaterThan(0));
        System.IO.File.WriteAllBytes("files/{0}".Fmt(resultFileName), bytes);
    }
}


/// <summary>
/// Text To Image Request to Match Stability AI API
/// </summary>
public class StableDiffusionTextToImage
{
    public long Seed { get; set; }
    public int CfgScale { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }
    public string Sampler { get; set; }
    public int Samples { get; set; }
    public int Steps { get; set; }
    public string Engine { get; set; }
    public List<TextPrompt> TextPrompts { get; set; }
}

public class TextPrompt
{
    public string Text { get; set; }
    public int Weight { get; set; }
}

/// <summary>
/// Engine DTO
/// </summary>
public class StableDiffusionEngine
{
    public string Description { get; set; }
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
}

public static class ComfyUiExtensions
{
    static string BaseUrl = "http://localhost:7860";
    static readonly Dictionary<string, JsonObject> MetadataMapping = new();
    
    public static string ReplacePlaceholdersInJson(string jsonTemplate, Dictionary<string, string> replacements)
    {
        foreach (var replacement in replacements)
        {
            jsonTemplate = jsonTemplate.Replace($"{{{replacement.Key}}}", replacement.Value);
        }

        return jsonTemplate;
    }

    public static string ConvertWorkflowToApi(this string rawWorkflow)
    {
        var workflow = JsonNode.Parse(rawWorkflow).AsObject();
        var apiNodes = new JsonObject();
        var nodeOutputs = new JsonObject();
        
        // `links` contains an array of links between nodes.
        // Each link is an array with the following elements:
        // 0: link id - Usually just incrementing integers
        // 1: source node id
        // 2: source slot id
        // 3: destination node id
        // 4: destination slot id
        // 5: link type - eg 'MODEL', 'CLIP', etc. The UI uses this to match valid input/output pairs.
        if (!workflow.TryGetPropertyValue("links", out var allLinks))
            throw new Exception("Invalid workflow JSON");

        // Map node outputs
        foreach (var jToken in allLinks.AsArray())
        {
            var link = jToken.AsArray();
            var srcNodeId = link[1].ToString();
            var srcSlot = (int)link[2];
            var destNodeId = link[3].ToString();
            var destSlot = (int)link[4];
            if (!nodeOutputs.ContainsKey(srcNodeId))
                nodeOutputs[srcNodeId] = new JsonObject();
            nodeOutputs[srcNodeId]![srcSlot.ToString()] = new JsonArray { destNodeId, destSlot };
        }
        
        // `nodes` contains an array of nodes. These represent steps along the workflow where processing is done.
        // Each node is an object with the following properties:
        // id: node id - Unique identifier for the node
        // type: node type - The type of node. This is used to determine what processing is done at this step.
        // inputs: array of inputs which can be either links from other node outputs or widget values set on the node
        // widgets_values: array of values set on the node. These are used when the node is not connected to any other nodes.
        // - wigets_values are index sensitive. The order of the values in the array is important.
        // - A node type is defined in the `object_info` endpoint. This endpoint returns metadata about the node type.
        // - A node type metadata contains a `required` object which lists required inputs for the node type.
        // - The order of the keys in the `required` object is the order in which the inputs should be set in the `widgets_values` array.
        // - The inputs that come from other nodes can be populated first since they are not index sensitive.
        // - The `seed` input is a special case. This is due to a few nodes have a seed value where behaviour is on the client side.
        // - The `control_after_generate` input value is in the `widgets_values` array but is not used in the API.
        // - This value always comes after the `seed` value, so we need to skip it when assigning values from the workflow to API format.
        if (!workflow.TryGetPropertyValue("nodes", out var workflowNodes))
            throw new Exception("Invalid workflow JSON");

        // Convert nodes
        foreach (var jToken in workflowNodes.AsArray())
        {
            var node = jToken.AsObject();
            if (node == null)
                throw new Exception("Invalid workflow JSON");
            if (!node.TryGetPropertyValue("id", out var idToken))
                throw new Exception("Invalid workflow JSON");
            var nodeId = idToken.ToString();
            var classType = node["type"].ToString();
            AddToMapping(classType);

            var apiNode = new JsonObject();
            apiNode["class_type"] = classType;
            apiNode["inputs"] = new JsonObject();
            if (MetadataMapping.TryGetValue(classType, out var currentClass))
            {
                var requiredMetadata = currentClass["input"]!["required"]!.AsObject();
                var widgetIndex = 0;
                foreach (var prop in requiredMetadata)
                {
                    var propName = prop.Key;
                    if (node.ContainsKey("inputs") && node["inputs"]!.AsArray().Any(x => x["name"]!.ToString() == propName))
                    {
                        var inputNode = node["inputs"]!.AsArray().FirstOrDefault(x => x["name"]!.ToString() == propName);
                        var srcNodeId = inputNode!["link"]!.ToString();
                        var linkVals = allLinks.AsArray().FirstOrDefault(x => (int)x[0] == int.Parse(srcNodeId));
                        Console.WriteLine(linkVals.ToString());
                        apiNode["inputs"][propName] = new JsonArray() { linkVals![1].GetValue<int>().ToString(), linkVals[2].GetValue<int>() };
                    }
                    else
                    {
                        if (!node.ContainsKey("widgets_values"))
                            continue;
                        var widgetVals = node["widgets_values"]!.AsArray();
                        if (widgetIndex < widgetVals.Count)
                        {
                            apiNode["inputs"][propName] = widgetVals[widgetIndex].DeepClone();
                            widgetIndex++;
                        }
                        if (propName == "seed")
                            widgetIndex++;
                    }
                }
            }
            apiNodes[nodeId] = apiNode;
        }

        return new JsonObject() { ["prompt"] = apiNodes }.ToJsonString();
    }

    static void AddToMapping(string classType)
    {
        if (!MetadataMapping.ContainsKey(classType))
        {
            var response = BaseUrl.CombineWith($"/object_info/{classType}").GetJsonFromUrl();
            var respObject = JsonNode.Parse(response).AsObject();
            if (respObject.TryGetPropertyValue(classType, out var value))
            {
                MetadataMapping[classType] = value.AsObject();
            }
        }
    }
}