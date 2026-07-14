# WS3A `AddLocalStack` Overload Compatibility Spike

**Date**: July 14, 2026

## Question

How can WS3A add package-owned `LocalStackHostingOptions` without breaking existing source or binary consumers of this released signature?

```csharp
AddLocalStack(
    string name = "localstack",
    ILocalStackOptions? localStackOptions = null,
    IAWSSDKConfig? awsConfig = null,
    Action<LocalStackContainerOptions>? configureContainer = null)
```

The existing method is unusually constraining because every parameter is optional and ordinary calls such as `AddLocalStack()`, `AddLocalStack(awsConfig: ...)`, and `AddLocalStack(configureContainer: ...)` all bind to it.

## Environment

- Package target frameworks: `net8.0`, `net9.0`, and `net10.0`
- Repository compiler: .NET SDK `10.0.301`, `LangVersion=latest`
- Consumer compilers tested: SDK `8.0.422`, `9.0.315`, and `10.0.301`, initially with `LangVersion=latest`
- Isolated artifacts: `/tmp/opencode/ws3-addlocalstack-overloads`

## Existing Call Shapes

Repository source, tests, samples, and README use these forms:

```csharp
builder.AddLocalStack();
builder.AddLocalStack(name: "custom");
builder.AddLocalStack(awsConfig: awsConfig);
builder.AddLocalStack(configureContainer: configureContainer);
builder.AddLocalStack(awsConfig: awsConfig, configureContainer: configureContainer);
builder.AddLocalStack(localStackOptions: legacyOptions);
```

The primary README and playground path combines `awsConfig` and `configureContainer`. Unit tests explicitly pass `localStackOptions` by name.

## Matrix

| Candidate | SDK 8 | SDK 9 | SDK 10 | Result |
| --- | --- | --- | --- | --- |
| Add a clean optional overload without `ILocalStackOptions` | CS0121 | CS0121 | CS0121 | Ordinary calls are ambiguous |
| Add an optional owned-options overload with overload priority | CS0121 | Pass | Pass | Reject: source compatibility depends on consumer compiler |
| Add only an exact zero-argument bridge and obsolete the legacy overload | Warnings | Warnings | Warnings | Only `AddLocalStack()` is protected; named common calls become obsolete |
| Add a required owned-options overload and obsolete the legacy overload | Warnings | Warnings | Warnings | Compiles, but every ordinary existing call warns |
| Add a required owned-options overload and retain the legacy overload without obsoleting the whole method | Pass | Pass | Pass | Source-compatible and warning-free |

The deciding factor is the consumer's C# language version, not its runtime. Overload resolution priority is a C# 13 feature. With `LangVersion=latest`, SDK 9 uses C# 13 and SDK 10 uses C# 14, so both honor the priority. SDK 8's latest compiler is C# 12 and reports CS0121 after ignoring the imported priority for overload selection. SDK 10 explicitly pinned to C# 12 also rejects source use of the attribute with CS9202. A consumer can target `net8.0` with a pre-C# 13 language version even when using a newer installed SDK, so compiler-priority-dependent overloads are not a safe public API foundation.

## Binary Compatibility

A `net8.0` consumer was compiled against a baseline assembly containing only the released legacy signature. Without recompiling the consumer, the assembly was replaced by a build that retained that exact signature and added the required package-owned overload.

The consumer ran successfully and resolved both previously compiled calls to the retained legacy method. This confirms the expected extend-only binary behavior: adding the new overload is safe when the existing declaring type and method signature remain unchanged.

## Initial Recommendation

Retain the released overload unchanged and do not mark the whole method obsolete in 13.x:

```csharp
public static IResourceBuilder<ILocalStackResource>? AddLocalStack(
    this IDistributedApplicationBuilder builder,
    string name = "localstack",
    ILocalStackOptions? localStackOptions = null,
    IAWSSDKConfig? awsConfig = null,
    Action<LocalStackContainerOptions>? configureContainer = null);
```

Add an unambiguous overload whose package-owned options argument is required and first:

```csharp
public static IResourceBuilder<ILocalStackResource>? AddLocalStack(
    this IDistributedApplicationBuilder builder,
    LocalStackHostingOptions options,
    string name = "localstack",
    IAWSSDKConfig? awsConfig = null,
    Action<LocalStackContainerOptions>? configureContainer = null);
```

Both should delegate to one package-owned implementation path. The legacy overload should translate an explicitly supplied `ILocalStackOptions` into package-owned state; when that argument is null, it should resolve package-owned configuration directly.

The legacy overload cannot issue a warning only when its `localStackOptions` parameter is explicitly used. `[Obsolete]` applies to the whole method, so marking it would warn ordinary callers that never use Client-owned options. Keep the overload source-compatible in 13.x, document the parameter as legacy, and remove or replace the signature only in a future major version.

Other Client-owned entry points that can be deprecated without warning ordinary `AddLocalStack()` calls should still be marked obsolete, including `AddLocalStackOptions`, the public resource options property, and Client-options fluent extensions.

## Revised Decision After Argument-Order Spike

Further SDK 8/9/10 compilation showed that exact forwarding overloads can protect parameterless, name-only, and explicit name/AWS/container call paths while allowing the released Client-options overload to be marked obsolete. Named `awsConfig:` or `configureContainer:` calls that omit `name` still bind the released overload and receive CS0618.

The approved 13.x family is:

```csharp
AddLocalStack();

AddLocalStack(string name);

AddLocalStack(
    string name,
    IAWSSDKConfig? awsConfig,
    Action<LocalStackContainerOptions>? configureContainer);

AddLocalStack(
    string name,
    IAWSSDKConfig? awsConfig,
    Action<LocalStackHostingOptions> configureOptions,
    Action<LocalStackContainerOptions>? configureContainer = null);

[Obsolete("Use a package-owned AddLocalStack overload.")]
AddLocalStack(
    string name = "localstack",
    ILocalStackOptions? localStackOptions = null,
    IAWSSDKConfig? awsConfig = null,
    Action<LocalStackContainerOptions>? configureContainer = null);
```

The argument order is `name`, `awsConfig`, hosting callback, then container callback. The callbacks are adjacent in the explicit configuration shape.

The two longer package-owned overloads are intentionally separate during 13.x. A single overload with optional `configureOptions` was compiled and produced CS0121 when the callback was omitted because both it and the released legacy overload remained applicable. `[Obsolete]` does not influence overload selection. The no-hosting-callback overload provides an exact better candidate and delegates to the same internal implementation.

After the legacy overload is removed in a future major version, the forwarding family can be reconsidered and the package-owned shape can use optional parameters without colliding with Client options.

## Rejected Alternatives

- A second all-optional overload: ambiguous for the dominant call shapes.
- `OverloadResolutionPriorityAttribute`: fails SDK 8 consumer compatibility.
- A forwarding-overload family for every existing named-argument combination: technically possible but creates a large, fragile API solely to route around one legacy optional parameter.
- A renamed `AddLocalStackWithOptions` method: unambiguous but unnecessarily abandons Aspire's `AddX` convention.
- Obsoleting the entire released method without the approved exact forwarding overloads: produces warnings for every normal call shape rather than protecting parameterless, name-only, and explicit name/AWS/container calls.

## Remaining Design Work

- Confirm the exact owned property and fluent method names.
- Define validation behavior and final obsolete messages.
