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
    const string BaseUrl = "https://localhost:7860";
    readonly ComfyClient client = new (BaseUrl);
    
    [Test]
    [Ignore("Integration test")]
    public async Task Can_call_comfyui_with_original_workflow_json()
    {
        // Take the original workflow JSON from "files/workflow_simple_generation.json"
        var rawWorkflow = System.IO.File.ReadAllText("workflows/workflow_simple_generation.json");
        
        // Convert the workflow JSON to API JSON
        var apiJson = await client.ConvertWorkflowToApiAsync(rawWorkflow);
        
        // Assert that the API JSON is not null
        Assert.That(apiJson, Is.Not.Null);
        
        // Assert that the API JSON is not empty
        Assert.That(apiJson, Is.Not.Empty);
        
        // Call the ComfyUI API with the API JSON
        var response = await client.QueueWorkflowAsync(apiJson);
        
        // Assert that the response is not null
        Assert.That(response, Is.Not.Null);
        
    }

    [Test]
    [Ignore("Integration test")]
    public async Task Can_convert_original_workflow_json()
    {
        // Take the original workflow JSON from "files/workflow_simple_generation.json"
        var rawWorkflow = System.IO.File.ReadAllText("workflows/workflow_simple_generation.json");
        
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
        var models = await client.GetModelsListAsync();
        
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

        var models = await client.GetModelsListAsync();
        Assert.That(models, Is.Not.Null);
        Assert.That(models.Any(x => x.Name.Contains(modelName)), Is.False);
    }

    [Test]
    [Ignore("Integration test")]
    public async Task Can_get_agent_pull_to_download_model()
    {
        var testUrl = "https://civitai.com/api/download/models/9208";
        var testName = "easynegative.safetensors";
        var models = await client.GetModelsListAsync();
        // Assert.That(models, Is.Not.Null);
        // Assert.That(models.Any(x => x.Name.Contains(modelName)), Is.False);

        var downloadRes = await client.DownloadModelAsync(testUrl, testName);
        Assert.That(downloadRes, Is.Not.Null);
        Assert.That(downloadRes, Is.Not.Empty);
        
        models = await client.GetModelsListAsync();
        Assert.That(models, Is.Not.Null);
        Assert.That(models.Any(x => x.Name.Contains(testName)), Is.True);
    }

    [Test]
    public async Task Can_use_ComfyClient_TextToImage()
    {
        // Init test DTO
        var testDto = new StableDiffusionTextToImage()
        {
            CfgScale = 7,
            Seed = Random.Shared.NextInt64(),
            Height = 1024,
            Width = 1024,
            Engine = "zavychromaxl_v80.safetensors",
            Sampler = StableDiffusionSampler.K_EULER_ANCESTRAL,
            Samples = 1,
            Steps = 20,
            TextPrompts = new List<TextPrompt>
            {
                new()
                {
                    Text = "A beautiful sunset over the ocean",
                    Weight = 1.0d,
                },
                new()
                {
                    Text = "low quality, blurry, noisy image",
                    Weight = -1.0d,
                }
            }
        };
        
        var response = await client.GenerateTextToImageAsync(testDto);
        
        Assert.That(response, Is.Not.Null);
        Assert.That(response.PromptId, Is.Not.Empty);
        
        var status = await client.GetWorkflowStatusAsync(response.PromptId);
        int jobTimeout = 20 * 1000; // 20 seconds
        int pollInterval = 1000; // 1 second
        var now = DateTime.UtcNow;
        while (status.Completed == false && (DateTime.UtcNow - now).TotalMilliseconds < jobTimeout)
        {
            await Task.Delay(pollInterval);
            status = await client.GetWorkflowStatusAsync(response.PromptId);
        }
        Assert.That(status, Is.Not.Null);
        Assert.That(status.StatusMessage, Is.EqualTo("success"));
        Assert.That(status.Completed, Is.EqualTo(true));
        Assert.That(status.Outputs, Is.Not.Empty);
        Assert.That(status.Outputs.Count, Is.EqualTo(1));
        Assert.That(status.Outputs[0].Filename, Is.Not.Null);
        Assert.That(status.Outputs[0].Filename.Contains("ComfyUI_temp"), Is.True);
        Assert.That(status.Outputs[0].Type, Is.EqualTo("temp"));
    }

    [Test]
    [Ignore("Integration test")]
    public async Task Can_use_StableDiffusionTextToImage_to_template_workflow()
    {
        // Init test DTO
        var testDto = new ComfyTextToImage()
        {
            CfgScale = 7,
            Seed = Random.Shared.NextInt64(),
            Height = 1024,
            Width = 1024,
            Model = "zavychromaxl_v80.safetensors",
            Sampler = ComfySampler.euler_ancestral,
            BatchSize = 1,
            Steps = 20,
            PositivePrompt = "A beautiful sunset over the ocean",
            NegativePrompt = "low quality, blurry, noisy image",
        };
        
        // Convert template to JSON
        var jsonTemplate = await client.PopulateTextToImageWorkflowAsync(testDto);
        
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
    }
}
