#-------COMPILE--------
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /source
COPY . .
#USING --disable-parallel 
#SINCE MY DOCKER INSTANCE ONLY HAS 1 CORE
RUN dotnet restore "MyBooru.csproj" --disable-parallel
RUN dotnet publish "MyBooru.csproj" -c release --no-restore -o /app

#--------RUN---------
FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
COPY --from=build /app ./
COPY ffmpeg /app/ffmpeg
EXPOSE 80
EXPOSE 443
ENTRYPOINT [ "dotnet", "MyBooru.dll" ]