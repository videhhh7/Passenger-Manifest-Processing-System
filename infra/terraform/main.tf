data "aws_caller_identity" "current" {}
resource "aws_s3_bucket" "manifests" { bucket = "${var.project_name}-${data.aws_caller_identity.current.account_id}" }
resource "aws_s3_bucket_public_access_block" "manifests" {
  bucket = aws_s3_bucket.manifests.id
  block_public_acls = true
  block_public_policy = true
  ignore_public_acls = true
  restrict_public_buckets = true
}
resource "aws_dynamodb_table" "ledger" {
  name = "${var.project_name}-ledger"
  billing_mode = "PAY_PER_REQUEST"
  hash_key = "ProcessingKey"
  attribute { name = "ProcessingKey" type = "S" }
  ttl { attribute_name = "ExpiresAt" enabled = true }
  server_side_encryption { enabled = true }
}
resource "aws_iam_role" "lambda" {
  name = "${var.project_name}-lambda-role"
  assume_role_policy = jsonencode({ Version = "2012-10-17", Statement = [{ Effect = "Allow", Principal = { Service = "lambda.amazonaws.com" }, Action = "sts:AssumeRole" }] })
}
resource "aws_iam_role_policy" "lambda" {
  role = aws_iam_role.lambda.id
  policy = jsonencode({
    Version = "2012-10-17",
    Statement = [
      { Effect = "Allow", Action = ["logs:CreateLogGroup","logs:CreateLogStream","logs:PutLogEvents"], Resource = "arn:aws:logs:${var.aws_region}:*:log-group:/aws/lambda/${var.project_name}:*" },
      { Effect = "Allow", Action = ["dynamodb:GetItem","dynamodb:PutItem","dynamodb:UpdateItem"], Resource = aws_dynamodb_table.ledger.arn },
      { Effect = "Allow", Action = ["s3:PutObject"], Resource = "${aws_s3_bucket.manifests.arn}/*" },
      { Effect = "Allow", Action = ["ses:SendEmail","ses:SendRawEmail"], Resource = "*" }
    ]
  })
}
resource "aws_lambda_function" "manifest" {
  function_name = var.project_name
  role = aws_iam_role.lambda.arn
  runtime = "dotnet8"
  handler = "Manifest.Function::Manifest.Function.LambdaEntryPoint::FunctionHandler"
  filename = var.lambda_zip_path
  source_code_hash = filebase64sha256(var.lambda_zip_path)
  timeout = 60
  memory_size = 512
  environment { variables = { LEDGER_TABLE = aws_dynamodb_table.ledger.name, MANIFEST_BUCKET = aws_s3_bucket.manifests.bucket, SES_FROM = var.ses_from_email, SES_TO = var.manifest_recipient_email } }
}
resource "aws_cloudwatch_event_rule" "schedule" {
  name = "${var.project_name}-schedule"
  schedule_expression = "rate(1 day)"
}
resource "aws_iam_role" "scheduler" {
  name = "${var.project_name}-scheduler-role"
  assume_role_policy = jsonencode({ Version = "2012-10-17", Statement = [{ Effect = "Allow", Principal = { Service = "events.amazonaws.com" }, Action = "sts:AssumeRole" }] })
}
resource "aws_iam_role_policy" "scheduler" {
  role = aws_iam_role.scheduler.id
  policy = jsonencode({ Version = "2012-10-17", Statement = [{ Effect = "Allow", Action = ["lambda:InvokeFunction"], Resource = aws_lambda_function.manifest.arn }] })
}
resource "aws_cloudwatch_event_target" "lambda" {
  rule = aws_cloudwatch_event_rule.schedule.name
  arn = aws_lambda_function.manifest.arn
  role_arn = aws_iam_role.scheduler.arn
}
resource "aws_lambda_permission" "events" {
  statement_id = "AllowEventBridgeInvoke"
  action = "lambda:InvokeFunction"
  function_name = aws_lambda_function.manifest.function_name
  principal = "events.amazonaws.com"
  source_arn = aws_cloudwatch_event_rule.schedule.arn
}
