# Architecture and workflow

## Processing flow
1. EventBridge Scheduler invokes the Lambda adapter.
2. The handler validates the request and derives a stable processing key from flight number and date.
3. The DynamoDB ledger conditionally claims the key so concurrent duplicate invocations do not both process.
4. A data-source adapter retrieves the records required for the manifest.
5. A generator creates the manifest document.
6. A delivery adapter sends the document using Amazon SES.
7. The ledger is marked complete and operational telemetry is emitted.

## Idempotency and failure handling
Use a deterministic key such as `FLIGHT123#2026-11-23`. A production DynamoDB implementation should use conditional writes and explicit states such as `IN_PROGRESS`, `COMPLETED` and `FAILED`.

There is a failure window between successfully sending an email and recording completion. A status flag alone cannot guarantee exactly-once delivery across DynamoDB and SES. Discuss an outbox/delivery ledger, provider idempotency where available, and reconciliation for this case.

## Security
- Retrieve secrets at runtime from Secrets Manager or Parameter Store SecureString.
- Encrypt data in transit and at rest.
- Scope IAM permissions to the exact resources and actions required.
- Never log passenger details, document contents, connection strings or tokens.
- Restrict SES identities and recipients by environment.
- Keep environment-specific configuration and credentials outside source control.

## Observability
Use structured, non-sensitive fields such as correlation ID, processing key, status, duration and error category. Monitor Lambda errors, throttles, duration, failed invocations and workflow failure counts. SNS can deliver alarm notifications.

## Test strategy
- Unit-test request validation, key generation and workflow state transitions.
- Test duplicate and concurrent invocation behavior.
- Mock data, document-generation, delivery and ledger adapters.
- Exercise timeout, SES rejection, conditional-write conflict and retry scenarios.
- Run integration tests only in a dedicated non-production AWS account.
