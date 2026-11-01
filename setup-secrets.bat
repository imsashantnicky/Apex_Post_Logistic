@echo off
rem Stores the demo password and a partner API key with user-secrets, outside the repository.
setlocal
set /p DEMO_PASSWORD=Demo password: 
set /p API_KEY=Partner API key: 
dotnet user-secrets set "Seed:DemoPassword" "%DEMO_PASSWORD%" --project src\ApexPost.Web
if errorlevel 1 exit /b 1
dotnet user-secrets set "Integration:ApiKeys:AcmeCouriers" "%API_KEY%" --project src\ApexPost.Web
if errorlevel 1 exit /b 1
echo Secrets stored. Start the application with: dotnet run --project src\ApexPost.Web --launch-profile https
endlocal
