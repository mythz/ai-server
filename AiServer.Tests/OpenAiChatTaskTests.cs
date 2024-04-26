using AiServer.ServiceModel;
using AiServer.Tests.Types;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.Text;

namespace AiServer.Tests;

[Explicit]
public class OpenAiChatTaskTests
{
    [Test]
    public async Task Generate_tasks()
    {
        var questionsDir = TestUtils.GetQuestionsDir();
        questionsDir.Print();
        var testFolder = new DirectoryInfo(Path.Combine(questionsDir, "000/000"));
        var questionFiles = testFolder.GetMatchingFiles("???.json");
        var client = TestUtils.CreateSystemClient();

        var model = "phi3"; //phi3/llama3
        var refIds = new List<string>();

        string nextId()
        {
            var refId = Guid.NewGuid().ToString("N");
            refIds.Add(refId);
            return refId;
        }

        foreach (var questionFile in questionFiles)
        {
            var question = questionFile.ReadAllText().FromJson<Post>();
            // question.PrintDump();
            
            var api = await client.ApiAsync(new CreateOpenAiChat {
                RefId = nextId(),
                Model = model,
                ReplyTo = "https://localhost:5001/api/CompleteOpenAiChat",
                Request = new()
                {
                    Model = model,
                    Messages = [
                        new() { Role = "system", Content = TestUtils.SystemPrompt },
                        new() { Role = "user", Content = question.Body! },
                    ],
                    Temperature = 0.7,
                    MaxTokens = 2048,
                    Stream = false,
                }
            });
            api.Error.PrintDump();
            api.Response.PrintDump();
        }
    }

    [Test]
    public async Task Can_FetchOpenAiChatRequests()
    {
        var client = TestUtils.CreateSystemClient();

        var api = await client.ApiAsync(new FetchOpenAiChatRequests
        {
            Provider = "ollama",
            Worker = nameof(OpenAiChatTaskTests),
            Models = ["phi3"],
            Take = 3,
        });
        
        Assert.That(api.ErrorMessage, Is.Null);
        Assert.That(api.Response!.Results.Length, Is.EqualTo(3));
        Assert.That(api.Response!.Results.All(x => x.Request.Model == "phi3"));

        api = await client.ApiAsync(new FetchOpenAiChatRequests
        {
            Provider = "ollama",
            Worker = nameof(OpenAiChatTaskTests),
            Models = ["llama3"],
            Take = 3,
        });
        
        Assert.That(api.ErrorMessage, Is.Null);
        Assert.That(api.Response!.Results.Length, Is.EqualTo(3));
        Assert.That(api.Response!.Results.All(x => x.Request.Model == "llama3"));

        api = await client.ApiAsync(new FetchOpenAiChatRequests
        {
            Provider = "ollama",
            Worker = nameof(OpenAiChatTaskTests),
            Models = ["phi3", "llama3"],
            Take = 3,
        });
        
        Assert.That(api.ErrorMessage, Is.Null);
        Assert.That(api.Response!.Results.Length, Is.EqualTo(3));
        Assert.That(api.Response!.Results.All(x => x.Request.Model is "phi3" or "llama3"));
    }

    [Test]
    public async Task Can_RequeueIncompleteTasks()
    {
        var client = TestUtils.CreateAdminClient();

        var api = await client.ApiAsync(new OpenAiChatOperations
        {
            RequeueIncompleteTasks = true
        });
        api.Response.PrintDump();
        api.Error.PrintDump();
    }

}
