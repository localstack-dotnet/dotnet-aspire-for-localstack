using System.Security.Cryptography;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LocalStack;
using Aspire.Hosting.LocalStack.Configuration;
using LocalStack.Client.Options;

if (args.Length != 1)
{
    return 2;
}

var loadedAssembly = typeof(LocalStackResource).Assembly.Location;
var loadedHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(loadedAssembly)));
if (!string.Equals(loadedHash, args[0], StringComparison.OrdinalIgnoreCase))
{
    return 3;
}

var builder = DistributedApplication.CreateBuilder([]);
var options = new LocalStackOptions(false, new SessionOptions(), new ConfigOptions())
    .WithEdgePort(4566).WithRegion("eu-central-1");
var oldShape = builder.AddLocalStack("old-shape", localStackOptions: options, awsConfig: null, configureContainer: null);
if (oldShape is not null || builder.AddLocalStack() is not null || !ReferenceEquals(builder.UseLocalStack(null), builder))
{
    return 4;
}

var legacyResource = new LocalStackResource("old-resource", options);
if (legacyResource.Options.UseLocalStack || legacyResource.Options.Session.RegionName != "eu-central-1"
    || builder.AddLocalStackOptions().UseLocalStack)
{
    return 5;
}

#if CANDIDATE_FLUENT
var parameter = builder.AddParameter("activation", () => "synthetic-consumer-probe", secret: true);
var container = new LocalStackContainerOptions();
if (!ReferenceEquals(container.WithLegacy().WithLegacy(false).WithAuthToken("synthetic").WithAuthToken(parameter), container))
{
    return 6;
}
if (builder.AddLocalStack("new-shape", awsConfig: null, hosting => hosting.WithEnabled(false),
        image => image.WithLegacy().WithAuthToken(parameter)) is not null)
{
    return 7;
}
#endif

await using var app = builder.Build();
Console.WriteLine("ABI_OK");
return 0;
