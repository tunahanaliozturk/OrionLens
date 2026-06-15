# OrionLens

[![CI/CD](https://github.com/tunahanaliozturk/OrionLens/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/tunahanaliozturk/OrionLens/actions/workflows/ci-cd.yml)
[![NuGet](https://img.shields.io/nuget/v/OrionLens.svg)](https://www.nuget.org/packages/OrionLens/)

Ambient correlation context for .NET. One correlation id (and a little baggage) is established at
the edge of a request and flows through every `await` and across every downstream HTTP call, so a
single operation is traceable end to end without threading an id through every method signature.

Part of the **Orion** family. Usable entirely on its own.

## Why

When a request fans out across services, you want one id stitching the logs together. Passing it
explicitly everywhere is noise; stuffing it in a thread-local breaks across `await`. OrionLens uses
`AsyncLocal` so the context follows the logical flow, reads and writes it on the HTTP boundary for
you, and keeps the data model immutable so a nested scope can add baggage without disturbing its
parent.

## Install

```
dotnet add package OrionLens
```

## Quick start

Establish context on the way in, propagate it on the way out:

```csharp
builder.Services.AddOrionLens(o => o.CorrelationHeader = "X-Correlation-ID");

// outbound: attach the handler to any client that calls a downstream service
builder.Services.AddHttpClient("downstream")
    .AddHttpMessageHandler<CorrelationPropagationHandler>();

var app = builder.Build();
app.UseOrionLens();   // early, before logging and downstream calls
```

Read the context anywhere, with no parameter passing:

```csharp
var id = OrionContext.Current?.CorrelationId;
logger.LogInformation("Processing order {OrderId} for correlation {Correlation}", orderId, id);

var tenant = OrionContext.Current?.GetBaggage("tenant");
```

Add baggage for the rest of the flow:

```csharp
using (OrionContext.BeginScope(OrionContext.Current!.WithBaggage("tenant", tenantId)))
{
    await next();   // everything in here sees the tenant baggage
}
```

## What propagates

| Field | Header (default) | Notes |
|-------|------------------|-------|
| Correlation id | `X-Correlation-ID` | Read inbound, minted if absent, echoed on the response |
| Baggage | `X-Orion-Baggage` | `key=value` pairs joined by commas, each part percent-encoded |

The middleware extracts both from the request, makes them the ambient context, and writes the id
back on the response. The `CorrelationPropagationHandler` injects the current context into every
outbound request, so downstream services receive the same id and baggage.

## Without ASP.NET

The core (`CorrelationContext`, `OrionContext`, `CorrelationPropagator`) has no HTTP dependency.
`CorrelationPropagator.Extract` and `Inject` work against any header getter/setter, so you can
carry context across a message queue or a background job the same way.

## Design

- Multi-targets `net8.0`, `net9.0`, `net10.0`.
- `TreatWarningsAsErrors`, latest analyzers, nullable enabled.
- `CorrelationContext` is immutable; `WithBaggage` returns a new instance, so scopes nest cleanly.

## License

MIT.
