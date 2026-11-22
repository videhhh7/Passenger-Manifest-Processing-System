variable "aws_region" { type = string, default = "ap-south-1" }
variable "project_name" { type = string, default = "passenger-manifest-demo" }
variable "lambda_zip_path" { type = string, default = "../../artifacts/manifest-function.zip" }
variable "ses_from_email" { type = string, description = "Verified SES sender identity" }
variable "manifest_recipient_email" { type = string, description = "Test recipient; use only controlled addresses" }
