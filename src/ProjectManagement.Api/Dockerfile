# syntax=docker/dockerfile:1.7
# Build context is expected to be the repository root.
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY src/BusinessLogic/BusinessLogic.csproj           src/BusinessLogic/
COPY src/DataAccess/DataAccess.csproj                 src/DataAccess/
COPY src/ProjectManagement.Api/ProjectManagement.Api.csproj src/ProjectManagement.Api/
RUN dotnet restore src/ProjectManagement.Api/ProjectManagement.Api.csproj

COPY src/BusinessLogic/           src/BusinessLogic/
COPY src/DataAccess/              src/DataAccess/
COPY src/ProjectManagement.Api/   src/ProjectManagement.Api/

RUN dotnet publish src/ProjectManagement.Api/ProjectManagement.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

# Default to SQLite stored on a mounted volume; overridable via compose / env.
ENV Database__Provider=Sqlite \
    ConnectionStrings__DefaultConnection="Data Source=/data/projectmanagement.db" \
    FileStorage__BasePath=/data/uploads

RUN mkdir -p /data/uploads

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "ProjectManagement.Api.dll"]
