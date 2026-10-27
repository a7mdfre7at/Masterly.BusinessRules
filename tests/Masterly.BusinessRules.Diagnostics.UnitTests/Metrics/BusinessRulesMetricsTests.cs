using System.Diagnostics.Metrics;
using Masterly.BusinessRules.Diagnostics.UnitTests.Support;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Metrics;

public class BusinessRulesMetricsTests
{
    private const string Code = "ORDER.NO_ITEMS";

    [Fact]
    public void Default_Meter_IsNamedMasterlyBusinessRules()
    {
        Assert.Equal("Masterly.BusinessRules", BusinessRulesMetrics.MeterName);
        Assert.Equal(BusinessRulesMetrics.MeterName, BusinessRulesMetrics.Default.Meter.Name);
        Assert.Same(BusinessRulesMetrics.Default, BusinessRulesMetrics.Default);
    }

    [Fact]
    public void Constructor_NullMeter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BusinessRulesMetrics(null!));
    }

    [Fact]
    public void Constructor_Meter_CreatesTheThreeInstruments()
    {
        using Meter meter = new Meter($"{BusinessRulesMetrics.MeterName}.Tests.{Guid.NewGuid():N}");
        List<Instrument> published = new List<Instrument>();
        using MeterListener listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    published.Add(instrument);
                }
            }
        };
        listener.Start();

        BusinessRulesMetrics metrics = new BusinessRulesMetrics(meter);

        Assert.Same(meter, metrics.Meter);
        Assert.Equal(3, published.Count);
        Instrument evaluations = Assert.Single(published, i => i.Name == BusinessRulesMetrics.EvaluationsInstrumentName);
        Instrument duration = Assert.Single(published, i => i.Name == BusinessRulesMetrics.EvaluationDurationInstrumentName);
        Instrument batches = Assert.Single(published, i => i.Name == BusinessRulesMetrics.BatchesInstrumentName);
        Assert.IsType<Counter<long>>(evaluations);
        Assert.IsType<Histogram<double>>(duration);
        Assert.IsType<Counter<long>>(batches);
        Assert.Equal(BusinessRulesMetrics.DurationUnit, duration.Unit);
    }

    [Fact]
    public void InstrumentNames_MatchTheRoadmap()
    {
        Assert.Equal("business_rules.evaluations", BusinessRulesMetrics.EvaluationsInstrumentName);
        Assert.Equal("business_rules.evaluation.duration", BusinessRulesMetrics.EvaluationDurationInstrumentName);
        Assert.Equal("business_rules.batches", BusinessRulesMetrics.BatchesInstrumentName);
    }

    [Fact]
    public void RecordEvaluation_WithCodeAndDuration_TagsCounterAndRecordsHistogramWithoutCode()
    {
        using MetricsCapture capture = new MetricsCapture();
        IBusinessRule rule = TestRules.Sync(Code, true, RuleSeverity.Warning);

        capture.Metrics.RecordEvaluation(rule, Code, RuleOutcomeTags.Broken, TimeSpan.FromMilliseconds(12.5));

        Measurement evaluation = Assert.Single(capture.Evaluations);
        Assert.Equal(1, evaluation.Value);
        Assert.Equal(Code, evaluation[BusinessRulesMetrics.CodeTag]);
        Assert.Equal(TestRules.Category, evaluation[BusinessRulesMetrics.CategoryTag]);
        Assert.Equal(RuleSeverityTags.Warning, evaluation[BusinessRulesMetrics.SeverityTag]);
        Assert.Equal(RuleOutcomeTags.Broken, evaluation[BusinessRulesMetrics.OutcomeTag]);
        Assert.Equal(4, evaluation.Tags.Count);

        Measurement duration = Assert.Single(capture.Durations);
        Assert.Equal(12.5, duration.Value);
        Assert.Equal(RuleOutcomeTags.Broken, duration[BusinessRulesMetrics.OutcomeTag]);
        Assert.Single(duration.Tags);
    }

    [Fact]
    public void RecordEvaluation_WithoutCode_OmitsCodeTag()
    {
        using MetricsCapture capture = new MetricsCapture();

        capture.Metrics.RecordEvaluation(TestRules.Sync(Code, false), null, RuleOutcomeTags.Passed, TimeSpan.Zero);

        Measurement evaluation = Assert.Single(capture.Evaluations);
        Assert.False(evaluation.Tags.ContainsKey(BusinessRulesMetrics.CodeTag));
        Assert.Equal(3, evaluation.Tags.Count);
    }

    [Fact]
    public void RecordEvaluation_WithoutDuration_RecordsCounterOnly()
    {
        using MetricsCapture capture = new MetricsCapture();

        capture.Metrics.RecordEvaluation(TestRules.Sync(Code, false), Code, RuleOutcomeTags.Faulted, null);

        Assert.Single(capture.Evaluations);
        Assert.Empty(capture.Durations);
    }

    [Fact]
    public void RecordEvaluation_NullArguments_Throw()
    {
        using MetricsCapture capture = new MetricsCapture();

        Assert.Throws<ArgumentNullException>(() => capture.Metrics.RecordEvaluation(null!, Code, RuleOutcomeTags.Passed, TimeSpan.Zero));
        Assert.Throws<ArgumentNullException>(() => capture.Metrics.RecordEvaluation(TestRules.Sync(Code, false), Code, null!, TimeSpan.Zero));
        Assert.Empty(capture.Measurements);
    }

    [Fact]
    public void RecordBatch_Outcome_IncrementsBatchesCounter()
    {
        using MetricsCapture capture = new MetricsCapture();

        capture.Metrics.RecordBatch(RuleOutcomeTags.Broken);
        capture.Metrics.RecordBatch(RuleOutcomeTags.Passed);

        Assert.Equal(2, capture.Batches.Count);
        Assert.All(capture.Batches, batch => Assert.Equal(1, batch.Value));
        Assert.Equal(new[] { RuleOutcomeTags.Broken, RuleOutcomeTags.Passed }, capture.Batches.Select(batch => (string?)batch[BusinessRulesMetrics.OutcomeTag]));
    }

    [Fact]
    public void RecordBatch_NullOutcome_Throws()
    {
        using MetricsCapture capture = new MetricsCapture();

        Assert.Throws<ArgumentNullException>(() => capture.Metrics.RecordBatch(null!));
    }

    [Fact]
    public void RecordEvaluation_ThrowingMetadataGetters_DegradeInsteadOfThrowing()
    {
        using MetricsCapture capture = new MetricsCapture();

        capture.Metrics.RecordEvaluation(new ThrowingMetadataRule(), null, RuleOutcomeTags.Broken, TimeSpan.Zero);

        Measurement evaluation = Assert.Single(capture.Evaluations);
        Assert.Equal(string.Empty, evaluation[BusinessRulesMetrics.CategoryTag]);
        Assert.Equal(RuleSeverityTags.Error, evaluation[BusinessRulesMetrics.SeverityTag]);
    }

    private sealed class ThrowingMetadataRule : BaseBusinessRule
    {
        public override string Code => throw new InvalidOperationException("code");

        public override string Message => "message";

        public override string Category => throw new InvalidOperationException("category");

        public override RuleSeverity Severity => throw new InvalidOperationException("severity");

        public override bool IsBroken()
        {
            return true;
        }
    }
}
