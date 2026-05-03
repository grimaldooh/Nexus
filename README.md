# Nexus

Backend service for insurance commission ingestion, sanitization, and audit workflows.

## Requirements
- .NET 9 SDK
- SQL Server

## Configuration

### Local Development Setup
1. Copy the example configuration as a template:
   ```bash
   cp src/Nexus.API/appsettings.example.json src/Nexus.API/appsettings.json
   ```

2. Initialize user secrets for sensitive configuration:
   ```bash
   cd src/Nexus.API
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:NexusDb" "Server=localhost;Database=NexusDb;User Id=sa;Password=YOUR_PASSWORD_HERE;TrustServerCertificate=True;"
   dotnet user-secrets set "Security:ApiKey" "your-secret-api-key"
   dotnet user-secrets set "OpenAi:ApiKey" "your-openai-api-key"
   cd ../..
   ```

### Production Configuration
Secrets are managed via:
- **Azure Key Vault** - For Azure deployments
- **AWS Secrets Manager** - For AWS deployments
- Environment variables - As fallback

Update [appsettings.example.json](src/Nexus.API/appsettings.example.json) with placeholder values for documentation.

## Run
From the repository root:
- `dotnet build`
- `dotnet run --project src/Nexus.API/Nexus.API.csproj`

Swagger UI will be available at `/swagger`.
