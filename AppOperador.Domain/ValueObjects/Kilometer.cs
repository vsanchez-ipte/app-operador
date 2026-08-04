using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace AppOperador.Domain.ValueObjects;

/// <summary>
/// Punto kilométrico en su forma canónica <c>000+000</c>: exactamente tres dígitos,
/// signo <c>+</c> y exactamente tres dígitos. No existe forma de construir un
/// <see cref="Kilometer"/> con un valor fuera de ese formato.
/// </summary>
/// <remarks>
/// El nombre del tipo está fijado en inglés por el documento de arquitectura.
/// Se compara por valor: dos instancias con el mismo <see cref="Valor"/> son iguales.
/// </remarks>
public sealed partial class Kilometer : IEquatable<Kilometer>
{
	/// <summary>Forma canónica exigida al texto de entrada.</summary>
	public const string FormatoCanonico = "000+000";

	private Kilometer(string valor, int kilometros, int metros)
	{
		Valor = valor;
		Kilometros = kilometros;
		Metros = metros;
	}

	/// <summary>Representación canónica, por ejemplo <c>012+345</c>.</summary>
	public string Valor { get; }

	/// <summary>Parte entera de kilómetros, entre 0 y 999.</summary>
	public int Kilometros { get; }

	/// <summary>Parte de metros, entre 0 y 999.</summary>
	public int Metros { get; }

	/// <summary>
	/// Crea un <see cref="Kilometer"/> y falla de forma explícita si el valor no
	/// respeta la forma canónica.
	/// </summary>
	/// <exception cref="ArgumentException">El valor no respeta <c>000+000</c>.</exception>
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

	/// <summary>
	/// Intenta crear un <see cref="Kilometer"/> sin lanzar excepciones.
	/// </summary>
	/// <returns><see langword="true"/> si el valor respeta la forma canónica.</returns>
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

	// Anclado con \A y \z (no ^ y $): en .NET '$' también acepta un salto de línea
	// final, con lo que "123+456\n" pasaría el filtro. Así se rechaza cualquier
	// carácter sobrante, incluidos espacios y saltos de línea.
	[GeneratedRegex(@"\A(?<km>[0-9]{3})\+(?<m>[0-9]{3})\z")]
	private static partial Regex PatronCanonico();
}
