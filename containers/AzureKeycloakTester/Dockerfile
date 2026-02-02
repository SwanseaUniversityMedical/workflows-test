#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AzureKeycloakTester/AzureKeycloakTester.csproj", "AzureKeycloakTester/"]
RUN dotnet restore "AzureKeycloakTester/AzureKeycloakTester.csproj"
COPY . .
WORKDIR "/src/AzureKeycloakTester"
RUN dotnet build "AzureKeycloakTester.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AzureKeycloakTester.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN sed -i 's/DEFAULT@SECLEVEL=2/DEFAULT@SECLEVEL=1/g' /etc/ssl/openssl.cnf

#COPY ./HDRUKInovationGatewayExtractor/datasets.v2.json /app/

# using a non-root user is a best practice for security related execution.
##RUN groupadd --gid ${gid} ${group} && useradd -u ${uid} -g ${group} -s /bin/sh ${user}
#RUN groupadd --gid 1011 appgroup && useradd -u 1012 -g appgroup -s /bin/sh appuser
# Serve on port 8080, we cannot serve on port 80 with a custom user that is not root.
#ENV ASPNETCORE_URLS=http://+:8080
#EXPOSE 8080
#
## Now switch user
#USER 1012


ENTRYPOINT ["dotnet", "AzureKeycloakTester.dll"]