# Build stage — restore, build, pack
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file first for layer caching
COPY src/Prompt.csproj src/
RUN dotnet restore src/Prompt.csproj

# Copy everything else
COPY . .

# Build in Release mode
RUN dotnet build src/Prompt.csproj -c Release --no-restore

# Pack NuGet package
RUN dotnet pack src/Prompt.csproj -c Release --no-build -o /packages

# Output stage — slim image with just the NuGet package
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS output
WORKDIR /packages
COPY --from=build /packages/*.nupkg .

# Default command: list the packaged artifacts
CMD ["ls", "-la", "/packages"]
