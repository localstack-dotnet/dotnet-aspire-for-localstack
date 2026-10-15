<!--
Thank you for contributing to .NET Aspire LocalStack integration!
Please fill out this template to help us review your pull request.
-->

## 📝 Description

**What does this PR do?**
Provide a clear and concise description of the changes.

**Related Issue(s):**

- Fixes #(issue number)
- Closes #(issue number)
- Related to #(issue number)

## 🔄 Type of Change

- [ ] 🐛 Bug fix (non-breaking change that fixes an issue)
- [ ] ✨ New feature (non-breaking change that adds functionality)
- [ ] 💥 Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] 📚 Documentation update
- [ ] 🧹 Code cleanup/refactoring
- [ ] ⚡ Performance improvement
- [ ] 🧪 Test improvements

## 🎯 Aspire Compatibility

- [ ] Compatible with .NET Aspire 13.x
- [ ] Supports .NET 8.0, .NET 9.0 and .NET 10.0
- [ ] Follows Aspire.Hosting.AWS patterns
- [ ] Works with existing AWS integrations

## 🧪 Testing

**How has this been tested?**

- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing with playground examples
- [ ] Tested with LocalStack container
- [ ] Tested across multiple .NET versions
- [ ] Tested auto-configure (`UseLocalStack()`)
- [ ] Tested manual configuration (`WithReference()`)

**Test Environment:**

- LocalStack Version:
- .NET Aspire Version:
- .NET Versions Tested:
- Operating Systems:

## 📚 Documentation

- [ ] Code is self-documenting with clear naming
- [ ] XML documentation comments added/updated
- [ ] README.md updated (if needed)
- [ ] Playground examples updated (if needed)
- [ ] Breaking changes documented

## ✅ Code Quality Checklist

- [ ] Code follows project coding standards
- [ ] No new analyzer warnings introduced
- [ ] All tests pass locally
- [ ] No merge conflicts
- [ ] Branch is up to date with target branch
- [ ] Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/)

## 🔍 Additional Notes

**Breaking Changes:**
If this is a breaking change, describe the impact and migration path for users.

**Performance Impact:**
Describe any performance implications of these changes.

**Dependencies:**
List any new dependencies or version changes.

## 🎯 Reviewer Focus Areas

**Please pay special attention to:**

- [ ] Security implications
- [ ] Performance impact
- [ ] Breaking changes
- [ ] Test coverage
- [ ] Documentation completeness
- [ ] Aspire integration patterns
- [ ] LocalStack compatibility

## 📸 Screenshots/Examples

If applicable, add screenshots or code examples showing the changes in action.

```csharp
// Example usage in AppHost
var builder = DistributedApplication.CreateBuilder(args);

var localstack = builder.AddLocalStack();
// Your new feature usage here

builder.Build().Run();
```

---

By submitting this pull request, I confirm that:

- [ ] I have read and agree to the project's [Code of Conduct](CODE_OF_CONDUCT.md)
- [ ] I have read the [contribution licensing terms](CONTRIBUTING.md) (inbound=outbound MIT, no CLA to sign)
- [ ] My contribution is licensed under the same terms as the project (MIT License)
