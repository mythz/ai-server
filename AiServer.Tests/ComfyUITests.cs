using AiServer.ServiceModel;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ServiceStack;

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
        var response = "http://localhost:7861/prompt".PostJsonToUrl(apiJson);
        
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
        
        // Assert that the API JSON is not null
        Assert.That(apiJson, Is.Not.Null);
        
        // Assert that the API JSON is not empty
        Assert.That(apiJson, Is.Not.Empty);
        
        // Convert to JObject
        var apiData = JObject.Parse(apiJson);
        Assert.That(apiData, Is.Not.Null);
        Assert.That(apiData.ContainsKey("prompt"), Is.True);
        Assert.That(apiData["prompt"], Is.Not.Null);
        Assert.That(apiData["prompt"].Children().Count(), Is.GreaterThan(0));
        Assert.That(apiData["prompt"].Children().Count(), Is.EqualTo(7));
        var prompt = apiData["prompt"] as JObject;
        Assert.That(prompt, Is.Not.Null);
        Assert.That(prompt.ContainsKey("10"), Is.True);
        Assert.That(prompt["10"], Is.Not.Null);
        var imageNode = prompt["10"] as JObject;
        Assert.That(imageNode, Is.Not.Null);
        Assert.That(imageNode.ContainsKey("class_type"), Is.True);
        Assert.That(imageNode["class_type"].Value<string>(), Is.EqualTo("PreviewImage"));
        Assert.That(imageNode.ContainsKey("inputs"), Is.True);
        var inputs = imageNode["inputs"] as JObject;
        Assert.That(inputs, Is.Not.Null);
        Assert.That(inputs.ContainsKey("images"), Is.True);
        var images = inputs["images"] as JArray;
        Assert.That(images, Is.Not.Null);
        Assert.That(images.Count, Is.EqualTo(2));
        Assert.That(images[0].Value<string>(), Is.EqualTo("8"));
        Assert.That(images[1].Value<int>(), Is.EqualTo(0));
    }
}

public static class ComfyUiExtensions
{
    const string BaseUrl = "http://localhost:7861";
    static readonly JObject MetadataMapping = new();


    public static string ConvertWorkflowToApi(this string rawWorkflow)
    {
        var workflow = JObject.Parse(rawWorkflow);

        var apiNodes = new JObject();
        var nodeOutputs = new JObject();
        
        // `links` contains an array of links between nodes.
        // Each link is an array with the following elements:
        // 0: link id - Usually just incrementing integers
        // 1: source node id
        // 2: source slot id
        // 3: destination node id
        // 4: destination slot id
        // 5: link type - eg 'MODEL', 'CLIP', etc. The UI uses this to match valid input/output pairs.
        if(!workflow.TryGetValue("links", out var allLinks))
            throw new Exception("Invalid workflow JSON");

        // Map node outputs
        foreach (var jToken in (JArray)allLinks)
        {
            var link = (JArray)jToken;
            var srcNodeId = link[1].ToString();
            var srcSlot = (int)link[2];
            var destNodeId = link[3].ToString();
            var destSlot = (int)link[4];

            if (!nodeOutputs.ContainsKey(srcNodeId))
                nodeOutputs[srcNodeId] = new JObject();

            nodeOutputs[srcNodeId]![srcSlot.ToString()] = new JArray { destNodeId, destSlot };
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
        if(!workflow.TryGetValue("nodes", out var workflowNodes))
            throw new Exception("Invalid workflow JSON");

        // Convert nodes
        foreach (var jToken in (JArray)workflowNodes)
        {
            var node = (JObject)jToken;
            if(node == null)
                throw new Exception("Invalid workflow JSON");
            if(!node.TryGetValue("id", out var idToken))
                throw new Exception("Invalid workflow JSON");
            
            var nodeId = idToken.ToString();
            var classType = node["type"].ToString();

            AddToMapping(classType);

            var apiNode = new JObject();
            apiNode["class_type"] = classType;
            apiNode["inputs"] = new JObject();

            if (MetadataMapping.TryGetValue(classType, out var currentClass))
            {
                var requiredMetadata = (JObject)currentClass["input"]["required"];
                var widgetIndex = 0;

                foreach (var prop in requiredMetadata)
                {
                    var propName = prop.Key;

                    if (node.ContainsKey("inputs") && ((JArray)node["inputs"]).Any(x => (string)x["name"] == propName))
                    {
                        var inputNode = ((JArray)node["inputs"]).FirstOrDefault(x => (string)x["name"] == propName);
                        var srcNodeId = inputNode["link"].ToString();
                        var linkVals =
                            ((JArray)allLinks).FirstOrDefault(x => (int)x[0] == int.Parse(srcNodeId));
                        Console.WriteLine(linkVals.ToString());
                        apiNode["inputs"][propName] = new JArray() { linkVals[1].ToString(), linkVals[2] };
                    }
                    else
                    {
                        if (!node.ContainsKey("widgets_values"))
                            continue;

                        var widgetVals = (JArray)node["widgets_values"];
                        if (widgetIndex < widgetVals.Count)
                        {
                            apiNode["inputs"][propName] = widgetVals[widgetIndex];
                            widgetIndex++;
                        }

                        if (propName == "seed")
                            widgetIndex++;
                    }
                }
            }

            apiNodes[nodeId] = apiNode;
        }

        return new JObject() { ["prompt"] = apiNodes }.ToString();
    }

    static void AddToMapping(string classType)
    {
        if (!MetadataMapping.ContainsKey(classType))
        {
            var response = BaseUrl.CombineWith($"/object_info/{classType}").GetJsonFromUrl();
            var respObject = JObject.Parse(response);

            if (respObject.TryGetValue(classType, out var value))
            {
                MetadataMapping[classType] = (JObject)value;
            }
        }
    }
}