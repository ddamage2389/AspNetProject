FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ ./src/
RUN dotnet restore src/Services/Users/AspNetProject.Users.Api/AspNetProject.Users.Api.csproj
RUN dotnet publish src/Services/Users/AspNetProject.Users.Api/AspNetProject.Users.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "AspNetProject.Users.Api.dll"]
