# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Danec.PdfGenerator.Domain/Danec.PdfGenerator.Domain.csproj                 src/Danec.PdfGenerator.Domain/
COPY src/Danec.PdfGenerator.Application/Danec.PdfGenerator.Application.csproj       src/Danec.PdfGenerator.Application/
COPY src/Danec.PdfGenerator.Infrastructure/Danec.PdfGenerator.Infrastructure.csproj src/Danec.PdfGenerator.Infrastructure/
COPY src/Danec.PdfGenerator.Api/Danec.PdfGenerator.Api.csproj                       src/Danec.PdfGenerator.Api/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/Danec.PdfGenerator.Api/Danec.PdfGenerator.Api.csproj -r linux-x64

COPY src/ src/
# RID fijo: solo se copia el driver de Playwright para linux-x64
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/Danec.PdfGenerator.Api/Danec.PdfGenerator.Api.csproj -c $BUILD_CONFIGURATION \
      -r linux-x64 --self-contained false -o /app/publish --no-restore /p:UseAppHost=false

# Imagen Ubuntu completa (NO chiseled/alpine): Chromium necesita librerias del sistema, fuentes y fontconfig
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Un solo Chromium para Playwright y PuppeteerSharp (Puppeteer lo encuentra via PLAYWRIGHT_BROWSERS_PATH)
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN ./.playwright/node/linux-x64/node ./.playwright/package/cli.js install --with-deps chromium \
 && apt-get install -y --no-install-recommends fonts-liberation fonts-dejavu-core fontconfig \
 && rm -rf /var/lib/apt/lists/* \
 && chmod -R o+rx /ms-playwright
# El usuario no root ($APP_UID) debe poder ejecutar el driver node de Playwright
RUN chmod -R a+rX /app/.playwright && chmod a+x /app/.playwright/node/linux-x64/node

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Pdf__DefaultFontFamily="Liberation Sans"
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Danec.PdfGenerator.Api.dll"]
