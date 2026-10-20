<#
.SYNOPSIS
  Deploys StoreOps to AWS Elastic Beanstalk (.NET 8 on Amazon Linux 2023), no Docker required.
  Publishes the API, zips it with a Procfile, uploads to the EB S3 bucket, creates the application/environment on the
  first run (single instance, t3.micro, basic health) or updates it on later runs, waits, then smoke-tests the live URL.
.PREREQUISITES
  AWS CLI v2 configured with credentials for YOUR account (aws configure / aws sso login); .NET 8 SDK.
  IAM permissions to use Elastic Beanstalk, S3, and (first run only) create the aws-elasticbeanstalk-ec2-role.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy\aws\deploy-eb.ps1 -Region ap-south-1
  powershell -ExecutionPolicy Bypass -File deploy\aws\deploy-eb.ps1 -BundleOnly   # just build artifacts\storeops-eb.zip for console upload
#>
param(
    [string]$Region = 'ap-south-1',
    [string]$AppName = 'storeops',
    [string]$EnvName = 'storeops-dev',
    [string]$InstanceType = 't3.micro',
    [switch]$BundleOnly,
    [int]$SlaSweepSeconds = 10,
    [int]$SlaGraceMinutes = 0
)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $root
function Invoke-Aws { $out = & aws @args --region $Region --output json; if ($LASTEXITCODE -ne 0) { throw "aws $($args -join ' ') failed" }; if ($out) { $out | ConvertFrom-Json } }

# 1. Build the source bundle (runtime-dependent publish output + Procfile pinning Kestrel to the port nginx proxies to)
$publish = Join-Path $root 'artifacts\publish'
$zip = Join-Path $root 'artifacts\storeops-eb.zip'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Split-Path $zip) | Out-Null
dotnet publish src\StoreOps.Api\StoreOps.Api.csproj -c Release -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
Set-Content -Path (Join-Path $publish 'Procfile') -Value 'web: dotnet StoreOps.Api.dll --urls http://0.0.0.0:5000' -Encoding ascii -NoNewline
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip
Write-Host "Bundle: $zip" -ForegroundColor Green
if ($BundleOnly) { return }

# 2. Upload the bundle to the Elastic Beanstalk storage bucket
$version = 'v' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + (git rev-parse --short HEAD)
$bucket = (Invoke-Aws elasticbeanstalk create-storage-location).S3Bucket
$key = "$AppName/$version.zip"
aws s3 cp $zip "s3://$bucket/$key" --region $Region
if ($LASTEXITCODE -ne 0) { throw 's3 upload failed' }

# 3. Application + version
$apps = Invoke-Aws elasticbeanstalk describe-applications --application-names $AppName
if (-not $apps.Applications) { Invoke-Aws elasticbeanstalk create-application --application-name $AppName --description 'StoreOps capstone API' | Out-Null }
Invoke-Aws elasticbeanstalk create-application-version --application-name $AppName --version-label $version --source-bundle "S3Bucket=$bucket,S3Key=$key" | Out-Null

# 4. Environment: create once, update afterwards
$envs = Invoke-Aws elasticbeanstalk describe-environments --application-name $AppName --environment-names $EnvName --no-include-deleted
$live = @($envs.Environments | Where-Object { $_.Status -ne 'Terminated' })
if ($live.Count -eq 0) {
    # Instance profile required by EB on EC2 (the console creates it automatically; the CLI does not)
    $roleName = 'aws-elasticbeanstalk-ec2-role'
    & aws iam get-instance-profile --instance-profile-name $roleName 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Creating IAM role/instance profile $roleName" -ForegroundColor Yellow
        $trust = Join-Path $env:TEMP 'eb-trust.json'
        Set-Content $trust '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"ec2.amazonaws.com"},"Action":"sts:AssumeRole"}]}' -Encoding ascii
        aws iam create-role --role-name $roleName --assume-role-policy-document "file://$trust" | Out-Null
        aws iam attach-role-policy --role-name $roleName --policy-arn arn:aws:iam::aws:policy/AWSElasticBeanstalkWebTier | Out-Null
        aws iam create-instance-profile --instance-profile-name $roleName | Out-Null
        aws iam add-role-to-instance-profile --instance-profile-name $roleName --role-name $roleName | Out-Null
        Start-Sleep -Seconds 15
    }
    $stack = & aws elasticbeanstalk list-available-solution-stacks --region $Region --query "SolutionStacks[?contains(@, 'Amazon Linux 2023') && contains(@, '.NET 8')] | [0]" --output text
    if (-not $stack -or $stack -eq 'None') { throw "No '.NET 8 on Amazon Linux 2023' solution stack found in $Region" }
    Write-Host "Creating environment $EnvName on '$stack'" -ForegroundColor Yellow
    $options = @(
        "Namespace=aws:elasticbeanstalk:environment,OptionName=EnvironmentType,Value=SingleInstance",
        "Namespace=aws:ec2:instances,OptionName=InstanceTypes,Value=$InstanceType",
        "Namespace=aws:autoscaling:launchconfiguration,OptionName=IamInstanceProfile,Value=$roleName",
        "Namespace=aws:elasticbeanstalk:healthreporting:system,OptionName=SystemType,Value=basic",
        "Namespace=aws:elasticbeanstalk:application:environment,OptionName=ASPNETCORE_ENVIRONMENT,Value=Production",
        "Namespace=aws:elasticbeanstalk:application:environment,OptionName=PORT,Value=5000",
        "Namespace=aws:elasticbeanstalk:application:environment,OptionName=Sla__SweepIntervalSeconds,Value=$SlaSweepSeconds",
        "Namespace=aws:elasticbeanstalk:application:environment,OptionName=Sla__EscalationGracePeriodMinutes,Value=$SlaGraceMinutes"
    )
    Invoke-Aws elasticbeanstalk create-environment --application-name $AppName --environment-name $EnvName --solution-stack-name $stack --version-label $version --option-settings $options | Out-Null
    aws elasticbeanstalk wait environment-exists --application-name $AppName --environment-names $EnvName --region $Region
} else {
    Write-Host "Updating environment $EnvName to $version" -ForegroundColor Yellow
    Invoke-Aws elasticbeanstalk update-environment --environment-name $EnvName --version-label $version | Out-Null
    aws elasticbeanstalk wait environment-updated --application-name $AppName --environment-names $EnvName --region $Region
}

# 5. Live URL + smoke test
$envInfo = (Invoke-Aws elasticbeanstalk describe-environments --application-name $AppName --environment-names $EnvName).Environments[0]
$url = "http://$($envInfo.CNAME)"
Write-Host "Environment: $($envInfo.Status) / $($envInfo.Health)   URL: $url" -ForegroundColor Green
powershell -ExecutionPolicy Bypass -File (Join-Path $root 'scripts\demo.ps1') -BaseUrl $url
