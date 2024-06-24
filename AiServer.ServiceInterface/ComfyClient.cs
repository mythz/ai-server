using System.Net.Http.Json;
using System.Text;
using ServiceStack;

namespace AiServer.ServiceInterface;

using System.Net.Http;
using System.Text.Json.Nodes;

public class ComfyClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly Dictionary<string, JsonObject> _metadataMapping = new();

    public ComfyClient(string baseUrl = "http://localhost:7860", string apiKey = null)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            DefaultRequestHeaders = { { "ContentType", "application/json" }, { "Accepts", "application/json" } }
        };
        if (!string.IsNullOrEmpty(apiKey))
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }
    
    public async Task<string> PromptGenerationAsync(string apiJson)
    {
        var response = await _httpClient.PostAsync("/prompt", new StringContent(apiJson, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
    
    public string ReplacePlaceholdersInJson(string jsonTemplate, Dictionary<string, string> replacements)
    {
        foreach (var replacement in replacements)
        {
            jsonTemplate = jsonTemplate.Replace($"{{{replacement.Key}}}", replacement.Value);
        }

        return jsonTemplate;
    }
    
    public async Task<string> GetModelsListAsync()
    {
        var response = await _httpClient.GetStringAsync("/engines/list");
        return response;
    }
    
    public async Task<string> DownloadModelAsync(string name)
    {
        var response = await _httpClient.GetStringAsync($"/agent/pull?name={name}");
        return response;
    }

    public async Task<string> GetPromptHistory(string id)
    {
        var response = await _httpClient.GetStringAsync($"/history/{id}");
        return response;
    }
    
    public async Task<Tuple<string,byte[]>> GetPromptResultFile(string id)
    {
        string resultFileName;
        // Poll for results
        while (true)
        {
            var poll = await GetPromptHistory(id);
            var pollDict = JsonNode.Parse(poll);
            // check if pollDict is empty
            if (pollDict.AsObject().Count == 0)
            {
                Thread.Sleep(1000);
                continue;
            }
            if (pollDict[id]["status"]["completed"].GetValue<bool>())
            {
                var result = pollDict[id]["outputs"].AsObject().First().Value["images"].AsArray().First().AsObject()["filename"].ToString();
                resultFileName = result;
                break;
            }
            Thread.Sleep(1000);
        }
        
        // Save the result to a file
        var data = await _httpClient.GetByteArrayAsync($"/view?filename={resultFileName}&type=temp");
        return Tuple.Create(resultFileName, data);
    }

    public async Task<string> DeleteModelAsync(string name)
    {
        var response = await _httpClient.DeleteAsync($"/agent/delete?name={name}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> ConvertWorkflowToApiAsync(string rawWorkflow)
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
            await AddToMappingAsync(classType);

            var apiNode = new JsonObject();
            apiNode["class_type"] = classType;
            apiNode["inputs"] = new JsonObject();
            if (_metadataMapping.TryGetValue(classType, out var currentClass))
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
    private async Task AddToMappingAsync(string classType)
    {
        if (!_metadataMapping.ContainsKey(classType))
        {
            var response = await _httpClient.GetStringAsync($"/object_info/{classType}");
            var respObject = JsonNode.Parse(response).AsObject();
            if (respObject.TryGetPropertyValue(classType, out var value))
            {
                _metadataMapping[classType] = value.AsObject();
            }
        }
    }
}
