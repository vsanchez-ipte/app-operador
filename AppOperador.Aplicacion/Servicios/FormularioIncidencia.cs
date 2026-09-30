using System.Diagnostics.CodeAnalysis;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Servicios;

public enum MotivoFormularioIncompleto
{
	FaltaTipo = 1,
	FaltaSeveridad = 2,
	KilometroInvalido = 3,
	NotaInsuficiente = 4,
}

public static class FormularioIncidencia
{
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
