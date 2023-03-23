FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["examples/Contraband.Examples.WebApi/Contraband.Examples.WebApi.csproj", "examples/Contraband.Examples.WebApi/"]
RUN dotnet restore "examples/Contraband.Examples.WebApi/Contraband.Examples.WebApi.csproj"
COPY . .
WORKDIR "/src/examples/Contraband.Examples.WebApi"
RUN dotnet build "Contraband.Examples.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Contraband.Examples.WebApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Contraband.Examples.WebApi.dll"]
