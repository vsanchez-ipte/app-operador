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
		var prioridad = registro.Prioridad == SyncPriority.Critica ? "Crítica" : "Normal";
		TextoDatos = $"{clase} / {prioridad} / {registro.Descripcion} / KM {registro.Kilometro}";

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

	/// <summary>Línea de datos: clase, prioridad, descripción y kilómetro.</summary>
	public string TextoDatos { get; }

	public string TextoEstado { get; }

	public string ColorFondoEstado { get; }

	public string ColorTextoEstado { get; }

	/// <summary>
	/// Folio central y confirmación de llegada al CCO.
	/// </summary>
	/// <remarks>El folio no existe hasta que Jacob confirma el registro (DA-15).</remarks>
	public string TextoFolio => $"{Registro.FolioCentral} · Visible en Incidencias";

	public bool MuestraFolio => !string.IsNullOrEmpty(Registro.FolioCentral);
}
