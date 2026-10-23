# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Backend/ProjectK.Backend/ Backend/ProjectK.Backend/
# The GitHub Packages token reaches only this step, as a BuildKit secret: it is never an ARG or ENV,
# so it stays out of the image layers and the build history.
RUN --mount=type=secret,id=nuget_auth_token,env=NUGET_AUTH_TOKEN \
    if [ -z "${NUGET_AUTH_TOKEN:-}" ]; then \
      echo "NUGET_AUTH_TOKEN is required to restore Project-K private GitHub NuGet packages" \
        "when building the API image locally."; \
      echo "Set PROJECTK_NUGET_AUTH_TOKEN in .env, or use the published" \
        "ghcr.io/lileyka-app/projectk-api image from the release bundle."; \
      exit 1; \
    fi \
    && dotnet restore Backend/ProjectK.Backend/ProjectK.API/ProjectK.API.csproj
RUN dotnet publish Backend/ProjectK.Backend/ProjectK.API/ProjectK.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
# The last catalogue fetched from the catalogue service is kept here, so a restart without the
# network still has one: the only path the API writes to, so the only one the non-root user owns.
RUN mkdir -p /app/catalog-cache && chown "$APP_UID" /app/catalog-cache
USER $APP_UID
ENTRYPOINT ["dotnet", "ProjectK.API.dll"]
