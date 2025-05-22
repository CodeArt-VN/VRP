# Use a runtime-only image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Copy the build output from the local machine to the container
COPY bin/Debug/net9.0/ ./

# Expose the port the application runs on
EXPOSE 80

# Set the entry point for the application
ENTRYPOINT ["dotnet", "SmartRouting.dll"]