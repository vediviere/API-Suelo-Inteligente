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

var app = builder.Build();

var lecturas = new ConcurrentQueue<LecturaSensor>();
var analisisLecturas = new ConcurrentQueue<AnalisisLectura>();

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

  var lectura = new LecturaSensor
  {
    LecturaId = $"lec-{Guid.NewGuid().ToString("N")[..8]}",
    DispositivoId = nuevaLectura.DispositivoId,
    Cultivo = string.IsNullOrWhiteSpace(nuevaLectura.Cultivo)
          ? "No especificado"
          : nuevaLectura.Cultivo,
    Ph = nuevaLectura.Ph,
    Conductividad = nuevaLectura.Conductividad,
    Humedad = nuevaLectura.Humedad,
    Orp = nuevaLectura.Orp,
    Temperatura = nuevaLectura.Temperatura,
    FechaRecepcion = DateTime.UtcNow,
    Origen = "app-movil",
    Estado = CalcularEstado(nuevaLectura),
    Procesado = true
  };

  var analisisLectura = AnalizadorSuelo.Procesar(lectura);

  lecturas.Enqueue(lectura);
  analisisLecturas.Enqueue(analisisLectura);

  while (lecturas.Count > 100)
  {
    lecturas.TryDequeue(out _);
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

app.Run();

static string CalcularEstado(NuevaLecturaSensor lectura)
{
  if (lectura.Ph < 5 || lectura.Ph > 8 ||
      lectura.Humedad < 20 || lectura.Humedad > 80 ||
      lectura.Temperatura < 10 || lectura.Temperatura > 38 ||
      lectura.Conductividad > 4)
  {
    return "critico";
  }

  if (lectura.Ph < 5.5 || lectura.Ph > 7.5 ||
      lectura.Humedad < 30 || lectura.Humedad > 70 ||
      lectura.Temperatura < 15 || lectura.Temperatura > 33 ||
      lectura.Conductividad > 3)
  {
    return "advertencia";
  }

  return "optimo";
}
