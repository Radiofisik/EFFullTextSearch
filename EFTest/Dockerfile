FROM microsoft/dotnet:2.2-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /src
COPY EFTest/EFTest.csproj EFTest/
RUN dotnet restore EFTest/EFTest.csproj
COPY . .
WORKDIR /src/EFTest
RUN dotnet build EFTest.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish EFTest.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "EFTest.dll"]
