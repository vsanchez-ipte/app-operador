using System.Diagnostics.CodeAnalysis;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Servicios;

/// <summary>Qué le falta a un formulario para poder entrar a la cola de envío.</summary>
public enum MotivoFormularioIncompleto
{
	FaltaTipo = 1,
	FaltaSeveridad = 2,
	KilometroInvalido = 3,
	NotaInsuficiente = 4,
}

/// <summary>
/// Las comprobaciones que debe superar una captura antes de quedar como <c>Pendiente</c>.
/// </summary>
/// <remarks>
/// <para>
/// Son las mismas para convertir un borrador (JTT-1399 CA 9) y para corregir un registro
/// rechazado (JTT-291 CA 8): en los dos casos algo que ya está guardado pasa a la cola, y lo que
/// Jacob va a validar es idéntico. Tenerlas en un solo sitio evita que un caso admita lo que el
/// otro rechaza.
/// </para>
/// <para>
/// El orden importa y se conserva el de siempre: tipo, kilómetro, severidad, nota. Es el orden
/// del formulario, y el primer hueco que se encuentra es el primero que el operador ve.
/// </para>
/// </remarks>
public static class FormularioIncidencia
{
	/// <returns>
	/// <see langword="true"/> si el formulario está completo —y entonces el tipo, la severidad y
	/// <paramref name="kilometroValido"/> se pueden usar sin comprobar—, o <see langword="false"/>
	/// con el primer hueco en <paramref name="motivo"/>.
	/// </returns>
	public static bool EstaCompleto(
		[NotNullWhen(true)] TipoIncidencia? tipo,
		string? kilometro,
		[NotNullWhen(true)] SeveridadIncidencia? severidad,
		string nota,
		[NotNullWhen(true)] out Kilometer? kilometroValido,
		[NotNullWhen(false)] out MotivoFormularioIncompleto? motivo)
	{
		kilometroValido = null;
		motivo = null;

		if (tipo is null)
		{
			motivo = MotivoFormularioIncompleto.FaltaTipo;
			return false;
		}

		if (!Kilometer.IntentarCrear(kilometro, out kilometroValido))
		{
			motivo = MotivoFormularioIncompleto.KilometroInvalido;
			return false;
		}

		if (severidad is null)
		{
			motivo = MotivoFormularioIncompleto.FaltaSeveridad;
			return false;
		}

		if (!ReglaNotaIncidencia.EsSuficiente(tipo.ExigeDescripcion, nota))
		{
			motivo = MotivoFormularioIncompleto.NotaInsuficiente;
			return false;
		}

		return true;
	}
}
