FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore InovaSkill.Importer.Api/InovaSkill.Importer.Api.csproj
RUN dotnet publish InovaSkill.Importer.Api/InovaSkill.Importer.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "InovaSkill.Importer.Api.dll"]
