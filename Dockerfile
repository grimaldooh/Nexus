FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy project files
COPY ["src/Nexus.API/Nexus.API.csproj", "src/Nexus.API/"]
COPY ["src/Nexus.Domain/Nexus.Domain.csproj", "src/Nexus.Domain/"]
COPY ["src/Nexus.Infrastructure/Nexus.Infrastructure.csproj", "src/Nexus.Infrastructure/"]
COPY ["src/Nexus.Application/Nexus.Application.csproj", "src/Nexus.Application/"]

# Restore dependencies
RUN dotnet restore "src/Nexus.API/Nexus.API.csproj"

# Copy source code
COPY . .

# Build and publish
RUN dotnet publish "src/Nexus.API/Nexus.API.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy published app from build
COPY --from=build /app/publish .

# Expose ports
EXPOSE 5000
EXPOSE 5001

# Set environment
ENV ASPNETCORE_URLS="http://+:5000;https://+:5001"
ENV ASPNETCORE_ENVIRONMENT="Development"

ENTRYPOINT ["dotnet", "Nexus.API.dll"]
