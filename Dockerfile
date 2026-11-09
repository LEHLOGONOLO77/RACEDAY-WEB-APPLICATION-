# syntax=docker/dockerfile:1

# ---- build stage: restore + publish ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file first so the restore layer is cached until dependencies change.
COPY RaceDay.Web.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish RaceDay.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime stage: small image, non-root ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    Api__BaseUrl=http://host.docker.internal:5100

COPY --from=build /app/publish ./

# The aspnet image ships a non-root "app" user (.NET 8+).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
