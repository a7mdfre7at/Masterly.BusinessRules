using System.Reflection;

namespace Masterly.BusinessRules.FeatureManagement.UnitTests;

public class PackageScaffoldTests
{
    private const string CoreAssemblyName = "Masterly.BusinessRules";
    private const string PackageAssemblyName = "Masterly.BusinessRules.FeatureManagement";

    [Fact]
    public void CoreReference_WhenPackageIsReferenced_ResolvesIBusinessRule()
    {
        Type ruleInterface = typeof(IBusinessRule);

        string? assemblyName = ruleInterface.Assembly.GetName().Name;

        Assert.Equal(CoreAssemblyName, assemblyName);
    }

    [Fact]
    public void PackageAssembly_WhenLoadedByName_HasExpectedName()
    {
        AssemblyName requested = new AssemblyName(PackageAssemblyName);

        Assembly loaded = Assembly.Load(requested);

        Assert.Equal(PackageAssemblyName, loaded.GetName().Name);
    }
}
