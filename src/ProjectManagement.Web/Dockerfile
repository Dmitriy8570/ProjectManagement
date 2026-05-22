# syntax=docker/dockerfile:1.7
# Build context is expected to be the repository root.
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY src/BusinessLogic/BusinessLogic.csproj           src/BusinessLogic/
COPY src/DataAccess/DataAccess.csproj                 src/DataAccess/
COPY src/ProjectManagement.Web/ProjectManagement.Web.csproj src/ProjectManagement.Web/
RUN dotnet restore src/ProjectManagement.Web/ProjectManagement.Web.csproj

COPY src/BusinessLogic/           src/BusinessLogic/
COPY src/DataAccess/              src/DataAccess/
COPY src/ProjectManagement.Web/   src/ProjectManagement.Web/

RUN dotnet publish src/ProjectManagement.Web/ProjectManagement.Web.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

ENV Database__Provider=Sqlite \
    ConnectionStrings__DefaultConnection="Data Source=/data/projectmanagement.db" \
    FileStorage__BasePath=/data/uploads

RUN mkdir -p /data/uploads

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "ProjectManagement.Web.dll"]
