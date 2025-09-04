# Railway optimized Dockerfile for .NET 8 API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Set globalization to invariant mode to avoid locale issues
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Copy project file and restore
COPY Team.API/Team.API.csproj Team.API/
RUN dotnet restore Team.API/Team.API.csproj

# Copy source and build
COPY Team.API/ Team.API/
WORKDIR /src/Team.API
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Set environment for Railway
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV ASPNETCORE_ENVIRONMENT=Production

# Copy published app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Team.API.dll"]