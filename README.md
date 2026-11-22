# Passenger Manifest Processing System

A .NET portfolio reference project demonstrating an event-driven passenger-manifest workflow on AWS.

## Architecture

```text
EventBridge Scheduler
        |
        v
   AWS Lambda (.NET)
        |
        +--> Manifest data source
        +--> Manifest generator
        +--> DynamoDB processing ledger (idempotency)
        +--> Amazon SES delivery
        |
        v
CloudWatch Logs, Metrics and Alarms --> SNS notifications
```

## Engineering concepts
- Scheduled, serverless processing with AWS Lambda and EventBridge Scheduler.
- Duplicate-invocation protection through a DynamoDB processing ledger.
- Separation of workflow/domain logic from cloud-specific adapters.
- Secure configuration through Secrets Manager or Systems Manager Parameter Store.
- Least-privilege IAM, encryption, structured logging and operational alarms.
- Failure handling and cancellation-aware asynchronous .NET code.

## Repository structure

```text
src/
  Manifest.Function/
    Models/
    Services/
docs/
  architecture.md
```

## Local development

Requires the .NET 8 SDK. From `src/Manifest.Function`:

```bash
dotnet build
```

The current implementation is a framework-neutral workflow starter. AWS Lambda hosting, AWS SDK adapters, Terraform infrastructure and automated tests are not yet included.

## Security and provenance

This is an independently written, sanitized portfolio project based on general engineering concepts. It does not contain employer source code, production identifiers, passenger information, secrets or credentials. Do not add any confidential work material.

## Interview discussion points
1. How to build stable idempotency keys and handle concurrent invocations.
2. Why sending an email and recording completion cannot by themselves guarantee exactly-once delivery.
3. How to isolate AWS SDK adapters behind interfaces.
4. How to secure secrets and apply least-privilege IAM.
5. How to monitor failures, retries, throttling and execution duration.
