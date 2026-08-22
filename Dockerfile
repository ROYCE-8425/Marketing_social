# syntax=docker/dockerfile:1@sha256:ecfaec9ed6d810b56388c508f4121597bfbba70d41a6dfeee4d8cad5f295fc32
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1fc6e423f543119c406d24e2e687d67c569f18f04a37a8b0005d80ad0dcee80 AS build
WORKDIR /src

# Copy CPM, solution, and root build configs
COPY Directory.Packages.props Directory.Build.props NuGet.Config DXOS.slnx ./

# Copy source and tests
COPY src/ src/
COPY tests/ tests/

# Restore in locked mode for API and its dependencies
RUN dotnet restore src/DXOS.Api/DXOS.Api.csproj --locked-mode

# Publish Api in Release mode
RUN dotnet publish src/DXOS.Api/DXOS.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:207cc51496778557731c81ff670333d8ade4a4fec22768fd1be8e78474a84ecf AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "DXOS.Api.dll"]

