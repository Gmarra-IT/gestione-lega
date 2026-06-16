FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer-cached restore
COPY ClassificaLega.Domain/ClassificaLega.Domain.csproj ClassificaLega.Domain/
COPY ClassificaLega.Infrastructure/ClassificaLega.Infrastructure.csproj ClassificaLega.Infrastructure/
COPY ClassificaLega.Api/ClassificaLega.Api.csproj ClassificaLega.Api/

RUN dotnet restore ClassificaLega.Api/ClassificaLega.Api.csproj

# Copy sources (Tests excluded from build context, not needed for publish)
COPY ClassificaLega.Domain/ ClassificaLega.Domain/
COPY ClassificaLega.Infrastructure/ ClassificaLega.Infrastructure/
COPY ClassificaLega.Api/ ClassificaLega.Api/

RUN dotnet publish ClassificaLega.Api/ClassificaLega.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClassificaLega.Api.dll"]
