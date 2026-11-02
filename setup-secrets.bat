@echo off
rem Stores the demo password, a partner API key and the connection string with user-secrets, outside the repository.
setlocal
set "LOCAL_DATABASE=Server=localhost;Database=ApexPostDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
set /p "DEMO_PASSWORD=Demo password: "
set /p "API_KEY=Partner API key: "
set "CONNECTION="
set /p "CONNECTION=Connection string (Enter for ApexPostDb on this computer, signing in with Windows): "
if not defined CONNECTION set "CONNECTION=%LOCAL_DATABASE%"
dotnet user-secrets set "Seed:DemoPassword" "%DEMO_PASSWORD%" --project src\ApexPost.Web
if errorlevel 1 exit /b 1
dotnet user-secrets set "Integration:ApiKeys:AcmeCouriers" "%API_KEY%" --project src\ApexPost.Web
if errorlevel 1 exit /b 1
dotnet user-secrets set "ConnectionStrings:ApexPostDb" "%CONNECTION%" --project src\ApexPost.Web
if errorlevel 1 exit /b 1
echo Secrets stored. Next: dotnet tool restore, then dotnet ef database update --project src\ApexPost.Infrastructure
endlocal
