# TLALCANI — API de suelo inteligente

API REST desarrollada con ASP.NET Core para recibir lecturas del suelo, compararlas con el catálogo regional y generar análisis e interpretaciones inteligentes.

## Direcciones publicadas

### API

```text
https://api-suelo-inteligente.onrender.com
```

### Swagger

```text
https://api-suelo-inteligente.onrender.com/swagger
```

## Funcionalidades

- Registro y consulta de lecturas.
- Consulta de la última lectura.
- Procesamiento de pH, conductividad, humedad, ORP y temperatura.
- Comparación con rangos regionales.
- Consulta de zonas y cultivos.
- Generación de alertas y recomendaciones.
- Interpretación mediante Groq.
- Documentación mediante Swagger.

## Tecnologías

- .NET 8
- ASP.NET Core Minimal API
- Swagger/OpenAPI
- Groq API
- Docker
- Render

## Requisitos

- .NET 8 SDK.
- Visual Studio 2022 o posterior.
- Una clave válida de Groq.

## Ejecución con Visual Studio

1. Abrir `ApiSueloInteligente.sln`.
2. Seleccionar `ApiSueloInteligente` como proyecto de inicio.
3. Elegir el perfil `http` o `https`.
4. Ejecutar el proyecto.

Direcciones locales:

```text
HTTP: http://localhost:5025
HTTPS: https://localhost:7003
Swagger: http://localhost:5025/swagger
```

## Ejecución desde terminal

```bash
dotnet restore
dotnet run
```

## Configuración de Groq

Para desarrollo local:

```bash
dotnet user-secrets set "Groq:ApiKey" "TU_CLAVE_DE_GROQ"
dotnet user-secrets set "Groq:Modelo" "openai/gpt-oss-20b"
```

No se debe guardar la clave dentro de `appsettings.json`.

## Configuración en Render

Variable requerida:

```env
Groq__ApiKey=TU_CLAVE_DE_GROQ
```

Variable opcional:

```env
Groq__Modelo=openai/gpt-oss-20b
```

En ASP.NET Core, los dos guiones bajos `__` representan la separación de una sección de configuración.

## Archivos de datos

```text
Data/
├── Analisis.json
└── CatalogoRegional.json
```

`CatalogoRegional.json` contiene las zonas, cultivos y rangos recomendados utilizados durante el análisis.

## Endpoints principales

### Estado del servicio

```http
GET /health
```

### Registrar una lectura

```http
POST /api/lecturas
```

Cuerpo de ejemplo:

```json
{
  "lecturaId": "lec_demo_001",
  "dispositivoId": "sensor_01",
  "campoId": "campo_001",
  "campoNombre": "Parcela norte",
  "cultivo": "naranja",
  "zona": "Venustiano Carranza",
  "fechaCaptura": "2026-09-30T18:00:00.000Z",
  "ph": 5.2,
  "conductividad": 2.8,
  "humedad": 19,
  "orp": 150,
  "temperatura": 31
}
```

### Consultar lecturas

```http
GET /api/lecturas
```

### Consultar la última lectura

```http
GET /api/lecturas/ultima
```

### Consultar análisis

```http
GET /api/lecturas/analisis
```

### Consultar el último análisis

```http
GET /api/lecturas/analisis/ultimo
```

### Consultar zonas

```http
GET /api/catalogo/zonas
```

### Consultar cultivos de una zona

```http
GET /api/catalogo/zonas/{zona}/cultivos
```

### Generar interpretación inteligente

```http
POST /api/lecturas/analisis/{analisisId}/interpretacion-ia
```

Este endpoint no requiere un cuerpo.

## Flujo de procesamiento

1. La API recibe una lectura.
2. Valida la zona y el cultivo.
3. Busca los rangos en `CatalogoRegional.json`.
4. Compara las variables con los valores recomendados.
5. Determina si cada valor es óptimo, advertencia o crítico.
6. Calcula el índice general del suelo.
7. Genera alertas y recomendaciones.
8. Conserva temporalmente la lectura y su análisis.
9. Cuando se solicita, genera una interpretación mediante Groq.

## Almacenamiento actual

Esta demostración utiliza almacenamiento en memoria.

Esto significa que:

- Los datos permanecen disponibles mientras la API está ejecutándose.
- Las lecturas se pierden cuando Render reinicia o despliega nuevamente el servicio.
- Una versión posterior puede utilizar una base de datos persistente.

## Docker

Construir la imagen:

```bash
docker build -t tlalcani-api .
```

Ejecutar el contenedor:

```bash
docker run -p 10000:10000 -e Groq__ApiKey=TU_CLAVE_DE_GROQ tlalcani-api
```

La API estará disponible en:

```text
http://localhost:10000
```

## Consideraciones

- La API no implementa autenticación en esta demostración.
- El almacenamiento actual no es permanente.
- La clave de Groq debe permanecer únicamente en la API.
- Las aplicaciones móvil y web nunca deben conocer la clave de Groq.
