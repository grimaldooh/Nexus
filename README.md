# Nexus

Backend service for insurance commission ingestion, sanitization, and audit workflows.

## Requirements
- .NET 9 SDK
- SQL Server

## Configuration
Update the following settings in [src/Nexus.API/appsettings.json](src/Nexus.API/appsettings.json):
- ConnectionStrings:NexusDb
- Security:ApiKey
- OpenAi:ApiKey

## Run
From the repository root:
- `dotnet build`
- `dotnet run --project src/Nexus.API/Nexus.API.csproj`

Swagger UI will be available at `/swagger`.
