output "lambda_name" { value = aws_lambda_function.manifest.function_name }
output "manifest_bucket" { value = aws_s3_bucket.manifests.bucket }
output "ledger_table" { value = aws_dynamodb_table.ledger.name }
