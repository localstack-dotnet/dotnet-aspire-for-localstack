namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class ConstantsTests
{
    [Fact]
    public void DefaultContainerPort_Should_Be_4566()
    {
        Assert.Equal(4566, Constants.DefaultContainerPort);
    }

    [Fact]
    public void CloudFormationReferenceAnnotation_Should_Have_Correct_Type_Name()
    {
        Assert.Equal("Aspire.Hosting.AWS.CloudFormation.CloudFormationReferenceAnnotation", Constants.CloudFormationReferenceAnnotation);
    }
}
