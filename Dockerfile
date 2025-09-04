# �ϥΩx�誺 .NET 8 runtime �@����¦�蹳
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# �]�wUTF-8�s�X����
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

# �ϥ� .NET 8 SDK �i��ظm
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# �]�wUTF-8�s�X����
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

# �ƻs�M���ɮר��٭�̿�
COPY ["Team.API/Team.API.csproj", "Team.API/"]
RUN dotnet restore "Team.API/Team.API.csproj"

# �ƻs�Ҧ���l�X�ëظm
COPY . .
WORKDIR "/src/Team.API"
RUN dotnet build "Team.API.csproj" -c Release -o /app/build

# �o�����ε{��
FROM build AS publish
RUN dotnet publish "Team.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# �إ̲߳׬M��
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Railway �|�۰ʳ]�w PORT �����ܼ�
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Team.API.dll"]