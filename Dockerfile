# Railway optimized Dockerfile for .NET 8 API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 🔧 關閉 globalization-invariant 模式以支援完整文化功能
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Copy project file and restore
COPY Team.API/Team.API.csproj Team.API/
RUN dotnet restore Team.API/Team.API.csproj

# Copy source and build
COPY Team.API/ Team.API/
WORKDIR /src/Team.API
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 🔧 安裝 ICU 套件以支援完整國際化
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    icu-devtools \
    libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# 🔧 設定環境變數支援完整文化
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LC_ALL=en_US.UTF-8
ENV LANG=en_US.UTF-8
ENV ASPNETCORE_ENVIRONMENT=Production

# Copy published app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Team.API.dll"]