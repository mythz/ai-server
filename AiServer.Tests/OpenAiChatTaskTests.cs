using System.Diagnostics;
using AiServer.ServiceModel;
using AiServer.Tests.Types;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.Text;

namespace AiServer.Tests;

[Explicit]
public class OpenAiChatTaskTests
{
    string nextId()
    {
        var refId = Guid.NewGuid().ToString("N");
        return refId;
    }

    [Test]
    public async Task Generate_phi3_tasks()
    {
        var testFolder = new DirectoryInfo(Path.Combine(TestUtils.GetQuestionsDir(), "000/000"));
        var questionFiles = testFolder.GetMatchingFiles("???.json");
        await CreateQuestionTasks(questionFiles, model:"phi3");
    }

    [Test]
    public async Task Generate_llama3_tasks()
    {
        var testFolder = new DirectoryInfo(Path.Combine(TestUtils.GetQuestionsDir(), "000/001"));
        var questionFiles = testFolder.GetMatchingFiles("???.json");
        await CreateQuestionTasks(questionFiles, model:"llama3:8b");
    }

    [Test]
    public async Task Generate_llama3_ollama_tasks()
    {
        var testFolder = new DirectoryInfo(Path.Combine(TestUtils.GetQuestionsDir(), "000/002"));
        var questionFiles = testFolder.GetMatchingFiles("???.json");
        await CreateQuestionTasks(questionFiles, model:"llama3:8b", provider:"ollama");
    }

    [Test]
    public async Task Generate_llama3_openrouter_tasks()
    {
        var testFolder = new DirectoryInfo(Path.Combine(TestUtils.GetQuestionsDir(), "000/003"));
        var questionFiles = testFolder.GetMatchingFiles("???.json");
        await CreateQuestionTasks(questionFiles, model:"llama3:8b", provider:"openrouter");
    }

    [Test]
    public async Task Generate_mixtral_openrouter_tasks()
    {
        var testFolder = new DirectoryInfo(Path.Combine(TestUtils.GetQuestionsDir(), "000/004"));
        var questionFiles = testFolder.GetMatchingFiles("???.json");
        await CreateQuestionTasks(questionFiles, model:"mixtral", provider:"openrouter");
    }

    private async Task CreateQuestionTasks(IEnumerable<string> questionFiles, string model, string? provider = null, string? replyTo = null)
    {
        var client = TestUtils.CreateSystemClient();
        foreach (var questionFile in questionFiles)
        {
            var question = questionFile.ReadAllText().FromJson<Post>();
            var body = question.Body ?? throw new ArgumentNullException(nameof(Post.Body));
            // question.PrintDump();
            
            await CreateOpenAiChatTask(client, model:model, body:body, replyTo:replyTo, provider:provider);
        }
    }

    private async Task<CreateOpenAiChatResponse?> CreateOpenAiChatTask(JsonApiClient client, string model, string body, 
        string? replyTo=null, string? provider=null)
    {
        var api = await client.ApiAsync(new CreateOpenAiChat {
            RefId = nextId(),
            Provider = provider,
            ReplyTo = replyTo,
            Request = new()
            {
                Model = model,
                Messages = [
                    new() { Role = "system", Content = TestUtils.SystemPrompt },
                    new() { Role = "user", Content = body },
                ],
                Temperature = 0.7,
                MaxTokens = 2048,
                Stream = false,
            }
        });
        api.Error.PrintDump();
        api.ThrowIfError();
        return api.Response;
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
            Models = ["llama3:8b"],
            Take = 3,
        });
        
        Assert.That(api.ErrorMessage, Is.Null);
        Assert.That(api.Response!.Results.Length, Is.EqualTo(3));
        Assert.That(api.Response!.Results.All(x => x.Request.Model == "llama3:8b"));
    }

    [Test]
    public async Task Can_FetchOpenAiChatRequests_with_multiple_models()
    {
        var client = TestUtils.CreateAdminClient();
        var api = await client.ApiAsync(new FetchOpenAiChatRequests
        {
            Provider = "ollama",
            Worker = nameof(OpenAiChatTaskTests),
            Models = ["phi3", "llama3:8b"],
            Take = 3,
        });
        
        Assert.That(api.ErrorMessage, Is.Null);
        Assert.That(api.Response!.Results.Length, Is.EqualTo(3));
        Assert.That(api.Response!.Results.All(x => x.Request.Model is "phi3" or "llama3:8b"));
    }

    [Test]
    public async Task Can_FetchOpenAiChatRequests_with_openrouter()
    {
        var client = TestUtils.CreateAdminClient();
        var api = await client.ApiAsync(new FetchOpenAiChatRequests
        {
            Provider = "openrouter",
            Worker = nameof(OpenAiChatTaskTests),
            Models = ["phi3", "llama3:8b"],
            Take = 3,
        });
        
        Assert.That(api.ErrorMessage, Is.Null);
        Assert.That(api.Response!.Results.Length, Is.EqualTo(3));
        Assert.That(api.Response!.Results.All(x => x is { Provider: null or "openrouter", Model: "phi3" or "llama3:8b" }));
    }

    [Test]
    public async Task Can_FetchOpenAiChatRequests_with_openrouter_and_mixtral()
    {
        var client = TestUtils.CreateAdminClient();
        var api = await client.ApiAsync(new FetchOpenAiChatRequests
        {
            Provider = "openrouter",
            Worker = nameof(OpenAiChatTaskTests),
            Models = ["mixtral"],
            Take = 3,
        });
        
        Assert.That(api.ErrorMessage, Is.Null);
        api.Response!.Results.PrintDump();
        Assert.That(api.Response!.Results.Length, Is.EqualTo(3));
        Assert.That(api.Response!.Results.All(x => x is { Provider: null or "openrouter", Model: "mixtral" }));
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

    [Test]
    public async Task Can_queue_pvq_answer_to_question()
    {
        var pvqApi = TestUtils.PvqApiClient();
        await pvqApi.SendAsync(new Authenticate
        {
            provider = "credentials",
            UserName = TestUtils.PvqUsername,
            Password = TestUtils.PvqPassword,
        });

        var model = "phi3";
        var postId = 100000001;
        var question = await pvqApi.ApiAsync(new GetQuestionBody
        {
            Id = postId,
        });
        var body = question.Response;
        body.Print();
        
        var client = TestUtils.CreatePvqClient();
        var replyTo = pvqApi.BaseUri.CombineWith("api/CreateAnswerCallback")
            .AddQueryParams(new() {
                ["PostId"] = postId,
                ["UserId"] = TestUtils.ModerUserIds[model]
            });
        var response = await CreateOpenAiChatTask(client, model:model, body:body!, replyTo:replyTo);
        response.PrintDump();
    }

    [Test]
    public async Task Can_execute_ollama_task()
    {
        var model = "phi3";
        var client = TestUtils.CreatePvqClient();

        var api = await client.ApiAsync(new FetchOpenAiChatRequests
        {
            Provider = "ollama",
            Worker = nameof(OpenAiChatTaskTests),
            Models = ["phi3"],
            Take = 1,
        });
        api.ThrowIfError();
        
        var entry = api.Response!.Results.First();
        var chatRequest = entry.Request;

        var sw = Stopwatch.StartNew();
        var openApiChatEndpoint = "http://macbook.pvq.app/v1/chat/completions";
        var responseJson = await openApiChatEndpoint.PostJsonToUrlAsync(chatRequest);
        var durationMs = (int)sw.ElapsedMilliseconds;
        
        var response = responseJson.FromJson<OpenAiChatResponse>();
        response.PrintDump();

        var completeApi = await client.ApiAsync(new CompleteOpenAiChat
        {
            Id = entry.Id,
            Provider = "ollama",
            DurationMs = durationMs,
            Response = response,
        });
        
        completeApi.ThrowIfError();
    }

    [Test]
    public async Task Can_execute_answers_for_llama3()
    {
        var models = new[] { "llama3:8b" };

        var testFolder = new DirectoryInfo(Path.Combine(TestUtils.GetQuestionsDir(), "100/000"));
        
        var postId = 100000001;
        
        var questionFiles = testFolder.GetMatchingFiles("???.json").ToList();
        foreach (var model in models)
        {
            var replyTo = TestUtils.PvqBaseUrl.CombineWith("api/CreateAnswerCallback")
                .AddQueryParams(new() {
                    ["PostId"] = postId,
                    ["UserId"] = TestUtils.ModerUserIds[model]
                });
            
            await CreateQuestionTasks(questionFiles, model:model, replyTo:replyTo);
        }
    }

    [Test]
    public async Task Can_execute_answers_from_multiple_models()
    {
        var models = TestUtils.ModerUserIds.Keys.ToList();

        var testFolder = new DirectoryInfo(Path.Combine(TestUtils.GetQuestionsDir(), "100/000"));
        
        var postId = 100000001;
        
        var questionFiles = testFolder.GetMatchingFiles("???.json").ToList();
        foreach (var model in models)
        {
            var replyTo = TestUtils.PvqBaseUrl.CombineWith("api/CreateAnswerCallback")
                .AddQueryParams(new() {
                    ["PostId"] = postId,
                    ["UserId"] = TestUtils.ModerUserIds[model]
                });
            
            await CreateQuestionTasks(questionFiles, model:model, replyTo:replyTo);
        }
    }

}
