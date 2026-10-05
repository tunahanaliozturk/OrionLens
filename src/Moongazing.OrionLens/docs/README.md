# OrionLens

Ambient correlation context for .NET: one correlation id and a little baggage, set at the edge of a request, flowing through every `await` and onto every downstream HTTP call, without threading an id through your method signatures.

![One request through OrionLens: the middleware extracts the headers, begins the OrionContext scope and echoes the id; the endpoint reads OrionContext.Current; CorrelationPropagationHandler injects the id and baggage into the downstream call](https://raw.githubusercontent.com/tunahanaliozturk/OrionLens/main/docs/diagrams/request-flow.png)

## Install

    dotnet add package OrionLens

Targets `net8.0`, `net9.0` and `net10.0`. References only the ASP.NET Core shared framework; no third-party dependencies.

## Quick start

```csharp
using Moongazing.OrionLens;
using Moongazing.OrionLens.Context;
using Moongazing.OrionLens.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOrionLens(o => o.CorrelationHeader = "X-Correlation-ID");

// outbound: every call through this client carries the id and baggage
builder.Services.AddHttpClient("downstream")
    .AddHttpMessageHandler<CorrelationPropagationHandler>();

var app = builder.Build();
app.UseOrionLens();   // early, before logging and downstream calls

app.MapGet("/orders/{id}", (string id, ILogger<Program> logger) =>
{
    logger.LogInformation("Order {OrderId}, correlation {Correlation}",
        id, OrionContext.Current?.CorrelationId);
    return Results.Ok();
});

app.Run();
```

Add baggage for the rest of the flow; the context is immutable, so the parent is restored on dispose:

```csharp
using (OrionContext.BeginScope(OrionContext.Current!.WithBaggage("tenant", tenantId)))
{
    await next();   // everything in here sees OrionContext.Current!.GetBaggage("tenant")
}
```

## What propagates

| Field | Header (default) | Notes |
|-------|------------------|-------|
| Correlation id | `X-Correlation-ID` | Read inbound, minted if absent (`GenerateIdWhenMissing`, default true), echoed on the response (`WriteResponseHeader`, default true) |
| Baggage | `X-Orion-Baggage` | `key=value` pairs joined by commas, each part percent-encoded |
| Trace context | `traceparent` | Only with `UseTraceContext` (default false) |
| W3C baggage | `baggage` | Only with `UseW3CBaggage` (default false) |

`CorrelationPropagationHandler` injects only when `OrionContext.Current` is set. Without ASP.NET, `CorrelationPropagator.Extract(getHeader, options)` and `CorrelationPropagator.Inject(context, setHeader, options)` work against any header getter and setter, so a message consumer or a background job uses the same logic.

## Behaviour

- **Baggage policy**: `MaxBaggageCount`, `MaxBaggageBytes` (both `null`, no cap, by default), `NonPropagatingBaggageKeys` (read inbound, never written) and `SampledOnlyBaggageKeys` (written only when `CorrelationContext.IsSampled`). A breach drops pairs; it never throws.
- **Activity integration**: `AlignWithActivity` (default false) seeds an absent id from the current W3C `Activity`, and the middleware tags that activity with the id (`ActivityCorrelationTag`, default `orion.correlation_id`). It never starts a span.
- **Logging**: `logger.BeginCorrelationScope()` (namespace `Moongazing.OrionLens.Logging`) opens a log scope carrying `CorrelationId`; the `BeginCorrelationScope(options)` overload adds the `LoggedBaggageKeys` you opt in. Both return null when no context is set.
- **Validation**: `AddOrionLens` validates the options at registration, so an empty header name fails at startup.
- **AOT**: a NativeAOT publish of the context is checked in CI with zero trim/AOT warnings.

## Related packages

- `OrionGuard` - guard clauses and validation from the same Orion family.
- `OrionAudit` - automatic EF Core change-audit trail from the same Orion family.

## Links

- Documentation and full README: https://github.com/tunahanaliozturk/OrionLens
- Changelog: https://github.com/tunahanaliozturk/OrionLens/blob/main/CHANGELOG.md
- License: MIT
