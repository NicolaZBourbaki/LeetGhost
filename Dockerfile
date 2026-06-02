# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy solution and project files
COPY LeetGhost.sln .
COPY src/LeetGhost/LeetGhost.csproj src/LeetGhost/

# Restore dependencies
RUN dotnet restore

# Copy the rest of the source code
COPY src/ src/

# Build and publish the application
WORKDIR /source/src/LeetGhost
RUN dotnet publish -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create data directory for SQLite database
RUN mkdir -p /data

# Copy the published application from build stage
COPY --from=build /app .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 8080

# Run the application
ENTRYPOINT ["dotnet", "LeetGhost.dll"]
