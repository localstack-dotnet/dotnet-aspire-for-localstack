// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Originally copied from https://github.com/aws/integrations-on-dotnet-aspire-for-aws
// and adjusted for Aspire.Hosting.LocalStack. All rights reserved.

using Amazon.DynamoDBv2;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using LocalStack.Client.Extensions;
using LocalStack.Client.Options;
using LocalStack.Provisioning.Frontend.Components;
using LocalStack.Provisioning.Frontend.Handlers;
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
    var chatQueueUrl = builder.Configuration["AWS:Resources:ChatMessagesQueueUrl"];

    if (chatTopicArn != null)
    {
        messageBuilder.AddSNSPublisher<ChatMessage>(chatTopicArn);
    }

    if (chatQueueUrl != null)
    {
        messageBuilder.AddSQSPoller(chatQueueUrl)
            .AddMessageHandler<ChatMessageHandler, ChatMessage>();
    }
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register the message handler
builder.Services.AddSingleton<ChatMessageHandler>();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/healthcheck/dynamodb", async (HttpContext httpContext) =>
{
    var ddbClient = app.Services.GetRequiredService<IAmazonDynamoDB>();
    var localStackOptions = app.Services.GetRequiredService<IOptions<LocalStackOptions>>().Value;

    try
    {
        // Check if the expected ChatMessages table exists
        var expectedTableName = app.Configuration["AWS:Resources:ChatMessagesTableName"] ?? "ChatMessages";

        var listTablesRequest = new Amazon.DynamoDBv2.Model.ListTablesRequest();
        var serviceUrl = ddbClient.DetermineServiceOperationEndpoint(listTablesRequest).URL;

        var tablesResponse = await ddbClient.ListTablesAsync(listTablesRequest, httpContext.RequestAborted).ConfigureAwait(false);
        var tableExists = tablesResponse.TableNames.Contains(expectedTableName, StringComparer.OrdinalIgnoreCase);

        string? tableStatus = null;
        long? itemCount = null;

        if (tableExists)
        {
            try
            {
                var describeResponse = await ddbClient.DescribeTableAsync(new Amazon.DynamoDBv2.Model.DescribeTableRequest
                {
                    TableName = expectedTableName,
                }).ConfigureAwait(false);
                tableStatus = describeResponse.Table.TableStatus;
                itemCount = describeResponse.Table.ItemCount;
            }
            catch (Amazon.DynamoDBv2.Model.ResourceNotFoundException)
            {
                // Table was listed but now not found - race condition
                tableExists = false;
                tableStatus = "NotFound";
            }
            catch (AmazonDynamoDBException ex)
            {
                // DynamoDB specific error
                tableStatus = $"DynamoDB Error: {ex.Message}";
            }
        }

        var healthCheck = new
        {
            Status = tableExists ? "Healthy" : "Unhealthy",
            IsLocalStack = localStackOptions.UseLocalStack,
            ServiceUrl = serviceUrl,
            LocalStackHost = localStackOptions.UseLocalStack ? localStackOptions.Config.LocalStackHost : null,
            Table = new
            {
                Name = expectedTableName,
                Exists = tableExists,
                Status = tableStatus,
                ItemCount = itemCount,
            },
            AvailableTables = tablesResponse.TableNames,
            Timestamp = DateTime.UtcNow,
        };

        return tableExists
            ? Results.Ok(healthCheck)
            : Results.BadRequest(healthCheck);
    }
    catch (AmazonDynamoDBException ex)
    {
        var errorResponse = new
        {
            Status = "DynamoDB Error",
            IsLocalStack = localStackOptions.UseLocalStack,
            ErrorMessage = ex.Message,
            ex.ErrorCode,
            ErrorType = ex.ErrorType.ToString(),
            Timestamp = DateTime.UtcNow,
        };

        return Results.Problem(
            detail: ex.Message,
            title: "DynamoDB Health Check Failed",
            statusCode: 500,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["details"] = errorResponse }
        );
    }
    catch (HttpRequestException ex)
    {
        var errorResponse = new
        {
            Status = "Connection Error",
            IsLocalStack = localStackOptions.UseLocalStack,
            ErrorMessage = ex.Message,
            ErrorType = nameof(HttpRequestException),
            Timestamp = DateTime.UtcNow,
        };

        return Results.Problem(
            detail: ex.Message,
            title: "DynamoDB Connection Failed",
            statusCode: 503,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["details"] = errorResponse }
        );
    }
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

    return builder.Configuration["AWS:Resources:ChatMessagesQueueUrl"] == null
        ? Results.BadRequest("Missing ChatTopicArn")
        : Results.Ok("Success");
});


await app.RunAsync().ConfigureAwait(false);
