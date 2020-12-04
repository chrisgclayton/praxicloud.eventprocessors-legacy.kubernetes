FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build

# Default Configuration to Release if not provided
ARG BUILD_CONFIGURATION=Release

WORKDIR /app

# Copy Solution Directory
COPY ./src .
RUN dotnet restore ./praxicloud.eventprocessors-legacy.kubernetes.solution.sln

# copy and publish app and libraries
WORKDIR /app/
COPY /. ./
WORKDIR /app
RUN dotnet publish -c Release -o out ./praxicloud.eventprocessors-legacy.sample/praxicloud.eventprocessors-legacy.sample.csproj

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "praxicloud.eventprocessors-legacy.sample.dll"]

EXPOSE 9600/tcp
