---
name: Bug report
about: Create a report to help us improve the .NET Aspire LocalStack integration
title: ''
labels: bug
assignees: ''

---

## 🐛 Bug Description

**What happened?**
A clear and concise description of the bug.

**What did you expect to happen?**
A clear and concise description of what you expected to happen.

## 🔄 Steps to Reproduce

1.
2.
3.
4.

**Minimal code example:**

```csharp
// Please provide a minimal AppHost example that reproduces the issue
var builder = DistributedApplication.CreateBuilder(args);

var localstack = builder.AddLocalStack();

// Add your code that reproduces the issue here

builder.Build().Run();
```

## 📋 Environment Information

**LocalStack.Aspire.Hosting Version:**

- Version: (e.g., 9.4.0)

**.NET Aspire Information:**

- Aspire Version: (e.g., 9.4.0)
- Aspire.Hosting.AWS Version: (e.g., 9.4.0)

**LocalStack Information:**

- LocalStack Version: (e.g., 4.6.0)
- LocalStack Image: (e.g., `localstack/localstack:4.6.0`)
- LocalStack Services Used: (e.g., S3, DynamoDB, SNS, CloudFormation, CDK)

**.NET Information:**

- .NET Version: (e.g., .NET 8, .NET 9)
- Operating System: (e.g., Windows 11, Ubuntu 22.04, macOS 14)
- IDE/Editor: (e.g., Visual Studio 2022, Rider, VS Code)

**Container Configuration (Optional):**

```csharp
// Your LocalStack container configuration
var localstack = builder.AddLocalStack(configureContainer: options => {
    options.LogLevel = LocalStackLogLevel.Debug;
    options.DebugLevel = 1;
});
```

**Aspire Configuration (Optional):**

```json
// Your appsettings.json LocalStack configuration
{
  "LocalStack": {
    "UseLocalStack": true,
    "Config": {
      "LocalStackHost": "localhost",
      "EdgePort": 4566
    }
  }
}
```

**Error Messages/Stack Traces:**

```text
Paste any error messages or stack traces here
```

**Screenshots:**
If applicable, add screenshots to help explain your problem.

**Additional Information:**
Add any other context about the problem here.

## ✅ Checklist

- [ ] I have searched existing issues to ensure this is not a duplicate
- [ ] I have provided all the requested information above
- [ ] I have tested this with the latest version of LocalStack.Client
- [ ] I have verified this issue occurs with LocalStack (not with real AWS services)
