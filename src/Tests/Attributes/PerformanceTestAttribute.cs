namespace Tests.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PerformanceTestAttribute : CategoryAttribute
{
    public PerformanceTestAttribute() : base("Performance")
    {
    }
}

