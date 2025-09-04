# 使用官方的 .NET 8 runtime 作為基礎鏡像
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# 設定UTF-8編碼環境
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

# 使用 .NET 8 SDK 進行建置
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 設定UTF-8編碼環境
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

# 複製專案檔案並還原依賴
COPY ["Team.API/Team.API.csproj", "Team.API/"]
RUN dotnet restore "Team.API/Team.API.csproj"

# 複製所有原始碼並建置
COPY . .
WORKDIR "/src/Team.API"
RUN dotnet build "Team.API.csproj" -c Release -o /app/build

# 發布應用程式
FROM build AS publish
RUN dotnet publish "Team.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 建立最終映像
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Railway 會自動設定 PORT 環境變數
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Team.API.dll"]