FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["aspnet-core-api.csproj", "./"]
COPY src/ ./src/
RUN dotnet restore "aspnet-core-api.csproj"
RUN dotnet build "aspnet-core-api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "aspnet-core-api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "aspnet-core-api.dll"]