﻿using ServiceStack;
using ServiceStack.DataAnnotations;

namespace AiServer.ServiceModel;

[ValidateApiKey]
[Description("API Providers that can process AI Tasks")]
public class QueryApiProviders : QueryDb<ApiProvider>
{
    public string? Name { get; set; }
}

[ValidateAuthSecret]
[Description("Create an API Provider that can process AI Tasks")]
[AutoPopulate(nameof(ApiProvider.CreatedDate),  Eval = "utcNow")]
public class CreateApiProvider : ICreateDb<ApiProvider>, IReturn<IdResponse>
{
    [Description("The unique name for this API Provider")]
    [Index(Unique = true)]
    public string Name { get; set; }
    
    [Description("The behavior for this API Provider")]
    public int ApiTypeId { get; set; }
    
    [Description("The API Key to use for this Provider")]
    public string? ApiKey { get; set; }

    [Description("Send the API Key in the Header instead of Authorization Bearer")]
    public string? ApiKeyHeader { get; set; }
    
    [Description("The Base URL for the API Provider")]
    public string? ApiBaseUrl { get; set; }

    [Description("The URL to check if the API Provider is still online")]
    public string? HeartbeatUrl { get; set; }
    
    [Description("Override API Paths for different AI Tasks")]
    public Dictionary<TaskType, string>? TaskPaths { get; set; }
    
    [Description("How many requests should be made concurrently")]
    public int Concurrency { get; set; }
    
    [Description("What priority to give this Provider to use for processing models")]
    public int Priority { get; set; }
    
    [Description("Whether the Provider is enabled")]
    public bool Enabled { get; set; }

    [Description("The models this API Provider can process")]
    public List<ApiProviderModel> Models { get; set; }
}

[ValidateAuthSecret]
[Description("View and API Provider Models")]
public class QueryApiProviderModels : QueryDb<ApiProviderModel>
{
    public int? ApiProviderId { get; set; }
    public string? Model { get; set; }
    public string? ApiModel { get; set; }
}

[ValidateAuthSecret]
[Description("Register a Model supported by an API Provider")]
public class CreateApiProviderModel : ICreateDb<ApiProviderModel>, IReturn<IdResponse>
{
    [Description("The ApiProvider Id")]
    public int ApiProviderId { get; set; }
    
    [Description("Supported ApiModel Name")]
    public string Model { get; set; }
    
    [Description("Model to use when sending requests to the API Provider")]
    public string? ApiModel { get; set; }
}

[ValidateAuthSecret]
[Description("Update the Model supported by the API Provider")]
public class UpdateApiProviderModel : IPatchDb<ApiProviderModel>, IReturn<IdResponse>
{
    [Description("The ApiProviderModel Id")]
    public int Id { get; set; }
    
    [Description("The ApiProvider Id")]
    public int? ApiProviderId { get; set; }
    
    [Description("Supported ApiModel Name")]
    public string? Model { get; set; }
    
    [Description("Model to use when sending requests to the API Provider")]
    public string? ApiModel { get; set; }
}

[ValidateAuthSecret]
[Description("Delete a Model supported by the API Provider")]
public class DeleteApiProviderModel : IDeleteDb<ApiProviderModel>, IReturn<IdResponse>
{
    [Description("The ApiProviderModel Id")]
    public int Id { get; set; }
}

[ValidateApiKey]
[Description("Different Models available in AI Server")]
public class QueryApiModels : QueryDb<ApiModel> {}

[ValidateAuthSecret]
[Description("Different Models available for the API")]
public class CreateApiModel : ICreateDb<ApiModel>, IReturn<IdResponse>
{
    public string Name { get; set; }

    public string? Website { get; set; }
    
    public int? Parameters { get; set; }

    public int? ContextSize { get; set; }

    public int? Experts { get; set; }
    
    public string? Notes { get; set; }
}

[ValidateApiKey]
[Description("The Type and behavior of different API Providers")]
public class QueryApiType : QueryDb<ApiType> {}
