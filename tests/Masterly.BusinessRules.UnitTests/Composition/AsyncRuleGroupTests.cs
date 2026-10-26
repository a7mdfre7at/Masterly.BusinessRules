namespace Masterly.BusinessRules.UnitTests;

public class AsyncRuleGroupTests
{
    private const string GroupCode = "GROUP.BROKEN";
    private const string GroupMessage = "The group is broken";
    private const string FlagKey = "flag";
    private static readonly TimeSpan ParallelTimeout = TimeSpan.FromSeconds(10);

    // ---------------------------------------------------------------------
    // Truth tables (three members; true = broken)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, true, true)]
    public async Task All_IsBrokenAsync_WhenAnyMemberIsBroken(bool first, bool second, bool third, bool expectedBroken)
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, CreateRule("A", first), CreateRule("B", second), CreateRule("C", third));

        Assert.Equal(expectedBroken, await group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, true)]
    public async Task Any_IsBrokenAsync_OnlyWhenEveryMemberIsBroken(bool first, bool second, bool third, bool expectedBroken)
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.Any(GroupCode, GroupMessage, CreateRule("A", first), CreateRule("B", second), CreateRule("C", third));

        Assert.Equal(expectedBroken, await group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, true)]
    public async Task AtLeast_IsBrokenAsync_WhenFewerThanThresholdMembersPass(bool first, bool second, bool third, bool expectedBroken)
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.AtLeast(2, GroupCode, GroupMessage, CreateRule("A", first), CreateRule("B", second), CreateRule("C", third));

        Assert.Equal(expectedBroken, await group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, true)]
    public async Task Exactly_IsBrokenAsync_UnlessExactlyThresholdMembersPass(bool first, bool second, bool third, bool expectedBroken)
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.Exactly(1, GroupCode, GroupMessage, CreateRule("A", first), CreateRule("B", second), CreateRule("C", third));

        Assert.Equal(expectedBroken, await group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, true)]
    public async Task All_WithParallelEvaluation_MatchesSequentialTruthTable(bool first, bool second, bool third, bool expectedBroken)
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, CreateRule("A", first), CreateRule("B", second), CreateRule("C", third))
            .WithParallelEvaluation();

        Assert.Equal(expectedBroken, await group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public async Task Any_WithParallelEvaluation_MatchesSequentialTruthTable(bool first, bool second, bool third, bool expectedBroken)
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.Any(GroupCode, GroupMessage, CreateRule("A", first), CreateRule("B", second), CreateRule("C", third))
            .WithParallelEvaluation();

        Assert.Equal(expectedBroken, await group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));
    }

    // ---------------------------------------------------------------------
    // Metadata and structure
    // ---------------------------------------------------------------------

    [Fact]
    public void All_ExposesMetadataOperatorAndOperands()
    {
        IAsyncBusinessRule a = RuleTestHelper.CreatePassingAsyncRule("A");
        IAsyncBusinessRule b = RuleTestHelper.CreatePassingAsyncRule("B");

        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, a, b);

        Assert.Equal(GroupCode, group.Code);
        Assert.Equal(GroupMessage, group.Message);
        Assert.Equal(RuleSeverity.Error, group.Severity);
        Assert.Equal("GroupAsyncBusinessRule", group.Name);
        Assert.Equal(RuleGroupQuantifier.All, group.Quantifier);
        Assert.Equal(RuleOperator.All, group.Operator);
        Assert.Equal(2, group.Threshold);
        Assert.False(group.ShortCircuit);
        Assert.False(group.ParallelEvaluation);
        Assert.Equal(new[] { a, b }, group.Operands);
        Assert.IsAssignableFrom<IComposedAsyncBusinessRule>(group);
    }

    [Fact]
    public void AtLeast_WithEnumerableMembers_ExposesRequestedThreshold()
    {
        List<IAsyncBusinessRule> members = new List<IAsyncBusinessRule>
        {
            RuleTestHelper.CreatePassingAsyncRule("A"),
            RuleTestHelper.CreatePassingAsyncRule("B")
        };

        GroupAsyncBusinessRule group = AsyncRuleGroup.AtLeast(1, GroupCode, GroupMessage, members);

        Assert.Equal(1, group.Threshold);
        Assert.Equal(RuleOperator.AtLeast, group.Operator);
        Assert.Equal(2, group.Operands.Count);
    }

    // ---------------------------------------------------------------------
    // Immutability and option exclusivity
    // ---------------------------------------------------------------------

    [Fact]
    public void WithShortCircuit_ReturnsNewInstance_OriginalUnchanged()
    {
        GroupAsyncBusinessRule original = AsyncRuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A"));

        GroupAsyncBusinessRule shortCircuiting = original.WithShortCircuit();

        Assert.NotSame(original, shortCircuiting);
        Assert.False(original.ShortCircuit);
        Assert.True(shortCircuiting.ShortCircuit);
    }

    [Fact]
    public void WithParallelEvaluation_ReturnsNewInstance_OriginalUnchanged()
    {
        GroupAsyncBusinessRule original = AsyncRuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A"));

        GroupAsyncBusinessRule parallel = original.WithParallelEvaluation();

        Assert.NotSame(original, parallel);
        Assert.False(original.ParallelEvaluation);
        Assert.True(parallel.ParallelEvaluation);
    }

    [Fact]
    public void WithSeverity_ReturnsNewInstance_OriginalUnchanged()
    {
        GroupAsyncBusinessRule original = AsyncRuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A"));

        GroupAsyncBusinessRule warning = original.WithSeverity(RuleSeverity.Warning);

        Assert.Equal(RuleSeverity.Error, original.Severity);
        Assert.Equal(RuleSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void WithParallelEvaluation_AfterWithShortCircuit_ThrowsInvalidOperationException()
    {
        GroupAsyncBusinessRule shortCircuiting = AsyncRuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A")).WithShortCircuit();

        Assert.Throws<InvalidOperationException>(() => shortCircuiting.WithParallelEvaluation());
    }

    [Fact]
    public void WithShortCircuit_AfterWithParallelEvaluation_ThrowsInvalidOperationException()
    {
        GroupAsyncBusinessRule parallel = AsyncRuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A")).WithParallelEvaluation();

        Assert.Throws<InvalidOperationException>(() => parallel.WithShortCircuit());
    }

    // ---------------------------------------------------------------------
    // Short-circuit and parallel behaviour
    // ---------------------------------------------------------------------

    [Fact]
    public async Task All_WithShortCircuit_StopsAtFirstBrokenMember()
    {
        int evaluations = 0;
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage,
            CreateCountingRule("A", broken: true, () => evaluations++),
            CreateCountingRule("B", broken: true, () => evaluations++),
            CreateCountingRule("C", broken: true, () => evaluations++)).WithShortCircuit();

        bool isBroken = await group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(isBroken);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public async Task Any_WithShortCircuit_StopsAtFirstPassingMember()
    {
        int evaluations = 0;
        GroupAsyncBusinessRule group = AsyncRuleGroup.Any(GroupCode, GroupMessage,
            CreateCountingRule("A", broken: true, () => evaluations++),
            CreateCountingRule("B", broken: false, () => evaluations++),
            CreateCountingRule("C", broken: false, () => evaluations++)).WithShortCircuit();

        bool isBroken = await group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.False(isBroken);
        Assert.Equal(2, evaluations);
    }

    [Fact]
    public async Task Exactly_WithShortCircuit_StopsOncePassingCountExceedsThreshold()
    {
        int evaluations = 0;
        GroupAsyncBusinessRule group = AsyncRuleGroup.Exactly(1, GroupCode, GroupMessage,
            CreateCountingRule("A", broken: false, () => evaluations++),
            CreateCountingRule("B", broken: false, () => evaluations++),
            CreateCountingRule("C", broken: false, () => evaluations++)).WithShortCircuit();

        bool isBroken = await group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(isBroken);
        Assert.Equal(2, evaluations);
    }

    [Fact]
    public async Task Any_WithParallelEvaluation_EvaluatesEveryMember()
    {
        int evaluations = 0;
        GroupAsyncBusinessRule group = AsyncRuleGroup.Any(GroupCode, GroupMessage,
            CreateCountingRule("A", broken: false, () => Interlocked.Increment(ref evaluations)),
            CreateCountingRule("B", broken: false, () => Interlocked.Increment(ref evaluations)),
            CreateCountingRule("C", broken: true, () => Interlocked.Increment(ref evaluations))).WithParallelEvaluation();

        bool isBroken = await group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.False(isBroken);
        Assert.Equal(3, evaluations);
    }

    [Fact]
    public async Task All_WithParallelEvaluation_StartsMembersConcurrently()
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(ParallelTimeout);
        TaskCompletionSource<bool> secondStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IAsyncBusinessRule waitsForSecond = AsyncBusinessRuleBuilder.Create("A").WithMessage("A")
            .WhenAsync(async (ctx, ct) =>
            {
                await secondStarted.Task.WaitAsync(ct);
                return false;
            })
            .Build();
        IAsyncBusinessRule signalsStart = AsyncBusinessRuleBuilder.Create("B").WithMessage("B")
            .WhenAsync((ctx, ct) =>
            {
                secondStarted.TrySetResult(true);
                return Task.FromResult(false);
            })
            .Build();
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, waitsForSecond, signalsStart).WithParallelEvaluation();

        bool isBroken = await group.IsBrokenAsync(RuleTestHelper.CreateContext(), timeout.Token);

        Assert.False(isBroken);
    }

    [Fact]
    public async Task All_WithParallelEvaluation_WhenMemberFaults_PropagatesAfterAwaitingEveryMember()
    {
        int completed = 0;
        IAsyncBusinessRule faulting = AsyncBusinessRuleBuilder.Create("FAULT").WithMessage("fault")
            .WhenAsync((ctx, ct) => Task.FromException<bool>(new InvalidOperationException("boom")))
            .Build();
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage,
            faulting,
            CreateCountingRule("B", broken: false, () => Interlocked.Increment(ref completed)),
            CreateCountingRule("C", broken: false, () => Interlocked.Increment(ref completed))).WithParallelEvaluation();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => group.IsBrokenAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));

        Assert.Equal(2, completed);
    }

    // ---------------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task All_IsBrokenAsync_WithCancelledToken_ThrowsBeforeEvaluatingMembers()
    {
        int evaluations = 0;
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, CreateCountingRule("A", broken: true, () => evaluations++));
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => group.IsBrokenAsync(RuleTestHelper.CreateContext(), cancellation.Token));

        Assert.Equal(0, evaluations);
    }

    [Fact]
    public async Task All_WithParallelEvaluation_WithCancelledToken_Throws()
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A")).WithParallelEvaluation();
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => group.IsBrokenAsync(RuleTestHelper.CreateContext(), cancellation.Token));
    }

    [Fact]
    public async Task All_WhenMemberObservesCancellation_DoesNotPopulateCachedMember()
    {
        int evaluations = 0;
        IAsyncBusinessRule cancellable = AsyncBusinessRuleBuilder.Create("A").WithMessage("A")
            .WhenAsync((ctx, ct) =>
            {
                evaluations++;
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            })
            .Build();
        CachedAsyncBusinessRule cached = new CachedAsyncBusinessRule(cancellable, TimeSpan.FromMinutes(5));
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, cached);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(0);
        await Task.Delay(1, TestContext.Current.CancellationToken);
        BusinessRuleContext context = RuleTestHelper.CreateContext();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => group.IsBrokenAsync(context, cancellation.Token));
        await group.IsBrokenAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(1, evaluations);
    }

    // ---------------------------------------------------------------------
    // Context, results and checker integration
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task All_IsBrokenAsync_ForwardsContextToMembers(bool flag, bool expectedBroken)
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, CreateContextReadingRule("A"), CreateContextReadingRule("B"));

        bool actualBroken = await group.IsBrokenAsync(RuleTestHelper.CreateContext((FlagKey, flag)), TestContext.Current.CancellationToken);

        Assert.Equal(expectedBroken, actualBroken);
    }

    [Fact]
    public async Task CheckAsync_WhenBroken_ThrowsSingleResultWithBrokenMembersInDetails()
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage,
            RuleTestHelper.CreateBrokenAsyncRule("A"),
            RuleTestHelper.CreatePassingAsyncRule("B"),
            RuleTestHelper.CreateBrokenAsyncRule("C"));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => group.CheckAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));

        BusinessRuleResult result = Assert.Single(exception.BrokenRules);
        Assert.Equal(GroupCode, result.Code);
        Assert.Equal(new[] { "A", "C" }, result.Details.Select(detail => detail.Code));
    }

    [Fact]
    public async Task CheckAsync_WhenPassing_DoesNotThrow()
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.Any(GroupCode, GroupMessage, RuleTestHelper.CreateBrokenAsyncRule("A"), RuleTestHelper.CreatePassingAsyncRule("B"));

        await group.CheckAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPassing_ReturnsNull()
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A"));

        Assert.Null(await group.EvaluateAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WithParallelEvaluation_Details_KeepDeclarationOrder()
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage,
            RuleTestHelper.CreateBrokenAsyncRule("A"),
            RuleTestHelper.CreateBrokenAsyncRule("B"),
            RuleTestHelper.CreateBrokenAsyncRule("C")).WithParallelEvaluation();

        BusinessRuleResult? result = await group.EvaluateAsync(RuleTestHelper.CreateContext(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(new[] { "A", "B", "C" }, result.Details.Select(detail => detail.Code));
    }

    [Fact]
    public async Task AsyncBusinessRuleChecker_CheckAllAsync_SurfacesMemberResultsInDetails()
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.Any(GroupCode, GroupMessage, RuleTestHelper.CreateBrokenAsyncRule("A"), RuleTestHelper.CreateBrokenAsyncRule("B"));

        BusinessRuleValidationException exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => AsyncBusinessRuleChecker.CheckAllAsync(RuleTestHelper.CreateContext(), group));

        Assert.Equal(new[] { "A", "B" }, exception.Flatten().Select(leaf => leaf.Code));
    }

    [Fact]
    public async Task AsyncBusinessRuleChecker_EvaluateAllAsync_SurfacesMemberResultsInDetails()
    {
        GroupAsyncBusinessRule group = AsyncRuleGroup.All(GroupCode, GroupMessage, RuleTestHelper.CreateBrokenAsyncRule("A"));

        AsyncRuleEvaluationResult evaluation = await AsyncBusinessRuleChecker.EvaluateAllAsync(RuleTestHelper.CreateContext(), group);

        Assert.Equal("A", Assert.Single(Assert.Single(evaluation.BrokenRules).Details).Code);
    }

    // ---------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------

    [Fact]
    public void All_NullCode_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AsyncRuleGroup.All(null!, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A")));
    }

    [Fact]
    public void All_NullMessage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AsyncRuleGroup.All(GroupCode, null!, RuleTestHelper.CreatePassingAsyncRule("A")));
    }

    [Fact]
    public void All_NullMembers_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AsyncRuleGroup.All(GroupCode, GroupMessage, (IEnumerable<IAsyncBusinessRule>)null!));
    }

    [Fact]
    public void Any_EmptyMembers_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AsyncRuleGroup.Any(GroupCode, GroupMessage));
    }

    [Fact]
    public void Exactly_NullMember_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AsyncRuleGroup.Exactly(1, GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A"), null!));
    }

    [Fact]
    public void AtLeast_NegativeThreshold_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AsyncRuleGroup.AtLeast(-1, GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A")));
    }

    [Fact]
    public void Exactly_ThresholdAboveMemberCount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AsyncRuleGroup.Exactly(2, GroupCode, GroupMessage, RuleTestHelper.CreatePassingAsyncRule("A")));
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static IAsyncBusinessRule CreateRule(string code, bool broken)
    {
        return broken ? RuleTestHelper.CreateBrokenAsyncRule(code) : RuleTestHelper.CreatePassingAsyncRule(code);
    }

    private static IAsyncBusinessRule CreateCountingRule(string code, bool broken, Action onEvaluated)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync((ctx, ct) =>
            {
                onEvaluated();
                return Task.FromResult(broken);
            })
            .Build();
    }

    private static IAsyncBusinessRule CreateContextReadingRule(string code)
    {
        return AsyncBusinessRuleBuilder.Create(code)
            .WithMessage(code)
            .WhenAsync((ctx, ct) => Task.FromResult(ctx.Get<bool>(FlagKey)))
            .Build();
    }
}
