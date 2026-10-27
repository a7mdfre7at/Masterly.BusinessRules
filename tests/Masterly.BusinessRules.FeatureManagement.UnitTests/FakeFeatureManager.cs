using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Masterly.BusinessRules.FeatureManagement.UnitTests;

/// <summary>
/// An in-memory <see cref="IFeatureManager"/> / <see cref="IVariantFeatureManager"/> that records every call so tests
/// can assert which Microsoft.FeatureManagement API the adapter used and what targeting context it passed.
/// </summary>
public sealed class FakeFeatureManager : IFeatureManager, IVariantFeatureManager
{
    private readonly Dictionary<string, bool> _enabled = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Variant> _variants = new(StringComparer.Ordinal);

    public List<string> IsEnabledCalls { get; } = new();

    public List<string> GetVariantCalls { get; } = new();

    public ITargetingContext? LastTargetingContext { get; private set; }

    public object? LastEnabledContext { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public FakeFeatureManager Enable(string flag)
    {
        _enabled[flag] = true;
        return this;
    }

    public FakeFeatureManager Disable(string flag)
    {
        _enabled[flag] = false;
        return this;
    }

    public FakeFeatureManager WithVariant(string flag, Variant variant)
    {
        _variants[flag] = variant;
        return this;
    }

    public FakeFeatureManager WithVariant(string flag, string variantName)
    {
        return WithVariant(flag, Variants.Named(variantName));
    }

    // IFeatureManager

    public IAsyncEnumerable<string> GetFeatureNamesAsync()
    {
        return Enumerate(_enabled.Keys);
    }

    public Task<bool> IsEnabledAsync(string feature)
    {
        IsEnabledCalls.Add(feature);
        LastEnabledContext = null;
        return Task.FromResult(IsEnabled(feature));
    }

    public Task<bool> IsEnabledAsync<TContext>(string feature, TContext context)
    {
        IsEnabledCalls.Add(feature);
        LastEnabledContext = context;
        LastTargetingContext = context as ITargetingContext;
        return Task.FromResult(IsEnabled(feature));
    }

    // IVariantFeatureManager

    public IAsyncEnumerable<string> GetFeatureNamesAsync(CancellationToken cancellationToken = default)
    {
        return Enumerate(_enabled.Keys);
    }

    public ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken = default)
    {
        IsEnabledCalls.Add(feature);
        LastEnabledContext = null;
        LastCancellationToken = cancellationToken;
        return new ValueTask<bool>(IsEnabled(feature));
    }

    public ValueTask<bool> IsEnabledAsync<TContext>(string feature, TContext context, CancellationToken cancellationToken = default)
    {
        IsEnabledCalls.Add(feature);
        LastEnabledContext = context;
        LastTargetingContext = context as ITargetingContext;
        LastCancellationToken = cancellationToken;
        return new ValueTask<bool>(IsEnabled(feature));
    }

    public ValueTask<Variant> GetVariantAsync(string feature, CancellationToken cancellationToken = default)
    {
        GetVariantCalls.Add(feature);
        LastTargetingContext = null;
        LastCancellationToken = cancellationToken;
        return new ValueTask<Variant>(FindVariant(feature));
    }

    public ValueTask<Variant> GetVariantAsync(string feature, ITargetingContext context, CancellationToken cancellationToken = default)
    {
        GetVariantCalls.Add(feature);
        LastTargetingContext = context;
        LastCancellationToken = cancellationToken;
        return new ValueTask<Variant>(FindVariant(feature));
    }

    private static async IAsyncEnumerable<string> Enumerate(IEnumerable<string> items)
    {
        foreach (string item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    private bool IsEnabled(string feature)
    {
        return _enabled.TryGetValue(feature, out bool enabled) && enabled;
    }

    private Variant FindVariant(string feature)
    {
        // Microsoft.FeatureManagement is nullable-oblivious and returns null for a flag without variants.
        return _variants.TryGetValue(feature, out Variant? variant) ? variant : null!;
    }
}

/// <summary>
/// Builds <see cref="Variant"/> instances the way Microsoft.FeatureManagement would hand them out.
/// </summary>
public static class Variants
{
    private const string SectionName = "variant";

    public static Variant Named(string name)
    {
        return new Variant { Name = name };
    }

    public static Variant WithConfiguration(string name, params (string key, string value)[] entries)
    {
        Dictionary<string, string?> data = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string key, string value) in entries)
        {
            data[$"{SectionName}:{key}"] = value;
        }

        return new Variant { Name = name, Configuration = BuildSection(data) };
    }

    public static Variant WithScalarConfiguration(string name, string value)
    {
        Dictionary<string, string?> data = new(StringComparer.OrdinalIgnoreCase) { [SectionName] = value };
        return new Variant { Name = name, Configuration = BuildSection(data) };
    }

    public static IConfigurationSection EmptySection()
    {
        return BuildSection(new Dictionary<string, string?>());
    }

    private static IConfigurationSection BuildSection(Dictionary<string, string?> data)
    {
        IConfigurationRoot root = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        return root.GetSection(SectionName);
    }
}
