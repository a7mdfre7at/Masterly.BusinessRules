namespace Masterly.BusinessRules.Diagnostics.UnitTests.Support;

/// <summary>
/// Test collections of this project. Every test that starts or listens to activities of <see cref="BusinessRulesActivitySource"/>
/// runs in <see cref="Activities"/>, serially, because an <see cref="System.Diagnostics.ActivityListener"/> sees every span of
/// the process and <see cref="System.Diagnostics.Activity.Current"/> is ambient state.
/// </summary>
public static class DiagnosticsCollections
{
    public const string Activities = "Diagnostics activities";
}

[CollectionDefinition(DiagnosticsCollections.Activities, DisableParallelization = true)]
public sealed class ActivitiesCollection
{
}
