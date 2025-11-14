namespace Tests.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class IntegrationTestAttribute : CategoryAttribute
{
    public IntegrationTestAttribute() : base("IntegrationTest")
    {
    }
}

