namespace Tests.Attributes;

using Xunit.v3;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class CategoryAttribute : Attribute, ITraitAttribute
{
    public string Category { get; }

    public CategoryAttribute(string category)
    {
        Category = category;
    }

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
    {
        return new[] { new KeyValuePair<string, string>("Category", Category) };
    }
}
