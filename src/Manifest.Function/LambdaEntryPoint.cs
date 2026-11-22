using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
namespace Manifest.Function;

public sealed class LambdaEntryPoint
{
    private readonly IAmazonDynamoDB _dynamo = new AmazonDynamoDBClient();
    private readonly IAmazonS3 _s3 = new AmazonS3Client();
    private readonly IAmazonSimpleEmailService _ses = new AmazonSimpleEmailServiceClient();

    public async Task<object> FunctionHandler(JsonElement input, ILambdaContext context)
    {
        var flight = input.TryGetProperty("flightNumber", out var f) ? f.GetString() : null;
        var date = input.TryGetProperty("flightDate", out var d) ? d.GetString() : null;
        if (string.IsNullOrWhiteSpace(flight) || !DateOnly.TryParse(date, out var flightDate))
            throw new ArgumentException("Provide flightNumber and flightDate (yyyy-MM-dd).");

        var table = Environment.GetEnvironmentVariable("LEDGER_TABLE")!;
        var bucket = Environment.GetEnvironmentVariable("MANIFEST_BUCKET")!;
        var key = $"{flight.Trim().ToUpperInvariant()}#{flightDate:yyyy-MM-dd}";
        try
        {
            await _dynamo.PutItemAsync(new PutItemRequest {
                TableName = table,
                Item = new Dictionary<string, AttributeValue> {
                    ["ProcessingKey"] = new(key),
                    ["Status"] = new("IN_PROGRESS"),
                    ["ExpiresAt"] = new(DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds().ToString())
                },
                ConditionExpression = "attribute_not_exists(ProcessingKey)"
            });
        }
        catch (ConditionalCheckFailedException)
        {
            return new { processingKey = key, status = "SKIPPED_DUPLICATE" };
        }

        try
        {
            // Synthetic data only. Replace this provider with an authorized source adapter.
            var manifest = new {
                flightNumber = flight,
                flightDate,
                generatedAtUtc = DateTimeOffset.UtcNow,
                passengers = new[] {
                    new { passengerReference = "DEMO-001", displayName = "Sample Passenger One" },
                    new { passengerReference = "DEMO-002", displayName = "Sample Passenger Two" }
                }
            };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = true });
            var objectKey = $"manifests/{flightDate:yyyy/MM/dd}/{Uri.EscapeDataString(flight)}.json";
            await _s3.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = objectKey, InputStream = new MemoryStream(bytes), ContentType = "application/json", ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256 });
            var from = Environment.GetEnvironmentVariable("SES_FROM")!;
            var to = Environment.GetEnvironmentVariable("SES_TO")!;
            await _ses.SendEmailAsync(new SendEmailRequest {
                Source = from,
                Destination = new Destination { ToAddresses = new List<string> { to } },
                Message = new Message {
                    Subject = new Content { Data = $"Demo manifest {flight} {flightDate:yyyy-MM-dd}" },
                    Body = new Body { Text = new Content { Data = $"A synthetic demo manifest was stored at s3://{bucket}/{objectKey}." } }
                }
            });
            await _dynamo.UpdateItemAsync(new UpdateItemRequest {
                TableName = table, Key = new Dictionary<string, AttributeValue> { ["ProcessingKey"] = new(key) },
                UpdateExpression = "SET #s = :done",
                ExpressionAttributeNames = new Dictionary<string,string> { ["#s"] = "Status" },
                ExpressionAttributeValues = new Dictionary<string,AttributeValue> { [":done"] = new("COMPLETED") }
            });
            context.Logger.LogLine($"Completed demo processing key {key}");
            return new { processingKey = key, status = "COMPLETED", objectKey };
        }
        catch
        {
            await _dynamo.UpdateItemAsync(new UpdateItemRequest {
                TableName = table, Key = new Dictionary<string, AttributeValue> { ["ProcessingKey"] = new(key) },
                UpdateExpression = "SET #s = :failed",
                ExpressionAttributeNames = new Dictionary<string,string> { ["#s"] = "Status" },
                ExpressionAttributeValues = new Dictionary<string,AttributeValue> { [":failed"] = new("FAILED") }
            });
            throw;
        }
    }
}
