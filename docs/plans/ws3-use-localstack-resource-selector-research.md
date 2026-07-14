# `UseLocalStack` Resource Selector Research

**Date**: July 10, 2026

**Pinned refs examined**:

- Aspire `v13.4.6` at `87fe259e4fc244c599019a7b1304c85a1488f248`
- AWS integration `release_2026-06-19` (`13.3.1`) at `b8f5c040b2a41a655ab9ae4d2c528803d5066ae6`

## Question

Should the canonical global auto-wiring API remain explicit:

```csharp
builder.UseLocalStack(localStack);
```

or should it discover LocalStack from the application model:

```csharp
builder.UseLocalStack();
```

## Findings

### The argument selects a resource; it is not a second options source

The current method reads configuration from `localStack.Resource.Options`; the argument identifies which concrete LocalStack resource supplies that state and endpoint. Passing the resource builder does not duplicate configuration.

The selected identity is used when wiring each destination:

- `LocalStackCloudFormationResourceExtensions.WithReference` adds `WaitFor(localStackBuilder)`, a `LocalStackEnabledAnnotation` containing that resource, and a reverse `LocalStackReferenceAnnotation` (`src/Aspire.Hosting.LocalStack/LocalStackCloudFormationResourceExtensions.cs:39-58`).
- `LocalStackProjectExtensions.WithReference` adds the same wait relationship, a named connection reference to that resource, and bidirectional annotations (`src/Aspire.Hosting.LocalStack/LocalStackProjectExtensions.cs:22-42`).
- `UseLocalStack` passes the selected builder into every generated `WithReference` call (`src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs:94-130`).

The package-owned options migration does not remove this identity requirement. It only changes the state stored on the selected resource from Client-owned options to package-owned hosting state.

### Repository history shows that resource scanning was already understood

`UseLocalStack` was introduced by commit `6a4b89c77ffb6f096cbb40a64965668cf6f88c60` on July 31, 2025.

That first implementation already scanned `builder.Resources` to:

- detect all `IStackResource` instances,
- iterate all `ICloudFormationTemplateResource` instances, and
- create resource builders with `builder.CreateResourceBuilder(resource)`.

It nevertheless required `IResourceBuilder<LocalStackResource> localStack` and used that exact builder in `WaitFor(localStack).WithLocalStack(localStack)`. Commit `93005394d1f05746e74cf326fdc377fa5079286c` retained the selector while expanding automatic resource detection and changing the parameter to `IResourceBuilder<ILocalStackResource>?`.

Therefore there is no evidence that the explicit argument exists because resource-model discovery was unknown. The source demonstrates the opposite. The commits do not record an explicit design rationale, so intentional resource selection is an inference from the implementation rather than a documented historical statement.

### Explicit selection preserves a useful call-order constraint

The expression below cannot be written until `AddLocalStack` has executed:

```csharp
var localStack = builder.AddLocalStack();
builder.UseLocalStack(localStack);
```

An argumentless builder-time scan loses that structural constraint:

```csharp
builder.UseLocalStack();
builder.AddLocalStack();
```

The no-argument implementation would have to choose between:

- silently doing nothing, which makes an ordering mistake indistinguishable from disabled LocalStack, or
- throwing when no resource exists, which would break the current disabled/no-op behavior unless additional registration state distinguished "disabled" from "not added".

Resolving the resource during `BeforeStartEvent` could remove this particular ordering problem, but it would not resolve multiple-instance ambiguity. The explicit overload can also move its AWS-resource scan to `BeforeStartEvent` while capturing the selected LocalStack identity, fixing late-added AWS resources without losing the selector.

### Multiple LocalStack resources are not documented, but the model permits them

The repository has no test, sample, or documentation for multiple LocalStack instances. However, `AddLocalStack` accepts an arbitrary resource name and does not enforce a singleton. Aspire resource references and endpoint expressions are instance-specific.

An argumentless API would therefore need a new policy:

- select the first resource, which is unsafe and order-dependent,
- require exactly one and throw for multiple resources, which narrows an existing structurally possible topology, or
- wire every LocalStack resource, which is semantically invalid because a destination cannot unambiguously target several LocalStack endpoints.

The explicit selector requires no new policy and leaves room for advanced isolation scenarios even though they are not currently documented.

### Pinned Aspire sources separate global processing from resource relationships

Aspire supports scanning the application model. For example, the AWS integration's `AWSBeforeStartEventHandler` processes all `IAWSResource` instances from the final model (`external/aws-integrations/release_2026-06-19/src/Aspire.Hosting.AWS/AWSBeforeStartEventHandler.cs:20-53`). `CreateResourceBuilder` is also a supported internal hosting pattern, including in wait/reference handling (`external/aspire/v13.4.6/src/Aspire.Hosting/ResourceBuilderExtensions.cs:2558-2594`).

Those facts establish that argumentless discovery is technically possible. They do not make it preferable for a relationship that must target a specific endpoint-bearing resource. First-party relationship APIs such as `WaitFor` and `WithReference` take explicit resource builders and record relationships to exact resource instances.

### ATS does not currently decide this question

The current package methods are not yet marked with `AspireExport`, and the exact WS10 export surface is not designed. Aspire's generators contain overload and options-name handling, but the examined source does not prove that these two proposed overloads would be invalid or require a particular obsolete strategy.

ATS therefore remains a later validation requirement, not a sound basis for choosing the WS3A selector API today.

## Recommendation

Keep the existing explicit method as the canonical API:

```csharp
var localStack = builder.AddLocalStack();
builder.UseLocalStack(localStack);
```

Do not obsolete it. Its parameter is package-owned (`ILocalStackResource`), carries resource identity rather than Client-owned configuration, preserves disabled/no-op behavior through the nullable handle, and avoids introducing singleton and discovery-order policies.

Do not add an argumentless overload in WS3A unless a concrete usability problem justifies it. It saves one local variable but introduces zero-resource and multiple-resource semantics that the package otherwise does not need to define.

The single source of truth should still be the LocalStack resource:

```text
AddLocalStack resolves package-owned state -> LocalStack resource stores that state
UseLocalStack(localStack) selects that resource -> wiring reads state and endpoint from it
```

When global AWS-resource detection moves to `BeforeStartEvent`, capture the explicit LocalStack resource and scan the final model there. That improves late-resource handling without changing the public selector contract.

## Residual Questions

- Should multiple `AddLocalStack` calls become an explicitly supported topology, or merely remain structurally possible?
- How should the explicit nullable resource handle be represented by future ATS/polyglot exports?
- Should calling `UseLocalStack` more than once with different LocalStack resources be rejected to prevent conflicting annotations and environment callbacks?
