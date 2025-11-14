namespace Tests.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class UnitTestAttribute : CategoryAttribute
{
    public UnitTestAttribute() : base("UnitTest")
    {
    }
}
