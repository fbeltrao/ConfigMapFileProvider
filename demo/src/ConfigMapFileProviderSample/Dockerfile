FROM mcr.microsoft.com/dotnet/core/aspnet:2.2-stretch-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:2.2-stretch AS build
WORKDIR /src
COPY ["src/ConfigMapFileProviderSample/ConfigMapFileProviderSample.csproj", "src/ConfigMapFileProviderSample/"]
RUN dotnet restore "src/ConfigMapFileProviderSample/ConfigMapFileProviderSample.csproj"
COPY . .
WORKDIR "/src/src/ConfigMapFileProviderSample"
RUN dotnet build "ConfigMapFileProviderSample.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "ConfigMapFileProviderSample.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "ConfigMapFileProviderSample.dll"]