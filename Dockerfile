# Stage 1: Build the backend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore NuGet packages
COPY backend/DnDSheetApi.csproj backend/
RUN dotnet restore backend/DnDSheetApi.csproj

# Copy the rest of the backend files and build
COPY backend/ backend/
WORKDIR /src/backend
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Serve the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy the built backend
COPY --from=build /app/publish .

# Copy the frontend files to a specific directory in the container
RUN mkdir /frontend
COPY *.html /frontend/
COPY *.css /frontend/
COPY *.js /frontend/

# Set the environment variable so Program.cs knows where to serve static files from
ENV FRONTEND_PATH="/frontend"

# Render sets the PORT environment variable dynamically
# Program.cs is already configured to read it
ENTRYPOINT ["dotnet", "DnDSheetApi.dll"]
