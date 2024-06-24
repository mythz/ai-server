using System.Text.Json;
using System.Text.Json.Nodes;
using AiServer.ServiceInterface;
using AiServer.ServiceModel;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.Text;
using JsonObject = System.Text.Json.Nodes.JsonObject;

namespace AiServer.Tests;

public class ComfyUITests
{
    const string BaseUrl = "http://localhost:7860";
    readonly ComfyClient client = new ComfyClient(BaseUrl);
    
    [Test]
    [Ignore("Integration test")]
    public async Task Can_call_comfyui_with_original_workflow_json()
    {
        // Take the original workflow JSON from "files/workflow_simple_generation.json"
        var rawWorkflow = System.IO.File.ReadAllText("files/workflow_simple_generation.json");
        
        // Convert the workflow JSON to API JSON
        var apiJson = await client.ConvertWorkflowToApiAsync(rawWorkflow);
        
        // Assert that the API JSON is not null
        Assert.That(apiJson, Is.Not.Null);
        
        // Assert that the API JSON is not empty
        Assert.That(apiJson, Is.Not.Empty);
        
        // Call the ComfyUI API with the API JSON
        var response = await client.PromptGenerationAsync(apiJson);
        
        // Assert that the response is not null
        Assert.That(response, Is.Not.Null);
        
    }

    [Test]
    [Ignore("Integration test")]
    public async Task Can_convert_original_workflow_json()
    {
        // Take the original workflow JSON from "files/workflow_simple_generation.json"
        var rawWorkflow = System.IO.File.ReadAllText("files/workflow_simple_generation.json");
        
        // Convert the workflow JSON to API JSON
        var apiJson = await client.ConvertWorkflowToApiAsync(rawWorkflow);
        
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
    public async Task Can_fetch_all_models_from_ComfyUI()
    {
        var response = await client.GetModelsListAsync();
        
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
    public async Task Can_delete_model_from_ComfyUI()
    {
        var modelName = "zavychromaxl_v80.safetensors"; // friendly named model
        var deleteRes = await client.DeleteModelAsync(modelName);
        Assert.That(deleteRes, Is.Not.Null);
        Assert.That(deleteRes, Is.Not.Empty);

        var response = await client.GetModelsListAsync();
        var models = response.FromJson<List<StableDiffusionEngine>>();
        Assert.That(models, Is.Not.Null);
        Assert.That(models.Any(x => x.Name.Contains(modelName)), Is.False);
    }

    [Test]
    [Ignore("Integration test")]
    public async Task Can_get_agent_pull_to_download_model()
    {
        var modelName = "zavychromaxl"; // friendly named model
        var response = await client.GetModelsListAsync();
        
        var models = response.FromJson<List<StableDiffusionEngine>>();
        // Assert.That(models, Is.Not.Null);
        // Assert.That(models.Any(x => x.Name.Contains(modelName)), Is.False);

        var downloadRes = await client.DownloadModelAsync(modelName);
        Assert.That(downloadRes, Is.Not.Null);
        Assert.That(downloadRes, Is.Not.Empty);
        
        response = await client.GetModelsListAsync();
        models = response.FromJson<List<StableDiffusionEngine>>();
        Assert.That(models, Is.Not.Null);
        Assert.That(models.Any(x => x.Name.Contains(modelName)), Is.True);
    }

    [Test]
    [Ignore("Integration test")]
    public async Task Can_use_StableDiffusionTextToImage_to_template_workflow()
    {
        // Load template
        var template = System.IO.File.ReadAllText("files/workflow_template_txt2img.json");
        
        // Init test DTO
        var testDto = new StableDiffusionTextToImage
        {
            CfgScale = 7,
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
        var jsonTemplate = client.ReplacePlaceholdersInJson(template, dict);
        
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

        var apiJson = await client.ConvertWorkflowToApiAsync(jsonTemplate);
        
        // Request to ComfyUI
        var response = await client.PromptGenerationAsync(apiJson);
        
        // Poll for results using /history/{id}
        var historyId = JsonNode.Parse(response)["prompt_id"].ToString();
        
        // Assert that the response is not null
        Assert.That(response, Is.Not.Null);
        Assert.That(historyId, Is.Not.Null);

        var fileResults = await client.GetPromptResultFile(historyId);
        Assert.That(fileResults.Item2, Is.Not.Null);
        Assert.That(fileResults.Item2.Length, Is.GreaterThan(0));
        await File.WriteAllBytesAsync($"files/{fileResults.Item1}", fileResults.Item2);
        Assert.That(File.Exists($"files/{fileResults.Item1}"), Is.True);
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