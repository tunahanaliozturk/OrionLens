# Roadmap

Ideas under consideration for OrionLens. These are directions, not commitments, and nothing here has
a date. The current release (0.1.0) covers the ambient correlation context, the immutable data model,
HTTP-agnostic propagation, the ASP.NET Core middleware, and the outbound `HttpClient` handler. If an
item matters to your workload, open an issue and say so; real demand is what moves things up the
list.

## Ideas being considered

- **Diagnostics bridge.** Optionally tie the correlation id to `System.Diagnostics.Activity` so the
  id lines up with OpenTelemetry traces, without making OpenTelemetry a dependency of the core.
- **Logging enrichment helpers.** Small, opt-in helpers to push the current correlation id (and
  selected baggage) into a logging scope, so structured logs carry it without a manual
  `BeginScope` call at every log site.
- **More transports.** Worked examples and thin adapters for carrying context across common message
  brokers, building on the already transport-agnostic `CorrelationPropagator`.
- **Baggage policy.** Optional limits on baggage size and key count, and a way to mark certain keys
  as inbound-only or non-propagating, to keep headers small and avoid leaking internal data across a
  trust boundary.
- **Sampling and redaction hooks.** A way to transform or drop baggage on inject, for redacting
  sensitive values before they cross a service boundary.

## Out of scope (for now)

- Becoming a full tracing system. OrionLens is a correlation-context primitive; distributed tracing
  backends remain a separate concern that a diagnostics bridge would feed, not replace.
- Bundling a specific logging or telemetry vendor. Any enrichment work would stay opt-in and
  dependency-light.

Feedback and pull requests are welcome. See [CONTRIBUTING.md](../CONTRIBUTING.md).
