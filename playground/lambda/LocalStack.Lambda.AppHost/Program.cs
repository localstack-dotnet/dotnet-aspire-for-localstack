using Amazon;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.LocalStack.Container;
using AWSCDK.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Set up a configuration for the AWS .NET SDK.
// us-east-1 is deliberate: Amazon.Lambda.TestTool's bundled AWS SDK loses its signing region whenever
// AWS_ENDPOINT_URL* variables are set, so its DynamoDB Streams poller always signs for us-east-1.
// LocalStack scopes tables and streams per region, so any other region leaves the poller unable to
// find the stream until the tool ships a fixed SDK.
var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.USEast1);

// Bootstrap the localstack container with enhanced configuration
var localstack = builder
    .AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
    });

var urlShortenerStack = builder
    .AddAWSCDKStack("custom", scope => new UrlShortenerStack(scope, "aspire-url-shortener"))
    .WithReference(awsConfig);

urlShortenerStack.AddOutput("QrBucketName", stack => stack.QrBucket.BucketName);
urlShortenerStack.AddOutput("UrlsTableName", stack => stack.UrlsTable.TableName);
urlShortenerStack.AddOutput("AnalyticsQueueUrl", stack => stack.AnalyticsQueue.QueueUrl);
urlShortenerStack.AddOutput("AnalyticsTableName", stack => stack.AnalyticsTable.TableName);

urlShortenerStack.WithTag("aws-repo", "integrations-on-dotnet-aspire-for-aws");

var urlShortenerLambda = builder
    .AddAWSLambdaFunction<Projects.LocalStack_Lambda_UrlShortener>(
        name: "UrlShortenerLambda",
        lambdaHandler: "LocalStack.Lambda.UrlShortener::LocalStack.Lambda.UrlShortener.Function::FunctionHandler")
    .WithReference(urlShortenerStack);

var redirectorLambda = builder
    .AddAWSLambdaFunction<Projects.LocalStack_Lambda_Redirector>(
        name: "RedirectorLambda",
        lambdaHandler: "LocalStack.Lambda.Redirector::LocalStack.Lambda.Redirector.Function::FunctionHandler")
    .WithReference(urlShortenerStack);

builder.AddAWSLambdaFunction<Projects.LocalStack_Lambda_Analyzer>(
        name: "AnalyzerLambda",
        lambdaHandler: "LocalStack.Lambda.Analyzer::LocalStack.Lambda.Analyzer.Function::FunctionHandler")
    .WithSQSEventSource(urlShortenerStack.GetOutput("AnalyticsQueueUrl"))
    .WithReference(urlShortenerStack);

builder.AddAWSLambdaFunction<Projects.LocalStack_Lambda_QrCodeGenerator>(
        name: "QrCodeGeneratorLambda",
        lambdaHandler: "LocalStack.Lambda.QrCodeGenerator::LocalStack.Lambda.QrCodeGenerator.Function::FunctionHandler")
    .WithDynamoDBStreamsEventSource(urlShortenerStack.GetOutput("UrlsTableName"))
    .WithReference(urlShortenerStack);

var apiGateway = builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(urlShortenerLambda, Method.Post, "/shorten")
    .WithReference(redirectorLambda, Method.Get, "/{slug}")
    .WithReference(redirectorLambda, Method.Get, "/{slug}/qr");

builder.AddProject<Projects.LocalStack_Lambda_Frontend>("Frontend")
    .WithReference(urlShortenerStack)
    .WithEnvironment("ApiGateway__BaseUrl", apiGateway.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(apiGateway);

// Autoconfigures the LocalStack for both AWS Cloudformation and CDK resources adds LocalStack reference to all resources that uses AWS references
builder.UseLocalStack(localstack);

await builder.Build().RunAsync().ConfigureAwait(false);
