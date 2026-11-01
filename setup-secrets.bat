@echo off
rem Stores the demo password and a partner API key with user-secrets, outside the repository.
setlocal
set /p DEMO_PASSWORD=Demo password: 
set /p API_KEY=Partner API key: 
dotnet user-secrets set "Seed:DemoPassword" "%DEMO_PASSWORD%" --project src\ApexPost.Web
if errorlevel 1 exit /b 1
dotnet user-secrets set "Integration:ApiKeys:AcmeCouriers" "%API_KEY%" --project src\ApexPost.Web
if errorlevel 1 exit /b 1
dotnet user-secrets set "ConnectionStrings:ApexPostDb" "Server=<server>;Database=<database>;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=True" --project src\ApexPost.Web
if errorlevel 1 exit /b 1
echo Replace the ConnectionStrings:ApexPostDb placeholder with your own value before using SQL Server.
echo Secrets stored. Start the application with: dotnet run --project src\ApexPost.Web --launch-profile https
endlocal
