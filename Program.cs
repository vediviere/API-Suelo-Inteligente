using ApiSueloInteligente.Models;
using System.Text.Json;
using System.Collections.Concurrent;
using ApiSueloInteligente.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
  options.AddDefaultPolicy(policy =>
  {
    policy.AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod();
  });
});

builder.Services.AddCors(options =>
{
  options.AddPolicy("Web", policy =>
  {
    policy.AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod();
  });
});

builder.Services.AddHttpClient<ServicioIa>(cliente =>
{
  cliente.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
  cliente.Timeout = TimeSpan.FromSeconds(25);
});

var app = builder.Build();

var lecturas = new ConcurrentQueue<LecturaSensor>();
var analisisLecturas = new ConcurrentQueue<AnalisisLectura>();
var interpretacionesIa =
    new ConcurrentDictionary<string, InterpretacionIa>(
        StringComparer.OrdinalIgnoreCase);

var lecturasPorId = new ConcurrentDictionary<string, LecturaSensor>(
    StringComparer.OrdinalIgnoreCase);

app.UseCors("Web");

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

var rutaJson = Path.Combine(AppContext.BaseDirectory, "Data", "Analisis.json");

if (!File.Exists(rutaJson))
{
  throw new FileNotFoundException("No se encontró el archivo Data/Analisis.json.");
}

var opciones = new JsonSerializerOptions
{
  PropertyNameCaseInsensitive = true
};

var json = File.ReadAllText(rutaJson);
var analisis = JsonSerializer.Deserialize<List<AnalisisSuelo>>(json, opciones) ?? [];

analisis = analisis
    .OrderByDescending(a => a.FechaProcesamiento)
    .ToList();

var rutaCatalogo = Path.Combine(
    AppContext.BaseDirectory,
    "Data",
    "CatalogoRegional.json");

if (!File.Exists(rutaCatalogo))
{
  throw new FileNotFoundException(
      "No se encontró el archivo Data/CatalogoRegional.json.");
}

var jsonCatalogo = File.ReadAllText(rutaCatalogo);

var catalogoRegional =
    JsonSerializer.Deserialize<CatalogoRegional>(jsonCatalogo, opciones)
    ?? throw new InvalidOperationException(
        "No fue posible leer Data/CatalogoRegional.json.");

if (catalogoRegional.Cultivos.Count == 0)
{
  throw new InvalidOperationException(
      "El catálogo regional no contiene cultivos.");
}

app.MapGet("/", () => Results.Ok(new
{
  nombre = "API Suelo Inteligente",
  version = "1.0",
  registros = analisis.Count
}));

app.MapGet("/health", () => Results.Ok(new
{
  estado = "Disponible"
}));

app.MapGet("/api/analisis", (int pagina = 1, int cantidad = 20) =>
{
  if (pagina < 1)
  {
    return Results.BadRequest(new { mensaje = "La página debe ser mayor que cero." });
  }

  if (cantidad < 1 || cantidad > 100)
  {
    return Results.BadRequest(new { mensaje = "La cantidad debe estar entre 1 y 100." });
  }

  var total = analisis.Count;
  var totalPaginas = (int)Math.Ceiling(total / (double)cantidad);

  var datos = analisis
      .Skip((pagina - 1) * cantidad)
      .Take(cantidad)
      .ToList();

  return Results.Ok(new
  {
    pagina,
    cantidad,
    total,
    totalPaginas,
    datos
  });
});

app.MapGet("/api/analisis/todos", () =>
{
  return Results.Ok(analisis);
});

app.MapGet("/api/analisis/ultimo", () =>
{
  var ultimo = analisis.FirstOrDefault();

  if (ultimo is null)
  {
    return Results.NotFound(new { mensaje = "No existen análisis registrados." });
  }

  return Results.Ok(ultimo);
});

app.MapGet("/api/analisis/{analisisId}", (string analisisId) =>
{
  var resultado = analisis.FirstOrDefault(a =>
      a.AnalisisId.Equals(analisisId, StringComparison.OrdinalIgnoreCase));

  if (resultado is null)
  {
    return Results.NotFound(new { mensaje = "Análisis no encontrado." });
  }

  return Results.Ok(resultado);
});

app.MapGet("/api/analisis/lectura/{lecturaId}", (string lecturaId) =>
{
  var resultado = analisis.FirstOrDefault(a =>
      a.LecturaId.Equals(lecturaId, StringComparison.OrdinalIgnoreCase));

  if (resultado is null)
  {
    return Results.NotFound(new { mensaje = "Lectura no encontrada." });
  }

  return Results.Ok(resultado);
});

app.MapPost("/api/lecturas", (NuevaLecturaSensor nuevaLectura) =>
{
  if (string.IsNullOrWhiteSpace(nuevaLectura.DispositivoId))
  {
    return Results.BadRequest(new { mensaje = "El dispositivoId es obligatorio." });
  }

  if (!string.IsNullOrWhiteSpace(nuevaLectura.LecturaId) &&
    lecturasPorId.TryGetValue(nuevaLectura.LecturaId.Trim(), out var lecturaExistente))
  {
    var analisisExistente = analisisLecturas.FirstOrDefault(a =>
        a.LecturaId.Equals(lecturaExistente.LecturaId, StringComparison.OrdinalIgnoreCase));

    return Results.Ok(new
    {
      lectura = lecturaExistente,
      analisis = analisisExistente,
      duplicada = true
    });
  }

  if (nuevaLectura.Ph < 0 || nuevaLectura.Ph > 14)
  {
    return Results.BadRequest(new { mensaje = "El pH debe estar entre 0 y 14." });
  }

  if (nuevaLectura.Humedad < 0 || nuevaLectura.Humedad > 100)
  {
    return Results.BadRequest(new { mensaje = "La humedad debe estar entre 0 y 100." });
  }

  if (nuevaLectura.Conductividad < 0)
  {
    return Results.BadRequest(new { mensaje = "La conductividad no puede ser negativa." });
  }

  var lecturaId = string.IsNullOrWhiteSpace(nuevaLectura.LecturaId)
    ? $"lec-{Guid.NewGuid().ToString("N")[..8]}"
    : nuevaLectura.LecturaId.Trim();

  if (string.IsNullOrWhiteSpace(nuevaLectura.Zona))
  {
    return Results.BadRequest(new
    {
      mensaje = "La zona es obligatoria."
    });
  }

  if (string.IsNullOrWhiteSpace(nuevaLectura.Cultivo))
  {
    return Results.BadRequest(new
    {
      mensaje = "El cultivo es obligatorio."
    });
  }

  var cultivoCatalogo = catalogoRegional.Cultivos
      .FirstOrDefault(elemento =>
          elemento.Key.Equals(
              nuevaLectura.Cultivo.Trim(),
              StringComparison.OrdinalIgnoreCase) ||
          elemento.Value.NombreComun.Equals(
              nuevaLectura.Cultivo.Trim(),
              StringComparison.OrdinalIgnoreCase));

  if (string.IsNullOrWhiteSpace(cultivoCatalogo.Key))
  {
    return Results.BadRequest(new
    {
      mensaje =
          $"El cultivo '{nuevaLectura.Cultivo}' no existe en el catálogo regional."
    });
  }

  var zonaValida = cultivoCatalogo.Value.ZonasRepresentativas
      .Any(zona =>
          zona.Equals(
              nuevaLectura.Zona.Trim(),
              StringComparison.OrdinalIgnoreCase));

  if (!zonaValida)
  {
    return Results.BadRequest(new
    {
      mensaje =
          $"El cultivo '{cultivoCatalogo.Value.NombreComun}' no está registrado para la zona '{nuevaLectura.Zona}'."
    });
  }

  var lectura = new LecturaSensor
  {
    LecturaId = lecturaId,
    DispositivoId = nuevaLectura.DispositivoId.Trim(),
    CampoId = string.IsNullOrWhiteSpace(nuevaLectura.CampoId)
        ? "campo-sin-asignar"
        : nuevaLectura.CampoId.Trim(),
    CampoNombre = string.IsNullOrWhiteSpace(nuevaLectura.CampoNombre)
        ? "Campo sin asignar"
        : nuevaLectura.CampoNombre.Trim(),
    Zona = nuevaLectura.Zona.Trim(),
    Cultivo = cultivoCatalogo.Value.NombreComun,
    Ph = nuevaLectura.Ph,
    Conductividad = nuevaLectura.Conductividad,
    Humedad = nuevaLectura.Humedad,
    Orp = nuevaLectura.Orp,
    Temperatura = nuevaLectura.Temperatura,
    FechaCaptura = nuevaLectura.FechaCaptura?.ToUniversalTime() ?? DateTime.UtcNow,
    FechaRecepcion = DateTime.UtcNow,
    Origen = "app-movil",
    Estado = string.Empty,
    Procesado = true
  };

  var analisisLectura =
    AnalizadorSuelo.Procesar(lectura, catalogoRegional);
  lectura.Estado = analisisLectura.EstadoGeneral;

  if (!lecturasPorId.TryAdd(lectura.LecturaId, lectura))
  {
    var existente = lecturasPorId[lectura.LecturaId];

    var analisisExistente = analisisLecturas.FirstOrDefault(a =>
        a.LecturaId.Equals(existente.LecturaId, StringComparison.OrdinalIgnoreCase));

    return Results.Ok(new
    {
      lectura = existente,
      analisis = analisisExistente,
      duplicada = true
    });
  }

  lecturas.Enqueue(lectura);
  analisisLecturas.Enqueue(analisisLectura);

  while (lecturas.Count > 100)
  {
    if (lecturas.TryDequeue(out var eliminada))
    {
      lecturasPorId.TryRemove(eliminada.LecturaId, out _);
    }
  }

  while (analisisLecturas.Count > 100)
  {
    analisisLecturas.TryDequeue(out _);
  }

  return Results.Created($"/api/lecturas/{lectura.LecturaId}", new
  {
    lectura,
    analisis = analisisLectura
  });
})
.WithName("RegistrarLectura");

app.MapGet("/api/lecturas/ultima", () =>
{
  var ultima = lecturas.LastOrDefault();

  if (ultima is null)
  {
    return Results.NotFound(new { mensaje = "Todavía no se han recibido lecturas." });
  }

  return Results.Ok(ultima);
})
.WithName("ObtenerUltimaLectura");

app.MapGet("/api/lecturas", (int? cantidad) =>
{
  var limite = Math.Clamp(cantidad ?? 20, 1, 100);
  var resultado = lecturas.Reverse().Take(limite).ToList();

  return Results.Ok(new
  {
    total = resultado.Count,
    lecturas = resultado
  });
})
.WithName("ObtenerLecturas");

app.MapGet("/api/lecturas/analisis/ultimo", () =>
{
  var ultimo = analisisLecturas.LastOrDefault();

  if (ultimo is null)
  {
    return Results.NotFound(new
    {
      mensaje = "Todavía no se han procesado lecturas."
    });
  }

  return Results.Ok(ultimo);
})
.WithName("ObtenerUltimoAnalisisRecibido");

app.MapGet("/api/lecturas/analisis", (int? cantidad) =>
{
  var limite = Math.Clamp(cantidad ?? 20, 1, 100);

  var resultado = analisisLecturas
      .Reverse()
      .Take(limite)
      .ToList();

  return Results.Ok(new
  {
    total = resultado.Count,
    analisis = resultado
  });
})
.WithName("ObtenerAnalisisRecibidos");

app.MapGet("/api/catalogo/zonas", () =>
{
  var zonas = catalogoRegional.Cultivos.Values
      .SelectMany(cultivo => cultivo.ZonasRepresentativas)
      .Where(zona => !string.IsNullOrWhiteSpace(zona))
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .OrderBy(zona => zona)
      .ToList();

  return Results.Ok(zonas);
});

app.MapGet("/api/catalogo/zonas/{zona}/cultivos", (string zona) =>
{
  var cultivos = catalogoRegional.Cultivos
      .Where(elemento =>
          elemento.Value.ZonasRepresentativas.Any(
              zonaRegistrada =>
                  zonaRegistrada.Equals(
                      zona,
                      StringComparison.OrdinalIgnoreCase)))
      .Select(elemento => new
      {
        id = elemento.Key,
        nombre = elemento.Value.NombreComun,
        nombreCientifico = elemento.Value.NombreCientifico,
        tipo = elemento.Value.Tipo
      })
      .OrderBy(cultivo => cultivo.nombre)
      .ToList();

  if (cultivos.Count == 0)
  {
    return Results.NotFound(new
    {
      mensaje =
          $"No existen cultivos registrados para la zona '{zona}'."
    });
  }

  return Results.Ok(cultivos);
});

app.MapPost(
    "/api/lecturas/analisis/{analisisId}/interpretacion-ia",
    async (
        string analisisId,
        ServicioIa servicioIa,
        CancellationToken cancellationToken) =>
    {
      if (interpretacionesIa.TryGetValue(
          analisisId,
          out var interpretacionGuardada))
      {
        return Results.Ok(interpretacionGuardada);
      }

      var analisis = analisisLecturas.FirstOrDefault(
          elemento => elemento.AnalisisId.Equals(
              analisisId,
              StringComparison.OrdinalIgnoreCase));

      if (analisis is null)
      {
        return Results.NotFound(new
        {
          mensaje = "No se encontró el análisis solicitado."
        });
      }

      if (!lecturasPorId.TryGetValue(
          analisis.LecturaId,
          out var lectura))
      {
        return Results.NotFound(new
        {
          mensaje = "No se encontró la lectura relacionada."
        });
      }

      try
      {
        var interpretacion =
            await servicioIa.GenerarInterpretacionAsync(
                lectura,
                analisis,
                cancellationToken);

        interpretacionesIa[analisisId] = interpretacion;

        return Results.Ok(interpretacion);
      }
      catch (Exception error)
      {
        return Results.Problem(
            title: "No fue posible generar la interpretación.",
            detail: error.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
      }
    });

app.Run();

