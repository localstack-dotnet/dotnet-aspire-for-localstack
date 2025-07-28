// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Originally copied from https://github.com/aws/integrations-on-dotnet-aspire-for-aws
// and adjusted for Aspire.Hosting.LocalStack. All rights reserved.

namespace LocalStack.Provisioning.Frontend.Models;

internal sealed class ChatMessage
{
    public string? Message { get; init; }

    public string? Recipient { get; init; }
}
