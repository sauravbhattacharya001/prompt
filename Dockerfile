# Build stage — restore and build
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy project files first for layer caching
COPY src/Prompt.csproj src/
COPY tests/Prompt.Tests.csproj tests/
COPY Prompt.sln .
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet build -c Release --no-restore

# Test stage — run unit tests (fail the build on test failure)
FROM build AS test
RUN dotnet test tests/Prompt.Tests.csproj -c Release --no-build --logger "console;verbosity=minimal"

# Pack stage — produce NuGet package
FROM build AS pack
RUN dotnet pack src/Prompt.csproj -c Release --no-build -o /packages

# Output stage — minimal image with just the artifact
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine AS output
RUN addgroup -S app && adduser -S app -G app
WORKDIR /packages
COPY --from=pack /packages/*.nupkg .
RUN chown -R app:app /packages
USER app

LABEL org.opencontainers.image.source="https://github.com/sauravbhattacharya001/prompt"
LABEL org.opencontainers.image.description="Prompt — .NET library for LLM prompt engineering"
LABEL org.opencontainers.image.licenses="MIT"

CMD ["ls", "-la", "/packages"]
