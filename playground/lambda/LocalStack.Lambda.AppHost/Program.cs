#pragma warning disable CA2252 // Using 'AddAWSLambdaFunction' requires opting into preview features.

using Amazon;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.LocalStack.Container;
using AWSCDK.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Set up a configuration for the AWS .NET SDK
var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);

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

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(urlShortenerLambda, Method.Post, "/shorten")
    .WithReference(redirectorLambda, Method.Get, "/{slug}");

// Autoconfigures the LocalStack for both AWS Cloudformation and CDK resources adds LocalStack reference to all resources that uses AWS references
builder.UseLocalStack(localstack);

await builder.Build().RunAsync().ConfigureAwait(false);
