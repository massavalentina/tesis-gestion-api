# tesis-gestion-api

## SMTP por variables de entorno

Configurar estas variables antes de levantar la API:

```bash
export Email__SmtpHost="smtp.gmail.com"
export Email__SmtpPort="587"
export Email__User="tu-cuenta@gmail.com"
export Email__Pass="tu-app-password"
export Email__From="tu-cuenta@gmail.com"
```

En Windows (PowerShell):

```powershell
$env:Email__SmtpHost = "smtp.gmail.com"
$env:Email__SmtpPort = "587"
$env:Email__User = "tu-cuenta@gmail.com"
$env:Email__Pass = "tu-app-password"
$env:Email__From = "tu-cuenta@gmail.com"
```

La API toma estos valores con la convención `Email:*` de ASP.NET Core.

Comando para levantar BACKEND por perfil

dotnet run --launch-profile https
dotnet run --launch-profile http
