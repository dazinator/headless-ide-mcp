namespace Tests.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class DocumentationAttribute : CategoryAttribute
{
    public DocumentationAttribute() : base("Documentation")
    {
    }
}

