using System.Net.Http.Json;
using System.Runtime.Serialization;
using System.Text;
using ServiceStack;
using ServiceStack.Script;
using ServiceStack.Text;

namespace AiServer.ServiceInterface.Comfy;

using System.Net.Http;
using System.Text.Json.Nodes;

public class ComfyClient(HttpClient httpClient)
{
    private readonly Dictionary<string, JsonObject> metadataMapping = new();
    private static ScriptContext context = new ScriptContext().Init();

    public string WorkflowTemplatePath { get; set; } = "workflows";
    public string TextToImageTemplate { get; set; } = "text_to_image.json";
    public string ImageToTextTemplate { get; set; } = "image_to_text.json";
    public string ImageToImageTemplate { get; set; } = "image_to_image.json";
    public string ImageToImageUpscaleTemplate { get; set; } = "image_to_image_upscale.json";
    public string ImageToImageWithMaskTemplate { get; set; } = "image_to_image_with_mask.json";
    public string TextToAudioTemplate { get; set; } = "text_to_audio.json";
    public string AudioToTextTemplate { get; set; } = "audio_to_text.json";
    
    public int PollIntervalMs { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 60000;

    public ComfyClient(string baseUrl,string apiKey = null)
        : this(string.IsNullOrEmpty(apiKey) ? new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            DefaultRequestHeaders = { { "ContentType", "application/json" }, { "Accepts", "application/json" } }
        } : new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            DefaultRequestHeaders = { { "ContentType", "application/json" }, { "Accepts", "application/json" }, {
                "Authorization", $"Bearer {apiKey}"}}})
    {
    }

    public async Task<string> QueueWorkflowAsync(string apiJson)
    {
        var response = await httpClient.PostAsync("/prompt", new StringContent(apiJson, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
    
    public async Task<ComfyImageInput> UploadImageAssetAsync(Stream fileStream, string filename)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "image", filename);
        var response = await httpClient.PostAsync("/upload/image?overwrite=true&type=temp", content);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        await Task.Delay(1000);
        return result.FromJson<ComfyImageInput>();
    }
    
    public async Task<ComfyImageInput> UploadAudioAssetAsync(Stream fileStream, string filename)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "audio", filename);
        // Still uses /upload/image endpoint at the time of development, expecting this will change
        var response = await httpClient.PostAsync("/upload/image?overwrite=true&type=temp", content);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        return result.FromJson<ComfyImageInput>();
    }
    
    public async Task<string> PopulateImageToTextWorkflowAsync(ComfyImageToText request)
    {
        return await PopulateWorkflow(request, ImageToTextTemplate);
    }
    
    public async Task<string> PopulateImageToImageWithMaskWorkflowAsync(ComfyImageToImageWithMask request)
    {
        return await PopulateWorkflow(request, ImageToImageWithMaskTemplate);
    }
    
    public async Task<string> PopulateTextToImageWorkflowAsync(ComfyTextToImage request)
    {
        return await PopulateWorkflow(request, TextToImageTemplate);
    }
    
    public async Task<string> PopulateImageToImageWorkflowAsync(ComfyImageToImage request)
    {
        return await PopulateWorkflow(request, ImageToImageTemplate);
    }
    
    public async Task<string> PopulateImageToImageUpscaleWorkflowAsync(ComfyImageToImageUpscale request)
    {
        return await PopulateWorkflow(request, ImageToImageUpscaleTemplate);
    }
    
    public async Task<string> PopulateWorkflow<T>(T request, string templatePath)
    {
        // Read template from file for Text to Image
        var template = await File.ReadAllTextAsync(Path.Combine(WorkflowTemplatePath, templatePath));
        // Populate template with request
        var workflowPageResult = new PageResult(context.OneTimePage(template))
        {
            Args = request.ToObjectDictionary(),
        };

        // Render template to JSON
        return await workflowPageResult.RenderToStringAsync();
    }
    
    public async Task<ComfyWorkflowResponse> GenerateImageToTextAsync(StableDiffusionImageToText request)
    {
        var comfyRequest = request.ToComfy();
        if (comfyRequest.Image == null && request.InitImage != null)
        {
            var tempFileName = $"image2text_{Guid.NewGuid()}.png";
            comfyRequest.Image = await UploadImageAssetAsync(request.InitImage, tempFileName);
        }

        if (comfyRequest.Image == null)
            throw new Exception("Image input is required for Image to Text");
        
        // Read template from file for Image to Text
        var workflowJson = await PopulateImageToTextWorkflowAsync(comfyRequest);
        // Convert to ComfyUI API JSON format
        var apiJson = await ConvertWorkflowToApiAsync(workflowJson);
        // Call ComfyUI API
        var response = await QueueWorkflowAsync(apiJson);
        // Returns with job ID
        using var jsConfig = JsConfig.With(new Config { TextCase = TextCase.SnakeCase });
        return response.FromJson<ComfyWorkflowResponse>();
    }

    public async Task<ComfyWorkflowResponse> GenerateImageToImageWithMaskAsync(
        StableDiffusionImageToImageWithMask request)
    {
        var comfyRequest = request.ToComfy();
        if (comfyRequest.InitImage == null)
            throw new Exception("Image input is required for Image to Image with Mask");
        if (comfyRequest.InitMask == null)
            throw new Exception("Mask image input is required for Image to Image with Mask");
        
        if (comfyRequest.Image == null && request.InitImage != null)
        {
            var tempFileName = $"image2image_mask_{Guid.NewGuid()}.png";
            comfyRequest.Image = await UploadImageAssetAsync(request.InitImage, tempFileName);
        }
        
        if (comfyRequest.MaskImage == null && request.MaskImage != null)
        {
            var tempFileName = $"image2image_mask_{Guid.NewGuid()}.png";
            comfyRequest.MaskImage = await UploadImageAssetAsync(request.MaskImage, tempFileName);
        }
        
        if (comfyRequest.Image == null)
            throw new Exception("Image input failed to upload for Image to Image with Mask");
        
        if (comfyRequest.MaskImage == null)
            throw new Exception("Mask input failed to upload for Image to Image with Mask");
        
        // Read template from file for Image to Image with Mask
        var workflowJson = await PopulateImageToImageWithMaskWorkflowAsync(comfyRequest);
        // Convert to ComfyUI API JSON format
        var apiJson = await ConvertWorkflowToApiAsync(workflowJson);
        // Call ComfyUI API
        var response = await QueueWorkflowAsync(apiJson);
        // Returns with job ID
        using var jsConfig = JsConfig.With(new Config { TextCase = TextCase.SnakeCase });
        return response.FromJson<ComfyWorkflowResponse>();
    }

    public async Task<ComfyWorkflowResponse> GenerateImageToImageUpscaleAsync(StableDiffusionImageToImageUpscale request)
    {
        var comfyRequest = request.ToComfy();
        if(comfyRequest.InitImage == null)
            throw new Exception("Image input is required for Image to Image Upscale");
        
        // Upload image asset
        comfyRequest.Image = await UploadImageAssetAsync(comfyRequest.InitImage, $"image2image_upscale_{Guid.NewGuid()}.png");
        
        // Read template from file for Image to Image
        var workflowJson = await PopulateImageToImageUpscaleWorkflowAsync(comfyRequest);
        // Convert to ComfyUI API JSON format
        var apiJson = await ConvertWorkflowToApiAsync(workflowJson);
        // Call ComfyUI API
        var response = await QueueWorkflowAsync(apiJson);
        // Returns with job ID
        using var jsConfig = JsConfig.With(new Config { TextCase = TextCase.SnakeCase });
        return response.FromJson<ComfyWorkflowResponse>();
    }

    public async Task<ComfyWorkflowResponse> GenerateImageToImageAsync(StableDiffusionImageToImage request)
    {
        var comfyRequest = request.ToComfy();
        if (comfyRequest.Image == null && request.InitImage != null)
        {
            var tempFileName = $"image2image_{Guid.NewGuid()}.png";
            comfyRequest.Image = await UploadImageAssetAsync(request.InitImage, tempFileName);
        }

        if (comfyRequest.Image == null)
            throw new Exception("Image input is required for Image to Image");
        
        // Read template from file for Image to Image
        var workflowJson = await PopulateImageToImageWorkflowAsync(comfyRequest);
        // Convert to ComfyUI API JSON format
        var apiJson = await ConvertWorkflowToApiAsync(workflowJson);
        // Call ComfyUI API
        var response = await QueueWorkflowAsync(apiJson);
        // Returns with job ID
        using var jsConfig = JsConfig.With(new Config { TextCase = TextCase.SnakeCase });
        return response.FromJson<ComfyWorkflowResponse>();
    }

    public async Task<ComfyWorkflowResponse> GenerateTextToImageAsync(StableDiffusionTextToImage request)
    {
        // Convert to Internal DTO
        var comfyRequest = request.ToComfy();
        // Read template from file for Text to Image
        var workflowJson = await PopulateTextToImageWorkflowAsync(comfyRequest);
        // Convert to ComfyUI API JSON format
        var apiJson = await ConvertWorkflowToApiAsync(workflowJson);
        // Call ComfyUI API
        var response = await QueueWorkflowAsync(apiJson);
        // Returns with job ID
        using var jsConfig = JsConfig.With(new Config { TextCase = TextCase.SnakeCase });
        return response.FromJson<ComfyWorkflowResponse>();
    }
    
    public async Task<ComfyAgentDownloadStatus> GetDownloadStatusAsync(string name)
    {
        var response = await httpClient.GetAsync($"/agent/pull?name={name}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        return result.FromJson<ComfyAgentDownloadStatus>();
    }
    
    public async Task<List<ComfyModel>> GetModelsListAsync()
    {
        var response = await httpClient.GetAsync("/engines/list");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        using var jsConfig = JsConfig.With(new Config { TextCase = TextCase.SnakeCase });
        return result.FromJson<List<ComfyModel>>();
    }
    
    /// <summary>
    /// Get agent to remotely download a model
    /// </summary>
    /// <param name="url">URL location to download the model from</param>
    /// <param name="filename">Unique name for the model to be saved and used as</param>
    /// <param name="apiKey">Optional API Key if the download URL requires API key authentication</param>
    /// <param name="apiKeyLocation">Optional API Key Location config which can be used to populate the API Key in a specific way.
    /// For example, `query:token` will use `token` query string when calling the download URL.
    /// `header:x-api-key` will populate the `x-api-key` request header with the API Key value.
    /// `bearer` will populate the Authorization header and use the API Key as a Bearer token.</param>
    /// <returns></returns>
    public async Task<string> DownloadModelAsync(string url, string filename, string apiKey = null, string apiKeyLocation = "")
    {
        var path = $"/agent/pull?url={url}&name={filename}";
        if (!string.IsNullOrEmpty(apiKey))
            path += $"&api_key={apiKey}";
        if (!string.IsNullOrEmpty(apiKeyLocation))
            path += $"&api_key_location={apiKeyLocation}";
        var response = await httpClient.PostAsync(path,null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetPromptHistory(string id)
    {
        var response = await httpClient.GetAsync($"/history/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
    
    public async Task<string> DeleteModelAsync(string name)
    {
        var response = await httpClient.PostAsync($"/agent/delete?name={name}", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
    
    public async Task<Stream> DownloadComfyOutputAsync(ComfyFileOutput output)
    {
        var response = await httpClient.GetAsync($"/view?filename={output.Filename}&type={output.Type}&subfolder={output.Subfolder}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
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

        // Sort workflow nodes by ID
        var orderedNodes = workflowNodes.AsArray().OrderBy(x => int.Parse(x["id"].ToString())).ToList();
        
        // Convert nodes
        foreach (var jToken in orderedNodes)
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
            apiNode["inputs"] = new JsonObject();
            if (metadataMapping.TryGetValue(classType, out var currentClass))
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
            apiNode["class_type"] = classType;
            apiNodes[nodeId] = apiNode;
        }

        return new JsonObject() { ["prompt"] = apiNodes }.ToJsonString();
    }
    private async Task AddToMappingAsync(string classType)
    {
        if (!metadataMapping.ContainsKey(classType))
        {
            var response = await httpClient.GetStringAsync($"/object_info/{classType}");
            var respObject = JsonNode.Parse(response).AsObject();
            if (respObject.TryGetPropertyValue(classType, out var value))
            {
                metadataMapping[classType] = value.AsObject();
            }
        }
    }
    
    /// <summary>
    /// Parse the status JSON response from ComfyUI API
    /// This comes from the /history/{id} endpoint
    /// And has somewhat of a strange format
    /// The root object key is the job ID, and it has 3 properties:
    /// - prompt
    /// - outputs
    /// - status
    ///
    /// `prompt` is an array of 5 elements that seem to be in the order of:
    /// 0. The number of nodes in the workflow (int)
    /// 1. The Job ID (string)
    /// 2. The full API request object that created the job (object)
    /// 3. Not sure, empty object?
    /// 4. An array of strings that represent nodes with outputs. Eg, ["10"]
    ///
    /// `outputs` is an object with keys that match the node IDs that have outputs.
    /// Each of these is an object which contains the output data for that node.
    /// The structure of that output also seems to be node specific.
    ///
    /// `status` is an object with the following properties:
    /// - status_str: A string that represents the status of the job
    /// - completed: A boolean that represents if the job is completed
    /// - messages: An array of messages events that have occurred during the job
    /// Each message is an array with two elements, a name string and an object with the message data,
    /// the structure of which is dependent on the message type.
    /// </summary>
    /// <param name="statusJson"></param>
    /// <returns></returns>
    private ComfyWorkflowStatus ParseWorkflowStatus(JsonObject statusJson, string jobId)
    {
        var hasJob = statusJson.ContainsKey(jobId);
        if (!hasJob)
            throw new Exception("Job ID not found in status JSON");
        
        var job = statusJson[jobId].AsObject();
        var prompt = job["prompt"].AsArray();
        var outputs = job["outputs"].AsObject();
        var status = job["status"].AsObject();

        if (outputs.Count == 0 &&
            status["messages"].AsArray() != null && status["messages"].AsArray().Count > 2 &&
            status["messages"][2].AsArray() != null && status["messages"][2].AsArray().Count > 0 &&
            status["messages"][2][0].ToString() == "execution_error")
        {
            // Check for error messages
            var errorMessages = status["messages"][2][1].AsObject();
            throw new Exception($"Error in job {jobId}: {errorMessages.ToJsonString()}");
        }

        var outputNodeIds = prompt[4].AsArray().GetValues<string>();
        var result = new ComfyWorkflowStatus
        {
            StatusMessage = status["status_str"].ToString(),
            Completed = status["completed"].GetValue<bool>(),
            Outputs = outputNodeIds.Select(x =>
            {
                var output = outputs[x].AsObject();
                var result = new ComfyOutput();
                if (output.ContainsKey("files"))
                {
                    result.Files = output["files"].AsArray().Select(y => new ComfyFileOutput
                    {
                        Filename = y["filename"].ToString(),
                        Type = y["type"].ToString(),
                        Subfolder = y["subfolder"].ToString()
                    }).ToList();
                }
                if (output.ContainsKey("text"))
                {
                    result.Texts = output["text"].AsArray().Select(y => new ComfyTextOutput
                    {
                        Text = y.ToString()
                    }).ToList();
                }

                return result;
            }).ToList()
        };
        
        return result;
    }

    public async Task<ComfyWorkflowStatus> GetWorkflowStatusAsync(string jobId)
    {
        var statusJson = await GetPromptHistory(jobId);
        var parsedStatus = JsonNode.Parse(statusJson);
        if (parsedStatus == null)
            throw new Exception("Invalid status JSON response");
        
        // Handle the case where the status is an empty object
        if (parsedStatus.AsObject().Count == 0)
            return new ComfyWorkflowStatus();
        
        var status = ParseWorkflowStatus(parsedStatus.AsObject(), jobId);
        // Convert to ComfyWorkflowStatus
        return status;
    }
}