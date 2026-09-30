using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AppOperador.Domain.ValueObjects;

public sealed partial class Kilometer : IEquatable<Kilometer>
{
	public const string FormatoCanonico = "000+000";

	private Kilometer(string valor, int kilometros, int metros)
	{
		Valor = valor;
		Kilometros = kilometros;
		Metros = metros;
	}

	public string Valor { get; }

	public int Kilometros { get; }

	public int Metros { get; }

	public static Kilometer Crear(string? valor)
	{
		if (!IntentarCrear(valor, out var kilometro))
		{
			throw new ArgumentException(
				$"El kilómetro '{valor ?? "(nulo)"}' no respeta la forma canónica {FormatoCanonico}: " +
				"exactamente tres dígitos, signo '+' y exactamente tres dígitos.",
				nameof(valor));
		}

		return kilometro;
	}

	public static Kilometer DesdeMetros(int metros)
	{
		if (metros is < 0 or > 999_999)
		{
			throw new ArgumentOutOfRangeException(
				nameof(metros),
				metros,
				$"El punto kilométrico en metros tiene que caber en la forma {FormatoCanonico}: " +
				"entre 0 y 999999.");
		}

		var (kilometros, resto) = Math.DivRem(metros, 1000);
		return new Kilometer($"{kilometros:D3}+{resto:D3}", kilometros, resto);
	}

	// Ojo: el API recibe kilómetros decimales (147.716), no metros.
	public int MetrosNormalizados => (Kilometros * 1000) + Metros;

	public static bool IntentarCrear(string? valor, [NotNullWhen(true)] out Kilometer? kilometro)
	{
		kilometro = null;

		if (valor is null)
		{
			return false;
		}

		var coincidencia = PatronCanonico().Match(valor);
		if (!coincidencia.Success)
		{
			return false;
		}

		var kilometros = int.Parse(coincidencia.Groups["km"].Value);
		var metros = int.Parse(coincidencia.Groups["m"].Value);

		kilometro = new Kilometer(valor, kilometros, metros);
		return true;
	}

	public bool Equals(Kilometer? otro) => otro is not null && Valor == otro.Valor;

	public override bool Equals(object? obj) => Equals(obj as Kilometer);

	public override int GetHashCode() => Valor.GetHashCode(StringComparison.Ordinal);

	public override string ToString() => Valor;

	public static bool operator ==(Kilometer? izquierdo, Kilometer? derecho) =>
		izquierdo is null ? derecho is null : izquierdo.Equals(derecho);

	public static bool operator !=(Kilometer? izquierdo, Kilometer? derecho) => !(izquierdo == derecho);

	// \A y \z en vez de ^ y $: en .NET, $ acepta un salto de línea al final.
	[GeneratedRegex(@"\A(?<km>[0-9]{3})\+(?<m>[0-9]{3})\z")]
	private static partial Regex PatronCanonico();
}
