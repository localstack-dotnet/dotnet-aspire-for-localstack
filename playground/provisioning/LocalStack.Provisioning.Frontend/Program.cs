// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Originally copied from https://github.com/aws/integrations-on-dotnet-aspire-for-aws
// and adjusted for Aspire.Hosting.LocalStack. All rights reserved.

using Amazon.DynamoDBv2;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using LocalStack.Client.Extensions;
using LocalStack.Client.Options;
using LocalStack.Provisioning.Frontend.Components;
using LocalStack.Provisioning.Frontend.Models;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddLocalStack(builder.Configuration);
builder.Services.AddAWSServiceLocalStack<IAmazonDynamoDB>();
builder.Services.AddAWSServiceLocalStack<IAmazonSQS>();
builder.Services.AddAWSServiceLocalStack<IAmazonSimpleNotificationService>();

// Configuring messaging using the AWS.Messaging library.
builder.Services.AddAWSMessageBus(messageBuilder =>
{
    // Get the SQS queue URL that was created from AppHost and assigned to the project.
    var chatTopicArn = builder.Configuration["AWS:Resources:ChatTopicArn"];
    if (chatTopicArn != null)
    {
        messageBuilder.AddSNSPublisher<ChatMessage>(chatTopicArn);
    }
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

#pragma warning disable S1481
var localStackOptions = app.Services.GetRequiredService<IOptions<LocalStackOptions>>().Value;
var requiredService = app.Services.GetRequiredService<IAmazonSQS>();
#pragma warning restore S1481

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/healthcheck/dynamodb", (HttpContext _) =>
{
    var ddbClient = app.Services.GetRequiredService<IAmazonDynamoDB>();
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL_DYNAMODB")))
    {
        return Results.BadRequest("The AWS_ENDPOINT_URL_DYNAMODB is not set");
    }

    if (!ddbClient.Config.ServiceURL.StartsWith(Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL_DYNAMODB")!, StringComparison.Ordinal))
    {
        return Results.BadRequest("The DynamoDB service client is not configured for DyanamoDB local");
    }

    return Results.Ok("Success");
});


app.MapGet("/healthcheck/cloudformation", (HttpContext _) =>
{
    // Confirm the WithEnvironment behavior
    if (builder.Configuration["ChatTopicArnEnv"] == null)
    {
        return Results.BadRequest("Missing ChatTopicArnEnv");
    }

    // Confirm the WithReference behavior
    if (builder.Configuration["AWS:Resources:ChatTopicArn"] == null)
    {
        return Results.BadRequest("Missing ChatTopicArn");
    }

    if (builder.Configuration["AWS:Resources:ChatMessagesQueueUrl"] == null)
    {
        return Results.BadRequest("Missing ChatTopicArn");
    }

    return Results.Ok("Success");
});


await app.RunAsync().ConfigureAwait(false);
