/*
    The application's login and its database user. The password comes from the command line or a secret store, never
    from this file; the connection string goes into user-secrets or the environment.

        sqlcmd -S <server> -E -C -I -d ApexPostDb -b -v AppPassword="<password>" -i sql/security/create-app-login.sql
*/
IF SUSER_ID('apexpost_app_login') IS NULL
    CREATE LOGIN apexpost_app_login WITH PASSWORD = N'$(AppPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
GO
IF DATABASE_PRINCIPAL_ID('apexpost_app_login') IS NULL
    CREATE USER apexpost_app_login FOR LOGIN apexpost_app_login WITH DEFAULT_SCHEMA = dbo;
GO
ALTER ROLE apexpost_app ADD MEMBER apexpost_app_login;
GO
