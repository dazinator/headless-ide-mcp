namespace Tests.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class ExploratoryAttribute : CategoryAttribute
{
    public ExploratoryAttribute() : base("Exploratory")
    {
    }
}

