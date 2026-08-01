# tesis-gestion-api

## Configuración SMTP

La API lee la configuración de mail desde la sección `Email`.

### Desarrollo local

Configurar `TesisGestorApi/appsettings.Development.json` con:

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "EnableSsl": true,
    "User": "tu-cuenta@gmail.com",
    "Pass": "contraseña-de-aplicacion-sin-espacios",
    "From": "tu-cuenta@gmail.com"
  }
}
```

Para Gmail:

- `User` y `From` deben ser la misma cuenta.
- `Pass` debe ser una contraseña de aplicación, no la contraseña normal.
- La cuenta debe tener verificación en 2 pasos activa.
- Si Google bloquea SMTP, entrar a la cuenta por navegador y revisar actividad de seguridad.

### Producción

En producción usar variables de entorno del hosting:

```bash
export Email__SmtpHost="smtp.gmail.com"
export Email__SmtpPort="587"
export Email__EnableSsl="true"
export Email__User="tu-cuenta@gmail.com"
export Email__Pass="tu-app-password"
export Email__From="tu-cuenta@gmail.com"
```

En Windows (PowerShell):

```powershell
$env:Email__SmtpHost = "smtp.gmail.com"
$env:Email__SmtpPort = "587"
$env:Email__EnableSsl = "true"
$env:Email__User = "tu-cuenta@gmail.com"
$env:Email__Pass = "tu-app-password"
$env:Email__From = "tu-cuenta@gmail.com"
```

En Render u otro hosting, cargar esas mismas variables en el panel de Environment Variables. `appsettings.json` queda sin secretos.

Comando para levantar BACKEND por perfil

dotnet run --launch-profile https
dotnet run --launch-profile http
