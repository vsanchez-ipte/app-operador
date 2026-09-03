using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Un <see cref="RegistroCola"/> preparado para mostrarse en la lista.
/// </summary>
/// <remarks>
/// Evita llenar el XAML de convertidores: el formato y el color del estado se resuelven
/// aquí, donde son legibles y se pueden probar. El modelo de la capa de aplicación se
/// mantiene libre de decisiones de presentación.
/// </remarks>
public sealed class RegistroColaVista
{
	public RegistroColaVista(RegistroCola registro)
	{
		Registro = registro;

		var clase = registro.Clase == ClaseRegistro.Incidencia ? "Incidencia" : "Evidencia local";

		// Va la SEVERIDAD, no la prioridad de sincronización. Antes iba la prioridad rotulada
		// como severidad, y la prioridad solo tiene dos valores: Advertencia e Información se
		// leían "Normal". La prioridad sigue existiendo y sigue ordenando la cola; lo que no
		// hace es hacerse pasar por otra cosa.
		TextoDatos =
			$"{clase} / {registro.SeveridadLegible} / {registro.Descripcion} / KM {registro.Kilometro}";

		(TextoEstado, ColorFondoEstado, ColorTextoEstado) = registro.Estado switch
		{
			EstadoSincronizacion.Sincronizado => ("SINCRONIZADO", "#D8F0E0", "#1E7A46"),
			EstadoSincronizacion.Enviando => ("ENVIANDO", "#FBE9C8", "#8A6100"),
			EstadoSincronizacion.Fallido => ("FALLIDO", "#F8D7D5", "#A03028"),
			EstadoSincronizacion.Borrador => ("BORRADOR", "#E9E0CE", "#3A2F2A"),
			_ => ("PENDIENTE", "#F7DCE6", "#92264F"),
		};
	}

	/// <summary>Registro original, por si la vista necesita algo más.</summary>
	public RegistroCola Registro { get; }

	public string ClaveLocal => Registro.ClaveLocal;

	/// <summary>
	/// Referencia que encabeza la tarjeta: el folio si ya llegó, la clave local mientras no.
	/// </summary>
	/// <remarks>
	/// La decide el modelo de aplicación, no esta clase: es el CA 1 de JTT-1403 y tiene un solo
	/// dueño, donde además se puede probar. Aquí solo se muestra.
	/// </remarks>
	public string ReferenciaPrincipal => Registro.ReferenciaPrincipal;

	/// <summary>Línea de datos: clase, severidad, descripción y kilómetro.</summary>
	public string TextoDatos { get; }

	public string TextoEstado { get; }

	public string ColorFondoEstado { get; }

	public string ColorTextoEstado { get; }

	/// <summary>
	/// Línea de trazabilidad: la clave local del registro, ya confirmado, y la confirmación de
	/// que llegó al CCO (JTT-1403 CA 2).
	/// </summary>
	/// <remarks>
	/// Cuando el folio encabeza la tarjeta, la clave local baja aquí en vez de desaparecer. Es
	/// la única referencia común entre lo que el operador ve, la base del dispositivo y la
	/// bitácora local, así que sin ella un registro confirmado deja de poder rastrearse hacia
	/// atrás.
	/// </remarks>
	public string TextoTrazabilidad => $"{Registro.ClaveLocal} · Visible en Incidencias";

	/// <summary>Si la tarjeta lleva línea de trazabilidad, que es tanto como decir si ya tiene folio.</summary>
	public bool MuestraTrazabilidad => Registro.TieneFolio;

	/// <summary>
	/// Por qué no salió este registro. Solo en los fallidos.
	/// </summary>
	/// <remarks>
	/// El texto lo compone <see cref="RegistroCola.MotivoFallo"/>, no esta clase: qué se le dice
	/// al operador se decide donde hay pruebas. Aquí solo se muestra.
	/// </remarks>
	public string TextoMotivoFallo => Registro.MotivoFallo;

	/// <summary>Si la tarjeta lleva la línea del motivo.</summary>
	public bool MuestraMotivoFallo => Registro.HayMotivoFallo;

	/// <summary>
	/// Cuándo va a reintentarse este registro, en hora local.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Se muestra la hora y no una cuenta atrás.</b> Una cuenta atrás obliga a repintar cada
	/// segundo mientras la pantalla esté abierta; la hora se calcula una vez y no vuelve a
	/// tocarse. La espera llega a treinta minutos, así que un contador sería además una cifra
	/// larga cambiando sin parar delante de alguien que solo quiere saber si tiene que hacer algo.
	/// </para>
	/// <para>
	/// <b>En hora local</b>, que es la única que el operador puede comparar con su reloj. El
	/// instante viaja en UTC, como todo lo demás.
	/// </para>
	/// </remarks>
	public string TextoReintento
	{
		get
		{
			if (Registro.ReintentoUtc is not { } reintento)
			{
				return string.Empty;
			}

			// Ya venció y sigue ahí: no hay enlace, o la comprobación no ha llegado todavía.
			// Anunciar una hora pasada haría dudar de si la aplicación sigue intentando algo.
			return reintento <= DateTime.UtcNow
				? "Listo para reintentar."
				: $"Reintento a las {reintento.ToLocalTime():HH:mm}.";
		}
	}

	/// <summary>Si la tarjeta lleva la línea del reintento.</summary>
	public bool MuestraReintento => Registro.HayReintentoProgramado;
}
