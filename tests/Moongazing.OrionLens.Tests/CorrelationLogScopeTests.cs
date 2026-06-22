namespace Moongazing.OrionLens.Tests;

using Microsoft.Extensions.Logging;

using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;
using Moongazing.OrionLens.Logging;

using Xunit;

public sealed class CorrelationLogScopeTests
{
    private static readonly string[] TenantKey = ["tenant"];
    private static readonly string[] JobKey = ["job"];

    [Fact]
    public void BeginCorrelationScope_with_no_ambient_context_returns_null()
    {
        var logger = new CapturingLogger();

        var scope = logger.BeginCorrelationScope();

        Assert.Null(scope);
        Assert.Empty(logger.Scopes);
    }

    [Fact]
    public void BeginCorrelationScope_places_the_correlation_id_in_the_scope()
    {
        var logger = new CapturingLogger();
        using (OrionContext.BeginScope(CorrelationContext.Create("id-42")))
        using (logger.BeginCorrelationScope())
        {
            var state = AsPairs(Assert.Single(logger.Scopes));
            Assert.Equal("id-42", Assert.Contains(CorrelationLogScope.CorrelationIdKey, state));
        }
    }

    [Fact]
    public void BeginCorrelationScope_includes_only_the_selected_baggage_keys()
    {
        var logger = new CapturingLogger();
        var options = new CorrelationOptions();
        options.LoggedBaggageKeys.Add("tenant");

        var context = CorrelationContext.Create("id-1")
            .WithBaggage("tenant", "acme")
            .WithBaggage("secret", "do-not-log");

        using (OrionContext.BeginScope(context))
        using (logger.BeginCorrelationScope(options))
        {
            var state = AsPairs(Assert.Single(logger.Scopes));

            Assert.Equal("id-1", Assert.Contains(CorrelationLogScope.CorrelationIdKey, state));
            Assert.Equal("acme", Assert.Contains("tenant", state));
            // A baggage key not opted in must never reach the logging scope.
            Assert.False(state.ContainsKey("secret"));
        }
    }

    [Fact]
    public void BeginCorrelationScope_skips_a_selected_key_that_is_absent_on_the_context()
    {
        var logger = new CapturingLogger();
        var options = new CorrelationOptions();
        options.LoggedBaggageKeys.Add("tenant");

        // Context has no "tenant" baggage; the key is selected but should be skipped, not emitted null.
        using (OrionContext.BeginScope(CorrelationContext.Create("id-1")))
        using (logger.BeginCorrelationScope(options))
        {
            var state = AsPairs(Assert.Single(logger.Scopes));
            Assert.False(state.ContainsKey("tenant"));
            Assert.Single(state); // correlation id only
        }
    }

    [Fact]
    public void BeginCorrelationScope_with_an_explicit_context_does_not_read_the_ambient()
    {
        var logger = new CapturingLogger();
        var context = CorrelationContext.Create("explicit-id").WithBaggage("job", "nightly");

        // No ambient scope established; the explicit overload must still enrich.
        using (logger.BeginCorrelationScope(context, JobKey))
        {
            var state = AsPairs(Assert.Single(logger.Scopes));
            Assert.Equal("explicit-id", Assert.Contains(CorrelationLogScope.CorrelationIdKey, state));
            Assert.Equal("nightly", Assert.Contains("job", state));
        }
    }

    [Fact]
    public void Scope_state_is_a_structured_key_value_list_and_renders_as_text()
    {
        var context = CorrelationContext.Create("id-1").WithBaggage("tenant", "acme");
        var scope = CorrelationLogScope.FromContext(context, TenantKey);

        // Structured providers read the scope as an indexed list of name/value pairs.
        Assert.Equal(2, scope.Count);
        Assert.Equal(CorrelationLogScope.CorrelationIdKey, scope[0].Key);
        Assert.Equal("id-1", scope[0].Value);

        // Text providers render the scope via ToString.
        var text = scope.ToString();
        Assert.Contains("CorrelationId:id-1", text, StringComparison.Ordinal);
        Assert.Contains("tenant:acme", text, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> AsPairs(object? scopeState)
    {
        var pairs = Assert.IsType<CorrelationLogScope>(scopeState);
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    /// <summary>
    /// A minimal <see cref="ILogger"/> that records the state of every scope opened on it, so a test
    /// can assert what the enrichment helper pushed without taking a real logging sink.
    /// </summary>
    private sealed class CapturingLogger : ILogger
    {
        public List<object?> Scopes { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            Scopes.Add(state);
            return new Marker();
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class Marker : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
