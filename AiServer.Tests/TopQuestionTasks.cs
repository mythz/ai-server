using AiServer.ServiceModel;
using AiServer.Tests.Types;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.Text;

namespace AiServer.Tests;

public class TopQuestionTasks
{
    // [Test]
    // [Ignore("Admin task")]
    // public async Task GenerateTop1000QuestionAnswers()
    // {
    //     // Read in the list of top question IDs from a line separated text file
    //     var topQuestionIdPaths = await File.ReadAllLinesAsync(Path.Combine("files", "top1000questions.txt"));
    //     
    //     Console.WriteLine($"Top questions contains {topQuestionIdPaths.Length} questions");
    //     var questionFiles = topQuestionIdPaths.Select(x => new FileInfo(Path.Combine(TestUtils.GetQuestionsDir(), x)).FullName).ToList();
    //     
    //     
    //     
    //     // Limit to just two to test
    //     questionFiles = questionFiles.Take(2).ToList();
    //     
    //     var client = TestUtils.CreateSystemClient();
    //     foreach (var questionFile in questionFiles)
    //     {
    //         var question = questionFile.ReadAllText().FromJson<Post>();
    //         var body = question.Body ?? throw new ArgumentNullException(nameof(Post.Body));
    //         // question.PrintDump();
    //         
    //         var replyTo = TestUtils.PvqBaseUrl.CombineWith("api/CreateAnswerCallback")
    //             .AddQueryParams(new() {
    //                 ["PostId"] = question.Id,
    //                 ["UserId"] = TestUtils.ModerUserIds["gpt-4-turbo"]
    //             });
    //         
    //         await TestUtils.CreateOpenAiChatTask(client, model:"gpt-4-turbo", body:body, provider:"openrouter",
    //             replyTo:replyTo);
    //     }
    //
    // }
}