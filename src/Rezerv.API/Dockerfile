FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Rezerv.slnx .
COPY src/Rezerv.Domain/Rezerv.Domain.csproj src/Rezerv.Domain/
COPY src/Rezerv.Application/Rezerv.Application.csproj src/Rezerv.Application/
COPY src/Rezerv.Infrastructure/Rezerv.Infrastructure.csproj src/Rezerv.Infrastructure/
COPY src/Rezerv.API/Rezerv.API.csproj src/Rezerv.API/
COPY tests/Rezerv.Tests/Rezerv.Tests.csproj tests/Rezerv.Tests/
RUN dotnet restore
COPY . .
RUN dotnet publish src/Rezerv.API -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Rezerv.API.dll"]
